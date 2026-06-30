using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.Runtime;
using IniParser;
using IniParser.Model;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace SW_AWS_IP_UPDATE
{
    /// <summary>
    /// Resultado do envio de e-mail via SendGrid.
    /// </summary>
    public class EmailResponse
    {
        public DateTime DateSent { get; internal set; }
        public string UniqueMessageId { get; internal set; }
    }

    /// <summary>
    /// Exceção lançada quando o envio de e-mail falha.
    /// Carrega o corpo da resposta de erro da API SendGrid.
    /// </summary>
    public class EmailServiceException : Exception
    {
        public string Body { get; private set; }

        public EmailServiceException(string message, string body) : base(message)
        {
            Body = body;
        }

        public EmailServiceException(string message, string body, Exception innerException) : base(message, innerException)
        {
            Body = body;
        }
    }


    /// <summary>
    /// Utilitário que monitora o IP público da máquina e atualiza automaticamente
    /// as regras do Security Group no AWS quando o IP muda.
    ///
    /// Fluxo:
    ///   1. Obtém o IP público atual via https://ipinfo.io/ip
    ///   2. Compara com o IP salvo no arquivo swconfigIP.ini
    ///   3. Se mudou: revoga a regra antiga no Security Group e autoriza o novo IP
    ///   4. Atualiza o .ini com o novo IP
    ///   5. Envia e-mail de notificação via SendGrid
    ///
    /// Configuração (swconfigIP.ini):
    ///   [DATA]
    ///   IP         = (preenchido automaticamente pelo programa)
    ///   DESCRICAO  = Nome do cliente/instância (exibido no e-mail)
    ///   AWS_KEY    = Access Key ID da conta AWS
    ///   AWS_SECRET = Secret Access Key da conta AWS
    ///   SG_GROUP   = ID do Security Group a atualizar (ex: sg-0abc123)
    ///   SG_PORT    = Porta TCP a liberar (ex: 1433 para SQL Server)
    ///   EMAIL_FROM = Endereço de e-mail remetente
    ///   EMAIL_TO   = Endereço de e-mail destinatário
    ///   SENDGRID_KEY = API Key do SendGrid
    ///
    /// ⚠️ NUNCA inclua credenciais AWS, SendGrid ou dados de clientes
    ///    diretamente no código-fonte. Use sempre o arquivo .ini de configuração.
    /// </summary>
    class Program
    {
        // Diretório base do executável — onde o swconfigIP.ini deve estar localizado
        public static string pasta = AppDomain.CurrentDomain.BaseDirectory;

        // Nome/descrição do cliente lido do .ini (usado no assunto e corpo do e-mail)
        public static string cliente = "";

        /// <summary>
        /// Registra mensagens de log em arquivo e no console.
        /// O arquivo SWService.txt é criado automaticamente no diretório do executável.
        /// O log só é gravado se o arquivo sentinela "swsql" existir no diretório,
        /// permitindo desabilitar o log simplesmente removendo esse arquivo.
        /// </summary>
        /// <param name="titulo">Título da entrada de log (não usado na gravação, mas útil para filtragem).</param>
        /// <param name="msg">Mensagem a ser registrada.</param>
        public static void SalvaLog(string titulo, string msg)
        {
            try
            {
                // Se o arquivo sentinela "swsql" não existir, log está desabilitado
                if (!File.Exists(pasta + "swsql"))
                    return;

                Console.WriteLine(msg);

                // Cria o arquivo de log se ainda não existir
                if (!File.Exists(pasta + "SWService.txt"))
                {
                    using (var fs = File.Create(pasta + "SWService.txt")) { }
                }

                // Prepend com timestamp no formato dd/MM/yyyy hh:mm:ss
                string entrada = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss") + " - " + msg;
                File.AppendAllText(pasta + "SWService.txt", entrada + Environment.NewLine);
            }
            catch { /* Ignora falhas de I/O no log para não interromper o fluxo principal */ }
        }

        /// <summary>
        /// Processa a resposta da API SendGrid após tentativa de envio.
        /// Lança EmailServiceException se o status não for 200 ou 202.
        /// </summary>
        private static EmailResponse ProcessResponse(Response response)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.Accepted
                || response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                return ToMailResponse(response);
            }

            // Lê o corpo do erro retornado pela API SendGrid para diagnóstico
            var errorResponse = response.Body.ReadAsStringAsync().Result;
            SalvaLog("Erro envio e-mail", errorResponse);
            throw new EmailServiceException(errorResponse, response.StatusCode.ToString());
        }

        /// <summary>
        /// Converte a resposta HTTP da SendGrid em um EmailResponse estruturado,
        /// extraindo o ID único da mensagem do cabeçalho X-Message-Id.
        /// </summary>
        private static EmailResponse ToMailResponse(Response response)
        {
            if (response == null) return null;

            var headers = (HttpHeaders)response.Headers;
            var messageId = headers.GetValues("X-Message-Id").FirstOrDefault();
            return new EmailResponse
            {
                UniqueMessageId = messageId,
                DateSent = DateTime.UtcNow,
            };
        }


        static void Main(string[] args)
        {
            try
            {
                // --- 1. Obtém o IP público atual ------------------------------------------
                // Usa o serviço ipinfo.io que retorna apenas o IP em texto plano
                string ip = new WebClient().DownloadString("https://ipinfo.io/ip").Trim();

                // --- 2. Lê configurações do arquivo .ini ----------------------------------
                // O arquivo swconfigIP.ini deve estar no mesmo diretório que o executável.
                // ⚠️ Este arquivo contém credenciais — adicione-o ao .gitignore e nunca
                //    o comite em repositórios públicos.
                var parser = new FileIniDataParser();
                IniData data = parser.ReadFile(pasta + "swconfigIP.ini");

                // Lê o IP salvo anteriormente e a descrição do cliente
                string ip_antigo = data["DATA"]["IP"].Trim();
                cliente = data["DATA"]["DESCRICAO"].Trim();

                SalvaLog("IP local", $"IP atual: {ip} | IP anterior: {ip_antigo}");

                // --- 3. Verifica se o IP mudou -------------------------------------------
                if (ip == ip_antigo)
                {
                    SalvaLog("IP local", "IP não mudou. Nenhuma ação necessária.");
                    return;
                }

                string erro = "";

                // --- 4. Atualiza o IP no .ini --------------------------------------------
                try
                {
                    data["DATA"]["IP"] = ip;
                    parser.WriteFile(pasta + "swconfigIP.ini", data);
                    SalvaLog("IP local", "Arquivo .ini atualizado com o novo IP.");
                }
                catch (Exception e)
                {
                    SalvaLog("IP local", $"Erro ao salvar .ini: {e.Message}");
                    erro += $" SalvandoIni -> {e.Message}";
                }

                // --- 5. Lê credenciais AWS do .ini ---------------------------------------
                // As credenciais são específicas por instância/cliente e devem estar
                // no .ini, nunca hardcoded no código-fonte.
                string awsKey    = data["DATA"]["AWS_KEY"].Trim();
                string awsSecret = data["DATA"]["AWS_SECRET"].Trim();
                string sgGroupId = data["DATA"]["SG_GROUP"].Trim();
                int    sgPort    = int.Parse(data["DATA"]["SG_PORT"].Trim());

                if (string.IsNullOrEmpty(awsKey) || awsKey == "YOUR_AWS_ACCESS_KEY_ID")
                    throw new InvalidOperationException("AWS_KEY não configurada no swconfigIP.ini.");

                // Cria as credenciais AWS com os valores lidos do arquivo de configuração
                var awsCredentials = new BasicAWSCredentials(awsKey, awsSecret);

                SalvaLog("IP local", "Conectando ao AWS EC2...");

                // --- 6. Revoga a regra antiga do Security Group --------------------------
                // Remove a liberação do IP anterior para fechar o acesso à porta especificada
                AmazonEC2Client ec2Client = new AmazonEC2Client(awsCredentials, Amazon.RegionEndpoint.SAEast1);

                try
                {
                    SalvaLog("IP local", $"Revogando acesso do IP antigo: {ip_antigo}");

                    ec2Client.RevokeSecurityGroupIngress(new RevokeSecurityGroupIngressRequest
                    {
                        GroupId = sgGroupId,
                        IpPermissions = new List<IpPermission>
                        {
                            new IpPermission
                            {
                                FromPort   = sgPort,
                                IpProtocol = "tcp",
                                ToPort     = sgPort,
                                Ipv4Ranges = new List<IpRange>
                                {
                                    new IpRange { CidrIp = ip_antigo + "/32" }
                                }
                            }
                        }
                    });
                }
                catch (Exception e)
                {
                    // Revoke pode falhar se a regra não existir — não é crítico, continua
                    SalvaLog("IP local", $"Aviso no Revoke: {e.Message}");
                    erro += $" Revoke -> {e.Message}";
                }

                // --- 7. Autoriza o novo IP no Security Group ----------------------------
                // Adiciona a regra de ingresso para o IP atual com descrição do cliente
                try
                {
                    SalvaLog("IP local", $"Autorizando novo IP: {ip}");

                    ec2Client.AuthorizeSecurityGroupIngress(new AuthorizeSecurityGroupIngressRequest
                    {
                        GroupId = sgGroupId,
                        IpPermissions = new List<IpPermission>
                        {
                            new IpPermission
                            {
                                FromPort   = sgPort,
                                IpProtocol = "tcp",
                                ToPort     = sgPort,
                                Ipv4Ranges = new List<IpRange>
                                {
                                    new IpRange
                                    {
                                        CidrIp      = ip + "/32",
                                        Description = cliente   // Nome exibido no console AWS
                                    }
                                }
                            }
                        }
                    });
                }
                catch (Exception e)
                {
                    SalvaLog("IP local", $"Erro no Authorize: {e.Message}");
                    erro += $" Authorize -> {e.Message}";
                }

                // --- 8. Mata processo legado caso esteja rodando -------------------------
                // O processo SWAtuDados mantém conexões com o IP antigo — precisamos
                // reiniciá-lo para que reconecte com o novo IP.
                foreach (var p in Process.GetProcessesByName("SWAtuDados.exe"))
                    p.Kill();

                // --- 9. Envia e-mail de notificação via SendGrid -------------------------
                // API Key, remetente e destinatário são lidos do .ini
                string sendgridKey = data["DATA"]["SENDGRID_KEY"].Trim();
                string emailFrom   = data["DATA"]["EMAIL_FROM"].Trim();
                string emailTo     = data["DATA"]["EMAIL_TO"].Trim();

                SalvaLog("IP local", "Preparando e-mail de notificação...");

                var sgClient = new SendGridClient(sendgridKey);

                var msg = MailHelper.CreateSingleEmailToMultipleRecipients(
                    from: new EmailAddress(emailFrom, emailFrom),
                    tos:  new List<EmailAddress> { new EmailAddress(emailTo, emailTo) },
                    subject:          $"IP Atualizado — {cliente}",
                    plainTextContent: $"IP anterior: '{ip_antigo}' | Novo IP: '{ip}'",
                    htmlContent:      $"<b>IP anterior:</b> {ip_antigo}<br><b>Novo IP:</b> {ip}<br><b>Erros:</b> {(string.IsNullOrEmpty(erro) ? "nenhum" : erro)}"
                );

                ProcessResponse(sgClient.SendEmailAsync(msg).Result);
                SalvaLog("IP local", "E-mail enviado com sucesso.");
            }
            catch (Exception e)
            {
                SalvaLog("IP local", $"Erro geral: {e.Message}");
            }
        }
    }
}
