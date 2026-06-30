using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Lambda.SQSEvents;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Gestaomax.EmailCaptureWorkerLambda;

/// <summary>
/// SQS message contract published by the Dispatcher Lambda.
/// Contains the encrypted credentials blob and routing metadata.
/// </summary>
public class EmailCaptureQueueMessage
{
    public int CodConta { get; set; }
    public string NomeBanco { get; set; }
    public string Provedor { get; set; }
    /// <summary>Rijndael-256 encrypted JSON with IMAP/OAuth credentials.</summary>
    public string DadosCriptografados { get; set; }
    public string Acao { get; set; }
}

/// <summary>
/// Decrypted credential model. Fields are populated according to the provider:
/// IMAP providers use Email/Senha/ImapHost/etc., OAuth providers use tokens.
/// </summary>
public class EmailContasCredenciais
{
    public string Email { get; set; }
    public string NomeExibicao { get; set; }
    public string GoogleAccessToken { get; set; }
    public string GoogleRefreshToken { get; set; }
    public string MicrosoftAccessToken { get; set; }
    public string MicrosoftRefreshToken { get; set; }
    public string Senha { get; set; }
    public string ImapHost { get; set; }
    public int? ImapPorta { get; set; }
    public string ImapSeguranca { get; set; }
    public string SmtpHost { get; set; }
    public int? SmtpPorta { get; set; }
    public string SmtpSeguranca { get; set; }
    public long UltimoUidProcessado { get; set; }
    public DateTime UltimaSinc { get; set; }
}

public class Function
{
    // S3 bucket where raw emails and token updates are staged
    // before the StorageWorker Lambda persists them to SQL Server.
    private readonly string _bucketName;

    public Function()
    {
        // Read S3 bucket name from environment so it can differ per stage (dev/prod).
        _bucketName = Environment.GetEnvironmentVariable("S3_EMAIL_BUCKET")
            ?? throw new InvalidOperationException("S3_EMAIL_BUCKET environment variable is not set.");
    }

