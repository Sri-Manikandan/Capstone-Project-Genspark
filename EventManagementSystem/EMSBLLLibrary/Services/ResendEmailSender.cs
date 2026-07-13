using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EMSBLLLibrary.Interfaces;
using EMSModelLibrary.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EMSBLLLibrary.Services
{
    // Calls Resend's REST API directly. The contract is a single POST with three
    // required fields, so a vendor SDK would add a dependency without adding value.
    public class ResendEmailSender : IEmailSender
    {
        private readonly HttpClient _http;
        private readonly ILogger<ResendEmailSender> _logger;
        private readonly bool _enabled;
        private readonly string _from;

        public ResendEmailSender(HttpClient http, IConfiguration config, ILogger<ResendEmailSender> logger)
        {
            _http = http;
            _logger = logger;
            _enabled = bool.TryParse(config["Email:Enabled"], out var enabled) && enabled;

            var fromAddress = config["Email:FromAddress"] ?? "onboarding@resend.dev";
            var fromName = config["Email:FromName"] ?? "EventHub";
            _from = $"{fromName} <{fromAddress}>";
        }

        public async Task<EmailSendResult> Send(string toEmail, string toName, string subject, string html, List<EmailAttachment> attachments)
        {
            // Dev and test mode: log the intent, send nothing. Keeps DataSeeder from
            // spraying real people and keeps local runs from needing an API key.
            if (!_enabled)
            {
                _logger.LogInformation("Email disabled. Would send '{Subject}' to {Email}.", subject, toEmail);
                return EmailSendResult.Ok("disabled");
            }

            var payload = new Dictionary<string, object>
            {
                ["from"] = _from,
                ["to"] = new[] { toEmail },
                ["subject"] = subject,
                ["html"] = html
            };

            if (attachments.Count > 0)
            {
                payload["attachments"] = attachments
                    .Select(a => new { filename = a.FileName, content = a.ContentBase64 })
                    .ToArray();
            }

            try
            {
                var response = await _http.PostAsJsonAsync("emails", payload);
                var body = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    string? id = null;
                    try
                    {
                        id = JsonDocument.Parse(body).RootElement.GetProperty("id").GetString();
                    }
                    catch (Exception)
                    {
                        // A send that succeeded but whose id we couldn't parse is still a send.
                    }
                    return EmailSendResult.Ok(id);
                }

                // 429 and 5xx are worth retrying. Other 4xx (bad address, bad key,
                // rejected payload) will fail identically forever.
                var transient = response.StatusCode == HttpStatusCode.TooManyRequests
                                || (int)response.StatusCode >= 500;

                return transient
                    ? EmailSendResult.Transient($"{(int)response.StatusCode}: {body}")
                    : EmailSendResult.Permanent($"{(int)response.StatusCode}: {body}");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                return EmailSendResult.Transient(ex.Message);
            }
        }
    }
}
