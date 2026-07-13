using System.Text.Json;
using EMSBLLLibrary.Constants;
using EMSBLLLibrary.Interfaces;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.Models;
using Microsoft.Extensions.Configuration;

namespace EMSBLLLibrary.Services
{
    public class EmailQueue : IEmailQueue
    {
        private readonly IEmailOutboxRepository _outboxRepo;
        private readonly string _appUrl;

        public EmailQueue(IEmailOutboxRepository outboxRepo, IConfiguration config)
        {
            _outboxRepo = outboxRepo;
            _appUrl = config["Email:AppBaseUrl"] ?? "http://localhost:4200";
        }

        public async Task Enqueue(string toEmail, string toName, string templateKey, string subject,
            IDictionary<string, string> tokens, string? dedupeKey = null, DateTime? sendAfter = null)
        {
            try
            {
                await _outboxRepo.Add(Build(new QueuedEmail(toEmail, toName, templateKey, subject, tokens, dedupeKey, sendAfter)));
            }
            catch (Exception)
            {
                // Swallowed by design. The caller is mid-booking or mid-approval; a
                // failure to queue mail must not roll that back. The row is lost, not
                // the booking.
            }
        }

        public async Task EnqueueMany(List<QueuedEmail> emails)
        {
            if (emails.Count == 0)
                return;

            try
            {
                await _outboxRepo.AddMany(emails.Select(Build).ToList());
            }
            catch (Exception)
            {
                // Same reasoning as Enqueue.
            }
        }

        private EmailOutbox Build(QueuedEmail email)
        {
            // Every template's layout needs AppUrl and Year. Supplying them here means
            // no call site can forget one and leak a literal {{AppUrl}} to a user.
            var tokens = new Dictionary<string, string>(email.Tokens)
            {
                ["AppUrl"] = _appUrl,
                ["Year"] = DateTime.UtcNow.Year.ToString()
            };

            return new EmailOutbox
            {
                ToEmail = email.ToEmail,
                ToName = email.ToName,
                TemplateKey = email.TemplateKey,
                Subject = email.Subject,
                PayloadJson = JsonSerializer.Serialize(tokens),
                Status = EmailStatus.Pending,
                DedupeKey = email.DedupeKey,
                SendAfter = email.SendAfter ?? DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}
