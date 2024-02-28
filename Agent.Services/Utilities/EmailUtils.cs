using System.Net.Mail;
using System.Net;
using System.Net.Mime;

namespace Agent.Services
{
    public class EmailUtils
    {
        public static async Task SendEmail(string toEmailAddress, string subject, string htmlBody, string attachmentFilePath = null)
        {
            var fromAddress = new MailAddress("gregsemple2003@yahoo.com", "Biz Dev Agent");
            var toAddress = new MailAddress(toEmailAddress, "Greg Semple");
            const string fromPassword = "yzhpsythclseyslt";

            var smtp = new SmtpClient
            {
                Host = "smtp.mail.yahoo.com",
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
            };

            using (var message = new MailMessage(fromAddress, toAddress)
            {
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            })
            {
                // Check if attachment file is provided and exists
                if (!string.IsNullOrEmpty(attachmentFilePath) && File.Exists(attachmentFilePath))
                {
                    // Create attachment and add it to the message
                    var attachment = new Attachment(attachmentFilePath, MediaTypeNames.Text.Plain);
                    message.Attachments.Add(attachment);
                }

                await smtp.SendMailAsync(message);
            }
        }
    }
}
