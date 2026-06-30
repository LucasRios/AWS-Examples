using Amazon.Lambda.Core;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Gestaomax.EmailDispatcherLambda;

/// <summary>
/// SQS message contract sent to the CaptureWorker queue.
/// Contains encrypted credentials so the worker never needs to hit SQL Server directly.
/// </summary>
public class EmailCaptureQueueMessage
{
    public int CodConta { get; set; }
    public string NomeBanco { get; set; }
    public string Provedor { get; set; }
    /// <summary>Rijndael-256 encrypted JSON blob with IMAP/OAuth credentials.</summary>
    public string DadosCriptografados { get; set; }
    public string Acao { get; set; } = "PuxarEmails";
}

/// <summary>
/// Email Dispatcher Lambda — the orchestrator of the email sync pipeline.
///
/// Triggered on a schedule (e.g. EventBridge cron). For each email account
/// that is due for sync, it:
///   1. Calls a SQL stored procedure to claim accounts (mark them as "Processing").
///   2. Sends a message per account to the CaptureWorker SQS queue.
///   3. The CaptureWorker Lambda picks up each message and fetches the actual emails.
///
/// Credentials are encrypted inside the SQL result set (via a SQL CLR function),
/// so neither the Dispatcher nor the CaptureWorker ever see plain-text passwords
/// — only the encrypted blob is passed through SQS.
///
/// Required environment variables:
///   SQL_CONNECTION_STRING — Full SQL Server connection string (use Secrets Manager in prod)
///   SQS_FILA_WORKER       — SQS queue URL for the CaptureWorker
/// </summary>
public class Function
{
    private readonly string _connectionString;
    private readonly IAmazonSQS _sqsClient;
    private readonly string _filaWorker;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Preserve '+' and '/' characters that appear in Base64-encoded data
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public Function()
    {
        // Read configuration from Lambda environment variables.
        // In production, SQL_CONNECTION_STRING should reference AWS Secrets Manager.
        _connectionString = GetEnv("SQL_CONNECTION_STRING");
        _filaWorker = GetEnv("SQS_FILA_WORKER");
        _sqsClient = new AmazonSQSClient();
    }

    /// <summary>
    /// Lambda entry point — invoked on a schedule (e.g. every 5 minutes via EventBridge).
    /// Reads pending accounts from SQL Server and dispatches one SQS message per account.
    /// </summary>
    public async Task FunctionHandler(object evnt, ILambdaContext context)
    {
        var sw = Stopwatch.StartNew();
        context.Logger.LogInformation("Dispatcher iniciado. Coletando contas pendentes...");

        // Step 1: Claim accounts that are due for sync.
        // The stored procedure atomically marks accounts as "Processing" to prevent
        // duplicate dispatch if the Lambda is invoked concurrently.
        List<EmailCaptureQueueMessage> contasPendentes;
        try
        {
            contasPendentes = await ClaimarContasPendentesAsync(context);
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"[CRÍTICO] Falha ao ler contas do SQL Server: {ex.GetType().Name} — {ex.Message}");
            throw;
        }

        if (contasPendentes.Count == 0)
        {
            context.Logger.LogInformation("Nenhuma conta necessitando de sincronização neste ciclo.");
            return;
        }

        context.Logger.LogInformation($"{contasPendentes.Count} conta(s) prontas. Despachando para SQS...");

        // Step 2: Send each account to the CaptureWorker queue in batches of 10 (SQS limit)
        var (enviados, falhos) = await DespacharParaSqsAsync(contasPendentes, context);

