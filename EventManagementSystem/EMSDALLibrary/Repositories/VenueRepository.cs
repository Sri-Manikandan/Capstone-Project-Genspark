using EMSDALLibrary.Contexts;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.Models;
using Microsoft.EntityFrameworkCore;

namespace EMSDALLibrary.Repositories
{
    public class VenueRepository : AbstractRepository<Venue>, IVenueRepository
    {
        public VenueRepository(EventContext context) : base(context) { }

        public async Task<List<Venue>> GetByCity(string city)
        {
            return await _context.Venues.Where(v => v.City == city).ToListAsync();
        }
    }
}
