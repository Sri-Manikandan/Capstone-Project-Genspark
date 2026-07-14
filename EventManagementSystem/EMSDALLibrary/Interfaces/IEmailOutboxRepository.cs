using EMSModelLibrary.Models;

namespace EMSDALLibrary.Interfaces
{
    public interface IEmailOutboxRepository : IRepository<EmailOutbox>
    {
        Task<List<EmailOutbox>> GetPendingBatch(int limit, DateTime now);
        Task AddMany(List<EmailOutbox> messages);
    }
}
