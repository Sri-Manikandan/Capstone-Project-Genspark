namespace EMSBLLLibrary.Interfaces
{
    public record QueuedEmail(
        string ToEmail,
        string ToName,
        string TemplateKey,
        string Subject,
        IDictionary<string, string> Tokens,
        string? DedupeKey,
        DateTime? SendAfter);

    // What domain services depend on. They never see the vendor, never await a
    // network call, and need exactly one mock in tests.
    public interface IEmailQueue
    {
        Task Enqueue(string toEmail, string toName, string templateKey, string subject,
            IDictionary<string, string> tokens, string? dedupeKey = null, DateTime? sendAfter = null);

        Task EnqueueMany(List<QueuedEmail> emails);
    }
}
