using EMSModelLibrary.Models;

namespace EMSDALLibrary.Interfaces
{
    public interface IEventRepository : IRepository<Event>
    {
        Task<(List<Event> Items, int TotalCount)> GetByOrganizerId(int organizerId, int page, int pageSize);
        Task<List<Event>> GetByStatus(string status);
        Task<List<Event>> GetByCategory(string category);
        Task<List<string>> GetCategories(string status);
        Task<List<string>> GetCities(string status);
        Task<Event?> GetBySlug(string slug);
        Task<(List<Event> Items, int TotalCount)> Search(
            string? query, string? category, string? city, string? status,
            DateTime? startFrom, DateTime? startTo,
            string? sortBy, string? sortOrder,
            int page, int pageSize);
    }
}