        sw.Stop();
        context.Logger.LogInformation(
            $"Dispatcher concluído. Enviados: {enviados} | Falhas: {falhos.Count} | " +
            $"Tempo: {sw.ElapsedMilliseconds}ms");
    }

    /// <summary>
    /// Executes the SQL stored procedure that returns accounts ready for email sync.
    ///
    /// The procedure (_sys.dbo.pr_ProcessaFilaEmailLeitura) atomically:
    ///   - Selects accounts where StatusSincronismo = 'Aguardando'
    ///   - Updates their status to 'Processando' to prevent double-dispatch
    ///   - Returns the encrypted credentials blob (DadosConexao) per account
    /// </summary>
    private async Task<List<EmailCaptureQueueMessage>> ClaimarContasPendentesAsync(ILambdaContext context)
    {
        var lista = new List<EmailCaptureQueueMessage>();

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // The stored procedure handles locking internally — no explicit transaction needed here
        await using var cmd = new SqlCommand("exec _sys.dbo.pr_ProcessaFilaEmailLeitura", conn)
        {
            CommandTimeout = 30
        };

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            int codConta = reader.GetInt32(reader.GetOrdinal("CodCRMEmailsConfig"));
            string nomeBanco = reader.GetString(reader.GetOrdinal("NomeBanco")).ToLower().Trim();
            string provider = reader.GetString(reader.GetOrdinal("Provedor")).ToLower().Trim();

            // Preserve the original Base64 casing — .ToLower() would corrupt the encrypted data
            string dadosCripto = reader.GetString(reader.GetOrdinal("DadosConexao")).Trim();

            if (string.IsNullOrWhiteSpace(dadosCripto))
            {
                context.Logger.LogWarning($"Conta {codConta} sem dados de credenciais — ignorando.");
                continue;
            }

            lista.Add(new EmailCaptureQueueMessage
            {
                CodConta = codConta,
                NomeBanco = nomeBanco,
                Provedor = provider,
                DadosCriptografados = dadosCripto
            });
        }

        return lista;
    }

    /// <summary>
    /// Sends a batch of account messages to the CaptureWorker SQS queue.
    ///
    /// SQS SendMessageBatch accepts up to 10 entries per call.
    /// Returns the count of successfully sent messages and a list of failed account IDs.
    /// </summary>
    private async Task<(int enviados, List<int> falhos)> DespacharParaSqsAsync(
        List<EmailCaptureQueueMessage> items, ILambdaContext context)
    {
        const int maxSqsBatch = 10;
        var falhos = new List<int>();
        int enviados = 0;

        for (int i = 0; i < items.Count; i += maxSqsBatch)
        {
            var batch = items.Skip(i).Take(maxSqsBatch).ToList();

            // Each entry needs a unique ID within the batch (not a global ID)
            var entries = batch.Select(item => new SendMessageBatchRequestEntry
            {
                Id = $"Conta_{item.NomeBanco}_{item.CodConta}",
                MessageBody = JsonSerializer.Serialize(item, _jsonOpts)
            }).ToList();

            try
            {
                var response = await _sqsClient.SendMessageBatchAsync(new SendMessageBatchRequest
                {
                    QueueUrl = _filaWorker,
                    Entries = entries
                });

                enviados += response.Successful.Count;

                foreach (var fail in response.Failed ?? Enumerable.Empty<BatchResultErrorEntry>())
                {
                    context.Logger.LogError($"[SQS REJEITOU] {fail.Id}: {fail.Message}");
                    if (int.TryParse(fail.Id.Replace("Conta_", ""), out int id))
                        falhos.Add(id);
                }
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"[FALHA BATCH SQS] {ex.Message}");
                falhos.AddRange(batch.Select(b => b.CodConta));
            }
        }

        return (enviados, falhos);
    }

    /// <summary>
    /// Rolls back the status of failed accounts to "Aguardando" so they are
    /// retried in the next Dispatcher invocation.
    /// </summary>
    private async Task RollbackStatusContasAsync(List<int> codigosContas, ILambdaContext context)
    {
        var paramNames = codigosContas.Select((_, idx) => $"@c{idx}").ToList();

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        string sql = $@"
            UPDATE dbo.CRMEmailsConfig
            SET    StatusSincronismo = 'Aguardando', UltimaSincronizacao = GETDATE()
            WHERE  CodCRMEmailsConfig IN ({string.Join(',', paramNames)})";

        await using var cmd = new SqlCommand(sql, conn);
        for (int i = 0; i < codigosContas.Count; i++)
            cmd.Parameters.AddWithValue($"@c{i}", codigosContas[i]);

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Reads a required environment variable, throwing a clear error if it is missing.
    /// </summary>
    private static string GetEnv(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Variável de ambiente '{name}' não configurada.");
}
