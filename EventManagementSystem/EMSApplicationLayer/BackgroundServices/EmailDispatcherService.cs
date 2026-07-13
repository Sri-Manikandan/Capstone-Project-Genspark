using System.Text.Json;
using EMSBLLLibrary.Constants;
using EMSBLLLibrary.Emails;
using EMSBLLLibrary.Interfaces;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.DTOs;
using EMSModelLibrary.Models;

namespace EMSApplicationLayer.BackgroundServices
{
    // Renders and sends one batch of queued email. Separated from the hosted service
    // so a single tick can be driven directly in tests.
    public class EmailDispatcher
    {
        private readonly IEmailOutboxRepository _outboxRepo;
        private readonly IEmailSender _sender;
        private readonly IEmailTemplateRenderer _renderer;
        private readonly ILogger<EmailDispatcher> _logger;

        public const int BatchSize = 50;
        public const int MaxAttempts = 5;

        // Attempt 1 waits 1m, attempt 5 waits 6h. After the fifth the row is Failed.
        private static readonly TimeSpan[] Backoff =
        {
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(15),
            TimeSpan.FromHours(1),
            TimeSpan.FromHours(6)
        };

        public EmailDispatcher(
            IEmailOutboxRepository outboxRepo,
            IEmailSender sender,
            IEmailTemplateRenderer renderer,
            ILogger<EmailDispatcher> logger)
        {
            _outboxRepo = outboxRepo;
            _sender = sender;
            _renderer = renderer;
            _logger = logger;
        }

        public async Task DispatchOnce(CancellationToken ct)
        {
            var batch = await _outboxRepo.GetPendingBatch(BatchSize, DateTime.UtcNow);

            foreach (var row in batch)
            {
                if (ct.IsCancellationRequested)
                    return;

                await Dispatch(row);
                await _outboxRepo.Update(row);
            }

            if (batch.Count > 0)
                _logger.LogInformation("Email dispatcher processed {Count} messages.", batch.Count);
        }

        private async Task Dispatch(EmailOutbox row)
        {
            var tokens = JsonSerializer.Deserialize<Dictionary<string, string>>(row.PayloadJson)
                         ?? new Dictionary<string, string>();

            // QrBase64 is an attachment, never body copy. Pull it out before rendering
            // so it can't be substituted into the HTML as a base64 blob.
            var attachments = new List<EmailAttachment>();
            if (tokens.Remove("QrBase64", out var qrBase64) && !string.IsNullOrWhiteSpace(qrBase64))
            {
                var reference = tokens.GetValueOrDefault("BookingReference", row.Id.ToString());
                attachments.Add(new EmailAttachment($"ticket-{reference}.png", qrBase64));
            }

            string html;
            try
            {
                html = _renderer.Render(row.TemplateKey, tokens);
            }
            catch (Exception ex)
            {
                // A template that won't render will never render. No point retrying.
                row.Status = EmailStatus.Failed;
                row.Attempts++;
                row.LastError = ex.Message;
                _logger.LogError(ex, "Email {Id} failed to render template {Template}.", row.Id, row.TemplateKey);
                return;
            }

            var result = await _sender.Send(row.ToEmail, row.ToName, row.Subject, html, attachments);
            row.Attempts++;

            if (result.Success)
            {
                row.Status = EmailStatus.Sent;
                row.SentAt = DateTime.UtcNow;
                row.ProviderMessageId = result.MessageId;
                row.LastError = null;
                return;
            }

            row.LastError = result.Error;

            if (!result.IsTransient || row.Attempts >= MaxAttempts)
            {
                row.Status = EmailStatus.Failed;
                _logger.LogError("Email {Id} to {To} permanently failed after {Attempts} attempt(s): {Error}",
                    row.Id, row.ToEmail, row.Attempts, result.Error);
                return;
            }

            row.SendAfter = DateTime.UtcNow.Add(Backoff[Math.Min(row.Attempts - 1, Backoff.Length - 1)]);
            _logger.LogWarning("Email {Id} attempt {Attempts} failed, retrying after {SendAfter}: {Error}",
                row.Id, row.Attempts, row.SendAfter, result.Error);
        }
    }

    public class EmailDispatcherService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<EmailDispatcherService> _logger;
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

        public EmailDispatcherService(IServiceScopeFactory scopeFactory, ILogger<EmailDispatcherService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var dispatcher = scope.ServiceProvider.GetRequiredService<EmailDispatcher>();
                    await dispatcher.DispatchOnce(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during email dispatch sweep.");
                }

                await Task.Delay(Interval, stoppingToken);
            }
        }
    }
}
