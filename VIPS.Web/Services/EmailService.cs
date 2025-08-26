using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System;

namespace VIPS.Web.Services
{
    public class EmailService
    {
        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly string _smtpUser;
        private readonly string _smtpPass;
        private readonly string _fromEmail;
        private readonly string _fromName;

        public EmailService(string smtpHost, int smtpPort, string smtpUser, string smtpPass, string fromEmail, string fromName)
        {
            _smtpHost = smtpHost;
            _smtpPort = smtpPort;
            _smtpUser = smtpUser;
            _smtpPass = smtpPass;
            _fromEmail = fromEmail;
            _fromName = fromName;
        }

        public async Task<bool> EnviarCorreo(string correoDestino, string asunto, string cuerpoHtml)
        {
            try
            {
                using var message = new MailMessage();
                message.From = new MailAddress(_fromEmail, _fromName);
                message.To.Add(new MailAddress(correoDestino));
                message.Subject = asunto;
                message.Body = cuerpoHtml;
                message.IsBodyHtml = true;

                using var client = new SmtpClient(_smtpHost, _smtpPort)
                {
                    Credentials = new NetworkCredential(_smtpUser, _smtpPass),
                    EnableSsl = true // usar SSL/TLS
                };

                await client.SendMailAsync(message);
                return true;
            }
            catch (Exception ex)
            {
                // Aquí podés registrar el error en log
                Console.WriteLine($"Error enviando correo: {ex.Message}");
                return false;
            }
        }
    }
}
