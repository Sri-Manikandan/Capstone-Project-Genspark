using EMSModelLibrary.Models;

namespace EMSDALLibrary.Interfaces
{
    public interface IVenueRepository : IRepository<Venue>
    {
        Task<List<Venue>> GetByCity(string city);
    }
}
