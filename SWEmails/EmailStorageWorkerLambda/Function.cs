using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Gestaomax.EmailStorageWorkerLambda;

/// <summary>
/// Email Storage Worker Lambda — the persistence layer of the email sync pipeline.
///
/// Triggered by S3 ObjectCreated events. Reads JSON files written by the
/// CaptureWorker Lambda and persists them to the target SQL Server database
/// via stored procedures.
///
/// The S3 folder prefix determines which stored procedure is called:
///   leituras-pendentes/  → pr_EmailsSincSalvaEmailCapturado  (new email)
///   tokens-atualizados/  → pr_EmailsSincSalvaTokens          (token/UID update)
///
/// After a successful write, the S3 file is deleted to prevent reprocessing.
/// On failure, the file is moved to an error folder (erros-storage/) for
/// manual inspection without data loss.
///
/// Required environment variables:
///   SQL_CONNECTION_STRING — SQL Server connection string (use Secrets Manager in prod)
/// </summary>
public class Function
{
    // Reuse the S3 client across invocations to avoid socket exhaustion
    private static readonly IAmazonS3 _s3Client = new AmazonS3Client();
    private readonly string _baseConnectionString;

    public Function()
    {
        _baseConnectionString = Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING")
            ?? throw new InvalidOperationException("SQL_CONNECTION_STRING environment variable is not set.");
    }

    /// <summary>
    /// Lambda entry point — processes a batch of S3 ObjectCreated events.
    ///
    /// Each S3 event corresponds to one JSON file written by the CaptureWorker.
    /// Files are processed sequentially; a failure on one file does not stop others.
    /// </summary>
    public async Task FunctionHandler(S3Event s3Event, ILambdaContext context)
    {
        if (s3Event.Records == null || s3Event.Records.Count == 0) return;

        foreach (var record in s3Event.Records)
        {
            string bucket = record.S3.Bucket.Name;
            // URL-decode the S3 key — S3 encodes spaces as '+' in event notifications
            string s3Key = System.Net.WebUtility.UrlDecode(record.S3.Object.Key);

            context.Logger.LogInformation($"[S3 EVENT] Processando: {s3Key}");

            try
            {
                // Step 1: Download the JSON file from S3 into memory
                string jsonBruto;
                using (var response = await _s3Client.GetObjectAsync(bucket, s3Key))
                using (var reader = new StreamReader(response.ResponseStream))
                {
                    jsonBruto = await reader.ReadToEndAsync();
                }

                // Step 2: Parse just enough to extract NomeBanco for connection routing.
                // The full JSON is passed as-is to the stored procedure as NVARCHAR(MAX).
                using var doc = JsonDocument.Parse(jsonBruto);
                if (!doc.RootElement.TryGetProperty("NomeBanco", out var bancoProp)
                    || string.IsNullOrWhiteSpace(bancoProp.GetString()))
                {
                    throw new Exception("JSON sem a propriedade obrigatória 'NomeBanco'.");
                }

                string nomeBanco = bancoProp.GetString().Trim();

                // Step 3: Route to the correct stored procedure based on the S3 folder prefix
                if (s3Key.StartsWith("leituras-pendentes/", StringComparison.OrdinalIgnoreCase))
                {
                    context.Logger.LogInformation($"[E-MAIL] Persistindo e-mail capturado no banco {nomeBanco}...");
                    await ExecutarProcedureNoBancoAsync(nomeBanco, "dbo.pr_EmailsSincSalvaEmailCapturado", jsonBruto);
                }
                else if (s3Key.StartsWith("tokens-atualizados/", StringComparison.OrdinalIgnoreCase))
                {
                    context.Logger.LogInformation($"[TOKENS] Persistindo atualização de token no banco {nomeBanco}...");
                    await ExecutarProcedureNoBancoAsync(nomeBanco, "dbo.pr_EmailsSincSalvaTokens", jsonBruto);
                }
                else
                {
                    context.Logger.LogWarning($"[IGNORADO] Arquivo fora das pastas monitoradas: {s3Key}");
                    continue;
                }

                // Step 4: Delete the S3 file after successful persistence to prevent duplicate processing
                await _s3Client.DeleteObjectAsync(bucket, s3Key);
                context.Logger.LogInformation($"[CONCLUÍDO] Processado e apagado: {s3Key}");
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"[FALHA DE PERSISTÊNCIA] {s3Key}: {ex.Message}");
                // Move to quarantine folder so the JSON is not lost even if SQL fails
                await MoverParaPastaDeErroAsync(bucket, s3Key, context);
            }
        }
    }

    /// <summary>
    /// Calls a stored procedure on the target client database, passing the full
    /// JSON payload as a single NVARCHAR(MAX) parameter (@JsonPayload).
    ///
    /// The database is selected dynamically via SqlConnectionStringBuilder.InitialCatalog.
    /// This allows a single Lambda to serve multiple client databases without
    /// maintaining a separate connection string per client.
    /// </summary>
    private async Task ExecutarProcedureNoBancoAsync(string nomeBanco, string procedureName, string jsonPayload)
    {
        // Route to the system (_sys) database which holds the cross-tenant procedures
        var builder = new SqlConnectionStringBuilder(_baseConnectionString)
        {
            InitialCatalog = "_sys",
            ConnectTimeout = 15 // Fail fast if the database is under stress
        };

        await using var conn = new SqlConnection(builder.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = new SqlCommand(procedureName, conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 30
        };

        // Pass the JSON as NVARCHAR(MAX) (-1) to avoid implicit conversions and
        // to preserve special characters (emojis, accents, Unicode) perfectly
        cmd.Parameters.Add(new SqlParameter("@JsonPayload", SqlDbType.NVarChar, -1)
        {
            Value = jsonPayload
        });

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Moves a file that failed to persist to an error quarantine folder in S3.
    ///
    /// Error folder mapping:
    ///   leituras-pendentes/ → erros-storage/emails/
    ///   tokens-atualizados/ → erros-storage/tokens/
    ///
    /// The file is copied then deleted (S3 has no rename/move operation).
    /// A failure here is logged but does not throw — the original error takes priority.
    /// </summary>
    private async Task MoverParaPastaDeErroAsync(string bucket, string oldKey, ILambdaContext context)
    {
        try
        {
            string newKey = oldKey.StartsWith("leituras-pendentes/", StringComparison.OrdinalIgnoreCase)
                ? oldKey.Replace("leituras-pendentes/", "erros-storage/emails/")
                : oldKey.Replace("tokens-atualizados/", "erros-storage/tokens/");

            await _s3Client.CopyObjectAsync(bucket, oldKey, bucket, newKey);
            await _s3Client.DeleteObjectAsync(bucket, oldKey);

            context.Logger.LogWarning($"[QUARENTENA] Arquivo movido para: {newKey}");
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"[ERRO GRAVE S3] Não foi possível mover {oldKey} para quarentena: {ex.Message}");
        }
    }
}
