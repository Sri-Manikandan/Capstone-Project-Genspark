using EMSDALLibrary.Contexts;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.Models;

namespace EMSDALLibrary.Repositories
{
    public class VenueRepository : AbstractRepository<Venue>, IVenueRepository
    {
        public VenueRepository(EventContext context) : base(context) { }
    }
}
