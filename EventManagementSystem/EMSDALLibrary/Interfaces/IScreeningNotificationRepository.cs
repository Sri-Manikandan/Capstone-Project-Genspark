using EMSModelLibrary.Models;

namespace EMSDALLibrary.Interfaces
{
    public interface IScreeningNotificationRepository : IRepository<ScreeningNotification>
    {
        Task<ScreeningNotification?> GetByScreeningAndUserId(int screeningId, int userId);
        Task<List<ScreeningNotification>> GetActiveNotificationsByScreeningId(int screeningId);
    }
}
