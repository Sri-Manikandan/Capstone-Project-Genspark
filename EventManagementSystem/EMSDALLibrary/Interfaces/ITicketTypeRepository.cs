using EMSModelLibrary.Models;

namespace EMSDALLibrary.Interfaces
{
    public interface ITicketTypeRepository : IRepository<TicketType>
    {
        Task<List<TicketType>> GetByScreeningId(int screeningId);
        Task<List<TicketType>> GetActiveByScreeningId(int screeningId);
        Task<bool> TryDecrementAvailableQuantity(int ticketTypeId);
        Task IncrementAvailableQuantity(int ticketTypeId);
    }
}
