using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace negosuite_api.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public void SendEmail(EmailPayload emailPayload)
        {
            MailMessage mailMessage = new MailMessage();

            emailPayload.Recipients.ForEach(recipient =>
            {
                mailMessage.To.Add(recipient);
            });

            mailMessage.Subject = emailPayload.Subject;
            mailMessage.Body = emailPayload.Message;
            mailMessage.IsBodyHtml = true;
            mailMessage.From = new MailAddress(_config["Smtp:FromEmail"]);

            SmtpClient smtpClient = new SmtpClient();
            smtpClient.Host = _config["Smtp:Host"];
            smtpClient.Port = int.Parse(_config["Smtp:Port"]);
            smtpClient.UseDefaultCredentials = false;
            smtpClient.Credentials = new NetworkCredential(
                _config["Smtp:Username"], _config["Smtp:Password"]);
            smtpClient.EnableSsl = true;
            smtpClient.Send(mailMessage);
        }
    }

    public interface IEmailService
    {
        void SendEmail(EmailPayload emailPayload);
    }

    public class EmailPayload
    {
        public string Subject { get; set; }
        public List<string> Recipients { get; set; }
        public string Message { get; set; }
        public string CompanyName { get; set; }
        public string SenderName { get; set; }
        public string RecipientName { get; set; }
        public string Password { get; set; }
        public string Identifier { get; set; }
        public int? ConfigId { get; set; }
        public int? UserRoleId { get; set; }
        public DateTime? ExpiryDate { get; set; }
    }

}
