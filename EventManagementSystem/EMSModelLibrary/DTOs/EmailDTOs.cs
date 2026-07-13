namespace EMSModelLibrary.DTOs
{
    public record EmailAttachment(string FileName, string ContentBase64);

    // IsTransient decides whether the dispatcher retries. A bad address is permanent;
    // a 500 or a rate limit is not.
    public record EmailSendResult(bool Success, bool IsTransient, string? MessageId, string? Error)
    {
        public static EmailSendResult Ok(string? messageId) => new(true, false, messageId, null);
        public static EmailSendResult Transient(string error) => new(false, true, null, error);
        public static EmailSendResult Permanent(string error) => new(false, false, null, error);
    }

    // Projection used for bulk fan-out to everyone holding a ticket.
    public class TicketHolderDto
    {
        public int BookingId { get; set; }
        public string BookingReference { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public int ScreeningId { get; set; }
        public DateTime ScreeningStartTime { get; set; }
    }
}
