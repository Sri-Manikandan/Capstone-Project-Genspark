using EMSModelLibrary.DTOs;

namespace EMSBLLLibrary.Interfaces
{
    // The only boundary the email vendor is visible through.
    public interface IEmailSender
    {
        Task<EmailSendResult> Send(string toEmail, string toName, string subject, string html, List<EmailAttachment> attachments);
    }
}