    /// <summary>
    /// Lambda entry point. Processes a batch of SQS records — each record
    /// represents one email account that needs to be synced.
    ///
    /// The batch model ensures that a failure in one account does not block the others.
    /// Each SQS record is processed independently with its own try/catch.
    /// </summary>
    public async Task FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
    {
        if (sqsEvent?.Records == null || sqsEvent.Records.Count == 0)
        {
            context.Logger.LogWarning("Worker invocado com evento SQS vazio. Ignorando.");
            return;
        }

        context.Logger.LogInformation($"Worker iniciado. Processando {sqsEvent.Records.Count} mensagem(ns) do SQS.");

        foreach (var record in sqsEvent.Records)
        {
            try
            {
                // Step 1: Deserialize the SQS message contract
                var msg = JsonSerializer.Deserialize<EmailCaptureQueueMessage>(record.Body);
                if (msg == null || string.IsNullOrWhiteSpace(msg.DadosCriptografados))
                {
                    context.Logger.LogError($"[CONTRATO INVÁLIDO] Mensagem {record.MessageId} vazia ou sem dados.");
                    continue;
                }

                context.Logger.LogInformation(
                    $"Conta: {msg.CodConta} | Banco: {msg.NomeBanco} | Provedor: {msg.Provedor}");

                // Step 2: Decrypt the credentials blob using the shared Rijndael-256 key.
                // The decryption algorithm must match the SQL CLR function that encrypted it.
                string jsonBruto;
                try
                {
                    jsonBruto = SqlCryptoHelper.Descriptografar(msg.DadosCriptografados);
                }
                catch (Exception ex)
                {
                    context.Logger.LogError(
                        $"[ERRO CRIPTOGRAFIA] Conta {msg.CodConta}: {ex.Message}");
                    continue;
                }

                var jsonOpts = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    NumberHandling = JsonNumberHandling.AllowReadingFromString
                };

                var credenciais = JsonSerializer.Deserialize<EmailContasCredenciais>(jsonBruto, jsonOpts);
                if (credenciais == null)
                {
                    context.Logger.LogError($"[JSON INVÁLIDO] Conta {msg.CodConta}: JSON inválido após descriptografia.");
                    continue;
                }

                // Step 3: Route to the correct provider handler
                switch (msg.Provedor.ToLower().Trim())
                {
                    case "kinghost":
                    case "icloud":
                    case "imap":
                        await ProcessarImapTradicionalAsync(msg.CodConta, msg.NomeBanco, credenciais, context);
                        break;

                    case "google":
                        await ProcessarGmailApiAsync(msg.CodConta, msg.NomeBanco, credenciais, context);
                        break;

                    case "microsoft":
                        await ProcessarGraphApiAsync(msg.CodConta, msg.NomeBanco, credenciais, context);
                        break;

                    default:
                        context.Logger.LogError(
                            $"[PROVEDOR DESCONHECIDO] '{msg.Provedor}' para conta {msg.CodConta}.");
                        break;
                }
            }
            catch (Exception ex)
            {
                // Catch-all per record to prevent a single failure from poisoning the entire batch
                context.Logger.LogError(
                    $"[FALHA CATASTRÓFICA] Registro {record.MessageId}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        context.Logger.LogInformation("Processamento do lote SQS finalizado.");
    }

    /// <summary>
    /// Handles email capture for IMAP-based providers (e.g. KingHost, iCloud, generic IMAP).
    ///
    /// Strategy:
    ///   - If UltimoUidProcessado > 0, fetch only messages with UID > last seen (incremental sync).
    ///   - Otherwise, fall back to fetching by delivery date (UltimaSinc).
    ///
    /// For each message:
    ///   - Downloads the full MIME body
    ///   - Extracts attachments and uploads them individually to S3 (anexos/ prefix)
    ///   - Saves the message metadata + body as a JSON blob to S3 (leituras-pendentes/ prefix)
    ///
    /// After the loop, if the UID advanced, writes a UID-update notification to S3
    /// (tokens-atualizados/ prefix) so the StorageWorker can persist the new pointer.
    /// </summary>
    private async Task ProcessarImapTradicionalAsync(
        int codConta, string NomeBanco, EmailContasCredenciais creds, ILambdaContext context)
    {
        context.Logger.LogInformation(
            $"[IMAP] Conectando para {creds.Email} em {creds.ImapHost}:{creds.ImapPorta ?? 993}");

        using var client = new ImapClient();
        using var s3Client = new AmazonS3Client();

        try
        {
            // Accept SSL certificates where only the name doesn't match the host —
            // some shared-hosting providers use wildcard certs that trigger this error.
            client.ServerCertificateValidationCallback = (sender, certificate, chain, errors) => true;

            bool useSsl = creds.ImapPorta == 993;
            await client.ConnectAsync(
                creds.ImapHost, creds.ImapPorta ?? 993,
                useSsl ? MailKit.Security.SecureSocketOptions.SslOnConnect
                       : MailKit.Security.SecureSocketOptions.Auto);

            await client.AuthenticateAsync(creds.Email, creds.Senha);
            await client.Inbox.OpenAsync(MailKit.FolderAccess.ReadOnly);

            // Build the search query — prefer UID-based (incremental) over date-based
            SearchQuery query;
            if (creds.UltimoUidProcessado > 0)
            {
                var uidInicio = new UniqueId((uint)(creds.UltimoUidProcessado + 1));
                query = SearchQuery.Uids(new UniqueIdRange(uidInicio, UniqueId.MaxValue));
                context.Logger.LogInformation($"Filtrando UIDs: {uidInicio}+");
            }
            else
            {
                query = SearchQuery.DeliveredAfter(creds.UltimaSinc);
                context.Logger.LogWarning($"Primeira sync. E-mails após: {creds.UltimaSinc:dd/MM/yyyy}");
            }

            var uids = await client.Inbox.SearchAsync(query);
            context.Logger.LogInformation($"Conta {codConta}: {uids.Count} nova(s) mensagem(ns).");

            uint maiorUid = (uint)creds.UltimoUidProcessado;

            foreach (var uid in uids)
            {
                try
                {
                    if (uid.Id > maiorUid)
                        maiorUid = uid.Id;

                    var summaries = await client.Inbox.FetchAsync(
                        new[] { uid }, MessageSummaryItems.Envelope | MessageSummaryItems.Size);
                    var summary = System.Linq.Enumerable.FirstOrDefault(summaries);

                    if (summary != null)
                        context.Logger.LogInformation(
                            $"UID: {uid.Id} | Assunto: {summary.Envelope.Subject} | {summary.Size} bytes");

                    var message = await client.Inbox.GetMessageAsync(uid);
                    string corpoHtml = message.HtmlBody ?? message.TextBody ?? string.Empty;

                    // Upload attachments individually to S3 and collect their S3 keys
                    var anexosProcessados = new System.Collections.Generic.List<object>();
                    foreach (var attachment in message.Attachments)
                    {
                        var fileName = attachment.ContentDisposition?.FileName
                            ?? attachment.ContentType.Name ?? "arquivo_sem_nome";
                        string s3KeyAnexo = $"anexos/{NomeBanco}/{codConta}/{Guid.NewGuid()}_{fileName}";

                        using var msAnexo = new MemoryStream();
                        if (attachment is MimePart mimePart)
                            await mimePart.Content.DecodeToAsync(msAnexo);
                        else
                            await ((MessagePart)attachment).Message.WriteToAsync(msAnexo);

                        msAnexo.Position = 0;
                        await s3Client.PutObjectAsync(new PutObjectRequest
                        {
                            BucketName = _bucketName,
                            Key = s3KeyAnexo,
                            InputStream = msAnexo,
                            ContentType = attachment.ContentType.MimeType
                        });

                        anexosProcessados.Add(new { NomeOriginal = fileName, S3Key = s3KeyAnexo });
                    }

                    // Serialize the full email payload (metadata + body + attachment refs) to S3.
                    // The StorageWorker Lambda picks this up and persists it to SQL Server.
                    var emailPayload = JsonSerializer.Serialize(new
                    {
                        CodConta = codConta,
                        NomeBanco = NomeBanco,
                        Uid = uid.Id,
                        Remetente = message.From.ToString(),
                        Destinatario = message.To.ToString(),
                        Cc = message.Cc.ToString(),
                        Cco = message.Bcc.ToString(),
                        Assunto = message.Subject,
                        DataEnvio = message.Date.DateTime,
                        Corpo = corpoHtml,
                        CorpoTexto = message.TextBody,
                        Anexos = anexosProcessados,
                        Raw = message.ToString()
                    });

                    string s3KeyJson = $"leituras-pendentes/{NomeBanco}/{codConta}_{uid.Id}.json";
                    using var msJson = new MemoryStream(Encoding.UTF8.GetBytes(emailPayload));
                    await s3Client.PutObjectAsync(new PutObjectRequest
                    {
                        BucketName = _bucketName,
                        Key = s3KeyJson,
                        InputStream = msJson,
                        ContentType = "application/json"
                    });
                }
                catch (Exception ex)
                {
                    context.Logger.LogError($"[ERRO MENSAGEM] UID {uid.Id} / conta {codConta}: {ex.Message}");
                }
            }

            // Notify the StorageWorker about the new IMAP UID watermark
            if (maiorUid > (uint)creds.UltimoUidProcessado)
            {
                context.Logger.LogInformation(
                    $"[FINALIZADO] Conta {codConta}: UID {creds.UltimoUidProcessado} → {maiorUid}");

                var payloadUid = JsonSerializer.Serialize(new
                {
                    TipoNotificacao = "ATUALIZAR_UID_IMAP",
                    CodConta = codConta,
                    NomeBanco = NomeBanco,
                    NovoUid = maiorUid,
                    DataAtualizacao = DateTime.UtcNow
                });

                string s3KeyUid = $"tokens-atualizados/{NomeBanco}/{codConta}_imap_uid.json";
                using var msUid = new MemoryStream(Encoding.UTF8.GetBytes(payloadUid));
                await s3Client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = s3KeyUid,
                    InputStream = msUid,
                    ContentType = "application/json"
                });
            }
            else
            {
                await NotificarCicloConcluidoAsync(codConta, NomeBanco, "imap", context);
            }

            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"[FALHA CRÍTICA IMAP] Conta {codConta} ({creds.Email}): {ex.Message}");
        }
    }

    /// <summary>
    /// Handles email capture for Google accounts via the Gmail REST API (OAuth2).
    ///
    /// Flow:
    ///   1. Refresh the access token using the stored refresh token + OAuth2 client credentials.
    ///      Client ID and Secret must be set as Lambda environment variables:
    ///        GOOGLE_CLIENT_ID, GOOGLE_CLIENT_SECRET
    ///   2. If Google rotates the refresh token, capture the new one.
    ///   3. List messages with the Gmail API using a date filter (after:YYYY/MM/DD).
    ///   4. For each message ID, download the raw MIME via format=raw.
    ///   5. Upload body + attachments to S3 (same structure as IMAP handler).
    ///   6. After the loop, write a TOKEN_REFRESH_GOOGLE notification to S3 if tokens changed.
    /// </summary>
    private async Task ProcessarGmailApiAsync(
        int codConta, string NomeBanco, EmailContasCredenciais creds, ILambdaContext context)
    {
        context.Logger.LogInformation($"[GMAIL API] Iniciando captura para {creds.Email}");

        using var httpClient = new System.Net.Http.HttpClient();
        using var s3Client = new AmazonS3Client();

        string accessTokenUtilizado = creds.GoogleAccessToken;
        string refreshTokenUtilizado = creds.GoogleRefreshToken;
        bool tokenFoiRenovado = false;

        try
        {
            // Read OAuth2 credentials from environment variables — never hardcode them.
            // Set these in the Lambda configuration or via AWS Secrets Manager.
            string clientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")
                ?? throw new InvalidOperationException("GOOGLE_CLIENT_ID env var not set.");
            string clientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET")
                ?? throw new InvalidOperationException("GOOGLE_CLIENT_SECRET env var not set.");

            // Step 1: Refresh the access token before making any API calls
            context.Logger.LogInformation($"[GMAIL API] Renovando access token para conta {codConta}...");

            var refreshParams = new System.Collections.Generic.Dictionary<string, string>
            {
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "refresh_token", creds.GoogleRefreshToken },
                { "grant_type", "refresh_token" }
            };

            var refreshResponse = await httpClient.PostAsync(
                "https://oauth2.googleapis.com/token",
                new System.Net.Http.FormUrlEncodedContent(refreshParams));

            if (!refreshResponse.IsSuccessStatusCode)
            {
                string erro = await refreshResponse.Content.ReadAsStringAsync();
                context.Logger.LogError($"[GMAIL API ERRO OAUTH] Conta {codConta}: {erro}");
                return;
            }

            using var jsonToken = JsonDocument.Parse(await refreshResponse.Content.ReadAsStringAsync());
            accessTokenUtilizado = jsonToken.RootElement.GetProperty("access_token").GetString();
            tokenFoiRenovado = true;

            // Google occasionally rotates the refresh token — capture it if present
            if (jsonToken.RootElement.TryGetProperty("refresh_token", out var refreshProp))
            {
                string novoRefresh = refreshProp.GetString();
                if (!string.IsNullOrWhiteSpace(novoRefresh) && novoRefresh != refreshTokenUtilizado)
                {
                    context.Logger.LogWarning($"[GMAIL API] Refresh token rotacionado para conta {codConta}.");
                    refreshTokenUtilizado = novoRefresh;
                }
            }

            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessTokenUtilizado);

            // Step 2: List message IDs since the last sync date
            var dataCorte = creds.UltimaSinc;
            string queryGmail = $"after:{dataCorte:yyyy/MM/dd}";
            string urlList = $"https://gmail.googleapis.com/gmail/v1/users/me/messages" +
                             $"?q={Uri.EscapeDataString(queryGmail)}&maxResults=50";

            var listResponse = await httpClient.GetAsync(urlList);
            if (!listResponse.IsSuccessStatusCode)
            {
                context.Logger.LogError($"[GMAIL API] Falha ao listar mensagens: {await listResponse.Content.ReadAsStringAsync()}");
                return;
            }

            using var jsonList = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
            if (!jsonList.RootElement.TryGetProperty("messages", out var messagesArray))
            {
                context.Logger.LogInformation($"[GMAIL API] Sem mensagens novas para conta {codConta}.");
                if (tokenFoiRenovado)
                    await NotificarMudancaTokensAsync(codConta, NomeBanco, "google", accessTokenUtilizado, refreshTokenUtilizado, context);
                else
                    await NotificarCicloConcluidoAsync(codConta, NomeBanco, "google", context);
                return;
            }

            context.Logger.LogInformation(
                $"[GMAIL API] {messagesArray.GetArrayLength()} mensagem(ns) para conta {codConta}.");

            // Step 3: Download each message in raw MIME format (format=raw)
            foreach (var msgObj in messagesArray.EnumerateArray())
            {
                string messageId = msgObj.GetProperty("id").GetString();

                try
                {
                    string urlDetail = $"https://gmail.googleapis.com/gmail/v1/users/me/messages/{messageId}?format=raw";
                    var detailResponse = await httpClient.GetAsync(urlDetail);
                    if (!detailResponse.IsSuccessStatusCode) continue;

                    using var jsonDetail = JsonDocument.Parse(await detailResponse.Content.ReadAsStringAsync());
                    string rawBase64Url = jsonDetail.RootElement.GetProperty("raw").GetString();

                    // Convert URL-safe Base64 to standard Base64 and add padding
                    string rawBase64 = rawBase64Url.Replace('-', '+').Replace('_', '/');
                    switch (rawBase64.Length % 4)
                    {
                        case 2: rawBase64 += "=="; break;
                        case 3: rawBase64 += "="; break;
                    }

                    byte[] emailBytes = Convert.FromBase64String(rawBase64);

                    using var msMime = new MemoryStream(emailBytes);
                    var message = await MimeMessage.LoadAsync(msMime);

                    string corpoHtml = message.HtmlBody ?? message.TextBody ?? string.Empty;
                    var anexosProcessados = new System.Collections.Generic.List<object>();

                    foreach (var attachment in message.Attachments)
                    {
                        var fileName = attachment.ContentDisposition?.FileName
                            ?? attachment.ContentType.Name ?? "arquivo_sem_nome";
                        string s3KeyAnexo = $"anexos/{NomeBanco}/{codConta}/{Guid.NewGuid()}_{fileName}";

                        using var msAnexo = new MemoryStream();
                        if (attachment is MimePart mimePart)
                            await mimePart.Content.DecodeToAsync(msAnexo);
                        else
                            await ((MessagePart)attachment).Message.WriteToAsync(msAnexo);

                        msAnexo.Position = 0;
                        await s3Client.PutObjectAsync(new PutObjectRequest
                        {
                            BucketName = _bucketName,
                            Key = s3KeyAnexo,
                            InputStream = msAnexo,
                            ContentType = attachment.ContentType.MimeType
                        });

                        anexosProcessados.Add(new { NomeOriginal = fileName, S3Key = s3KeyAnexo });
                    }

                    var emailPayload = JsonSerializer.Serialize(new
                    {
                        CodConta = codConta,
                        NomeBanco = NomeBanco,
                        Uid = 0,
                        GoogleMessageId = messageId,
                        Remetente = message.From.ToString(),
                        Destinatario = message.To.ToString(),
                        Cc = message.Cc.ToString(),
                        Cco = message.Bcc.ToString(),
                        Assunto = message.Subject,
                        DataEnvio = message.Date.DateTime,
                        Corpo = corpoHtml,
                        CorpoTexto = message.TextBody,
                        Anexos = anexosProcessados,
                        Raw = message.ToString()
                    });

                    string s3KeyJson = $"leituras-pendentes/{NomeBanco}/{codConta}_{messageId}.json";
                    using var msJson = new MemoryStream(Encoding.UTF8.GetBytes(emailPayload));
                    await s3Client.PutObjectAsync(new PutObjectRequest
                    {
                        BucketName = _bucketName,
                        Key = s3KeyJson,
                        InputStream = msJson,
                        ContentType = "application/json"
                    });
                }
                catch (Exception ex)
                {
                    context.Logger.LogError($"[GMAIL API ERRO] ID {messageId} / conta {codConta}: {ex.Message}");
                }
            }

            if (tokenFoiRenovado)
                await NotificarMudancaTokensAsync(codConta, NomeBanco, "google", accessTokenUtilizado, refreshTokenUtilizado, context);
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"[GMAIL API FALHA CRÍTICA] Conta {codConta}: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles email capture for Microsoft accounts via the Microsoft Graph API (OAuth2).
    ///
    /// Similar to the Gmail handler but uses:
    ///   - Microsoft Identity Platform v2.0 for token refresh
    ///   - OData $filter for date-based message listing
    ///   - The /$value endpoint to download raw MIME bytes directly
    ///
    /// Env vars required:
    ///   MICROSOFT_CLIENT_ID, MICROSOFT_CLIENT_SECRET
    /// </summary>
    private async Task ProcessarGraphApiAsync(
        int codConta, string NomeBanco, EmailContasCredenciais creds, ILambdaContext context)
    {
        context.Logger.LogInformation($"[GRAPH API] Iniciando captura para {creds.Email}");

        using var httpClient = new System.Net.Http.HttpClient();
        using var s3Client = new AmazonS3Client();

        string accessTokenUtilizado = creds.MicrosoftAccessToken;
        string refreshTokenUtilizado = creds.MicrosoftRefreshToken;
        bool tokenFoiRenovado = false;

        try
        {
            // Read Azure AD app credentials from environment variables.
            // Register the app at https://portal.azure.com → Azure Active Directory → App registrations.
            string clientId = Environment.GetEnvironmentVariable("MICROSOFT_CLIENT_ID")
                ?? throw new InvalidOperationException("MICROSOFT_CLIENT_ID env var not set.");
            string clientSecret = Environment.GetEnvironmentVariable("MICROSOFT_CLIENT_SECRET")
                ?? throw new InvalidOperationException("MICROSOFT_CLIENT_SECRET env var not set.");
            string tenantId = Environment.GetEnvironmentVariable("MICROSOFT_TENANT_ID") ?? "common";

            context.Logger.LogInformation($"[GRAPH API] Renovando access token para conta {codConta}...");

            var refreshParams = new System.Collections.Generic.Dictionary<string, string>
            {
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "refresh_token", creds.MicrosoftRefreshToken },
                { "grant_type", "refresh_token" },
                { "scope", "https://graph.microsoft.com/.default" }
            };

            string urlToken = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";
            var refreshResponse = await httpClient.PostAsync(
                urlToken, new System.Net.Http.FormUrlEncodedContent(refreshParams));

            if (!refreshResponse.IsSuccessStatusCode)
            {
                string erro = await refreshResponse.Content.ReadAsStringAsync();
                context.Logger.LogError($"[GRAPH API ERRO OAUTH] Conta {codConta}: {erro}");
                return;
            }

            using var jsonToken = JsonDocument.Parse(await refreshResponse.Content.ReadAsStringAsync());
            accessTokenUtilizado = jsonToken.RootElement.GetProperty("access_token").GetString();
            tokenFoiRenovado = true;

            // Microsoft rotates refresh tokens frequently — always capture the new one
            if (jsonToken.RootElement.TryGetProperty("refresh_token", out var refreshProp))
            {
                string novoRefresh = refreshProp.GetString();
                if (!string.IsNullOrWhiteSpace(novoRefresh) && novoRefresh != refreshTokenUtilizado)
                {
                    context.Logger.LogWarning($"[GRAPH API] Refresh token rotacionado para conta {codConta}.");
                    refreshTokenUtilizado = novoRefresh;
                }
            }

            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessTokenUtilizado);

            // OData date filter for the Graph API (ISO 8601 format required)
            var dataCorte = creds.UltimaSinc;
            string filterOData = $"receivedDateTime ge {dataCorte:yyyy-MM-ddTHH:mm:ssZ}";
            string urlList = $"https://graph.microsoft.com/v1.0/users/{creds.Email}/messages" +
                             $"?$filter={Uri.EscapeDataString(filterOData)}&$select=id&$top=50";

            var listResponse = await httpClient.GetAsync(urlList);
            if (!listResponse.IsSuccessStatusCode)
            {
                context.Logger.LogError($"[GRAPH API] Falha ao listar: {await listResponse.Content.ReadAsStringAsync()}");
                return;
            }

            using var jsonList = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
            if (!jsonList.RootElement.TryGetProperty("value", out var messagesArray)
                || messagesArray.GetArrayLength() == 0)
            {
                context.Logger.LogInformation($"[GRAPH API] Sem mensagens novas para conta {codConta}.");
                if (tokenFoiRenovado)
                    await NotificarMudancaTokensAsync(codConta, NomeBanco, "microsoft", accessTokenUtilizado, refreshTokenUtilizado, context);
                else
                    await NotificarCicloConcluidoAsync(codConta, NomeBanco, "microsoft", context);
                return;
            }

            context.Logger.LogInformation(
                $"[GRAPH API] {messagesArray.GetArrayLength()} mensagem(ns) para conta {codConta}.");

            foreach (var msgObj in messagesArray.EnumerateArray())
            {
                string messageId = msgObj.GetProperty("id").GetString();

                try
                {
                    // The /$value suffix returns the raw MIME bytes directly (no Base64 wrapping)
                    string urlDetail = $"https://graph.microsoft.com/v1.0/users/{creds.Email}/messages/{messageId}/$value";
                    var detailResponse = await httpClient.GetAsync(urlDetail);
                    if (!detailResponse.IsSuccessStatusCode) continue;

                    byte[] emailBytes = await detailResponse.Content.ReadAsByteArrayAsync();

                    using var msMime = new MemoryStream(emailBytes);
                    var message = await MimeMessage.LoadAsync(msMime);

                    string corpoHtml = message.HtmlBody ?? message.TextBody ?? string.Empty;
                    var anexosProcessados = new System.Collections.Generic.List<object>();

                    foreach (var attachment in message.Attachments)
                    {
                        var fileName = attachment.ContentDisposition?.FileName
                            ?? attachment.ContentType.Name ?? "arquivo_sem_nome";
                        string s3KeyAnexo = $"anexos/{NomeBanco}/{codConta}/{Guid.NewGuid()}_{fileName}";

                        using var msAnexo = new MemoryStream();
                        if (attachment is MimePart mimePart)
                            await mimePart.Content.DecodeToAsync(msAnexo);
                        else
                            await ((MessagePart)attachment).Message.WriteToAsync(msAnexo);

                        msAnexo.Position = 0;
                        await s3Client.PutObjectAsync(new PutObjectRequest
                        {
                            BucketName = _bucketName,
                            Key = s3KeyAnexo,
                            InputStream = msAnexo,
                            ContentType = attachment.ContentType.MimeType
                        });

                        anexosProcessados.Add(new { NomeOriginal = fileName, S3Key = s3KeyAnexo });
                    }

                    // Sanitize the Microsoft message ID for use as a safe S3 key component
                    // (Graph API IDs can contain '/' and '+' which would be misread as S3 path separators)
                    string safeMessageId = messageId.Replace("/", "-").Replace("+", "_");

                    var emailPayload = JsonSerializer.Serialize(new
                    {
                        CodConta = codConta,
                        NomeBanco = NomeBanco,
                        Uid = 0,
                        MicrosoftMessageId = messageId,
                        Remetente = message.From.ToString(),
                        Destinatario = message.To.ToString(),
                        Cc = message.Cc.ToString(),
                        Cco = message.Bcc.ToString(),
                        Assunto = message.Subject,
                        DataEnvio = message.Date.DateTime,
                        Corpo = corpoHtml,
                        CorpoTexto = message.TextBody,
                        Anexos = anexosProcessados,
                        Raw = message.ToString()
                    });

                    string s3KeyJson = $"leituras-pendentes/{NomeBanco}/{codConta}_{safeMessageId}.json";
                    using var msJson = new MemoryStream(Encoding.UTF8.GetBytes(emailPayload));
                    await s3Client.PutObjectAsync(new PutObjectRequest
                    {
                        BucketName = _bucketName,
                        Key = s3KeyJson,
                        InputStream = msJson,
                        ContentType = "application/json"
                    });
                }
                catch (Exception ex)
                {
                    context.Logger.LogError($"[GRAPH API ERRO] ID {messageId} / conta {codConta}: {ex.Message}");
                }
            }

            if (tokenFoiRenovado)
                await NotificarMudancaTokensAsync(codConta, NomeBanco, "microsoft", accessTokenUtilizado, refreshTokenUtilizado, context);
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"[GRAPH API FALHA CRÍTICA] Conta {codConta}: {ex.Message}");
        }
    }

    /// <summary>
    /// Writes a token-update notification to S3 so the StorageWorker can persist
    /// the new access/refresh token pair back to SQL Server.
    ///
    /// Called whenever a provider issues new tokens during a sync cycle.
    /// </summary>
    private async Task NotificarMudancaTokensAsync(
        int codConta, string nomeBanco, string provedor,
        string novoAccess, string novoRefresh, ILambdaContext context)
    {
        using var s3Client = new AmazonS3Client();
        string provUpper = provedor.ToUpper().Trim();

        var payload = JsonSerializer.Serialize(new
        {
            TipoNotificacao = $"TOKEN_REFRESH_{provUpper}",
            CodConta = codConta,
            NomeBanco = nomeBanco,
            AccessToken = novoAccess,
            RefreshToken = novoRefresh,
            DataAtualizacao = DateTime.UtcNow
        });

        string s3Key = $"tokens-atualizados/{nomeBanco}/{codConta}_{provedor.ToLower()}.json";

        try
        {
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(payload));
            await s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = s3Key,
                InputStream = ms,
                ContentType = "application/json"
            });

            context.Logger.LogInformation($"[TOKEN] {provUpper} persistido: {s3Key}");
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"[TOKEN ERRO] Conta {codConta}: {ex.Message}");
        }
    }

    /// <summary>
    /// Writes a CICLO_CONCLUIDO notification to S3 when a sync cycle finishes
    /// with no new messages and no token rotation.
    ///
    /// The StorageWorker uses this to reset the account status to "Aguardando"
    /// so the Dispatcher schedules the next sync cycle.
    /// </summary>
    private async Task NotificarCicloConcluidoAsync(
        int codConta, string nomeBanco, string provedor, ILambdaContext context)
    {
        context.Logger.LogInformation($"[FINALIZADO] Conta {codConta} ({provedor}) — sem novos e-mails.");

        using var s3Client = new AmazonS3Client();

        var payload = JsonSerializer.Serialize(new
        {
            TipoNotificacao = "CICLO_CONCLUIDO",
            CodConta = codConta,
            NomeBanco = nomeBanco,
            Provedor = provedor.ToLower(),
            DataAtualizacao = DateTime.UtcNow
        });

        string s3Key = $"tokens-atualizados/{nomeBanco}/{codConta}_concluido.json";

        try
        {
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(payload));
            await s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = s3Key,
                InputStream = ms,
                ContentType = "application/json"
            });
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"[CICLO ERRO] Conta {codConta}: {ex.Message}");
        }
    }
}

