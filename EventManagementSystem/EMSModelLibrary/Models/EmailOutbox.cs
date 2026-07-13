namespace EMSModelLibrary.Models
{
    // A queued email. Rows are inserted in the same transaction as the domain
    // change they describe; EmailDispatcherService renders and sends them.
    public class EmailOutbox
    {
        public int Id { get; set; }
        public string ToEmail { get; set; } = string.Empty;
        public string ToName { get; set; } = string.Empty;
        public string TemplateKey { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = "{}";
        public string Subject { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";
        public int Attempts { get; set; }
        public DateTime SendAfter { get; set; }
        public string? LastError { get; set; }
        public string? ProviderMessageId { get; set; }

        // Unique when present. Makes re-enqueueing the same logical email a no-op,
        // which is what keeps the reminder sweeper safe across restarts.
        public string? DedupeKey { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? SentAt { get; set; }

        public EmailOutbox()
        {
            CreatedAt = DateTime.UtcNow;
            SendAfter = DateTime.UtcNow;
        }
    }
}
