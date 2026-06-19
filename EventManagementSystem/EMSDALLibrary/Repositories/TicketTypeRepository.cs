using EMSDALLibrary.Contexts;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.Models;
using Microsoft.EntityFrameworkCore;

namespace EMSDALLibrary.Repositories
{
    public class TicketTypeRepository : AbstractRepository<TicketType>, ITicketTypeRepository
    {
        public TicketTypeRepository(EventContext context) : base(context) { }

        public async Task<List<TicketType>> GetByEventId(int eventId)
        {
            return await _context.TicketTypes.Where(t => t.EventId == eventId).ToListAsync();
        }

        public async Task<List<TicketType>> GetActiveByEventId(int eventId)
        {
            var now = DateTime.UtcNow;
            return await _context.TicketTypes
                .Where(t => t.EventId == eventId && t.IsActive && t.SaleStart <= now && t.SaleEnd >= now)
                .ToListAsync();
        }

        public async Task<bool> TryDecrementAvailableQuantity(int ticketTypeId)
        {
            var rows = await _context.TicketTypes
                .Where(t => t.Id == ticketTypeId && t.AvailableQuantity > 0)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.AvailableQuantity, t => t.AvailableQuantity - 1));
            return rows > 0;
        }

        public async Task IncrementAvailableQuantity(int ticketTypeId)
        {
            await _context.TicketTypes
                .Where(t => t.Id == ticketTypeId)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.AvailableQuantity, t => t.AvailableQuantity + 1));
        }
    }
}
