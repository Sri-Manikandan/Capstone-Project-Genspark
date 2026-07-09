namespace EMSBLLLibrary.Interfaces
{
    public interface IScreeningNotificationService
    {
        Task Subscribe(int screeningId, int userId);
        Task NotifyAvailableTickets(int screeningId);
    }
}