/// <summary>
/// Rijndael-256 (AES-256) decryption helper.
///
/// This is an exact reimplementation of the SQL Server CLR encryption function
/// used by the GestaoMax platform to encrypt IMAP/OAuth credentials at rest.
/// Using the same algorithm here ensures that tokens encrypted in SQL can be
/// decrypted inside the Lambda without an extra service call.
///
/// The encryption key and IV are read from environment variables:
///   RIJNDAEL_CRYPTO_KEY — Base64-encoded 32-byte AES key
///   RIJNDAEL_IV         — comma-separated 16 hex bytes (optional; falls back to default)
/// </summary>
public static class SqlCryptoHelper
{
    private static readonly byte[] bIV = LoadIV();
    private static readonly string cryptoKey = LoadKey();

    private static byte[] LoadIV()
    {
        string envIV = Environment.GetEnvironmentVariable("RIJNDAEL_IV");
        if (!string.IsNullOrEmpty(envIV))
        {
            string[] parts = envIV.Split(',');
            byte[] iv = new byte[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                iv[i] = Convert.ToByte(parts[i].Trim(), 16);
            return iv;
        }

        // Default IV — must match the SQL CLR implementation.
        return new byte[] {
            0x50, 0x08, 0xF1, 0xDD, 0xDE, 0x3C, 0xF2, 0x18,
            0x44, 0x74, 0x19, 0x2C, 0x53, 0x49, 0xAB, 0xBC
        };
    }

    private static string LoadKey()
    {
        return Environment.GetEnvironmentVariable("RIJNDAEL_CRYPTO_KEY")
            ?? "Q3JpcHRvZ3JhZmlhcyBjb20gUmluamRhZWwgLyBBRVM=";
    }

    public static string Descriptografar(string textoCriptografado)
    {
        byte[] bKey = Convert.FromBase64String(cryptoKey);
        byte[] bText = Convert.FromBase64String(textoCriptografado);

        // Use Aes.Create() (recommended in .NET 5+) which maps to native AES primitives
        // while maintaining full compatibility with RijndaelManaged from .NET Framework.
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Key = bKey;
        aes.IV = bIV;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var mStream = new MemoryStream(bText);
        using var cStream = new CryptoStream(mStream, decryptor, CryptoStreamMode.Read);
        using var reader = new StreamReader(cStream, Encoding.UTF8);

        return reader.ReadToEnd();
    }
}
