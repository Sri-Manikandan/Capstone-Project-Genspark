using EMSModelLibrary.Models;

namespace EMSDALLibrary.Interfaces
{
    public interface ITicketTypeRepository : IRepository<TicketType>
    {
        Task<List<TicketType>> GetByEventId(int eventId);
        Task<List<TicketType>> GetActiveByEventId(int eventId);
        Task<bool> TryDecrementAvailableQuantity(int ticketTypeId);
        Task IncrementAvailableQuantity(int ticketTypeId);
    }
}
