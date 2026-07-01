using EMSModelLibrary.DTOs;

namespace EMSBLLLibrary.Interfaces
{
    public interface IEventService
    {
        Task<EventDto> Create(int organizerId, CreateEventRequest request);
        Task<EventDto> GetById(int id);
        Task<EventDto?> GetBySlug(string slug);
        Task<List<EventDto>> GetAll();
        Task<PagedResult<EventDto>> Search(EventSearchRequest request);
        Task<List<string>> GetCategories();
        Task<List<string>> GetCities();
        Task<PagedResult<EventDto>> GetByOrganizer(int organizerId, int page, int pageSize);
        Task<EventDto> Update(int id, int organizerId, UpdateEventRequest request);
        Task Delete(int id, int requesterId, bool isAdmin = false);
        Task<EventDto> Submit(int id, int organizerId, bool isAdmin = false);
        Task<EventDto> Cancel(int id, int requesterId, bool isAdmin = false);

        // Admin-only
        Task<List<EventDto>> GetPendingApproval();
        Task<EventDto> AdminApprove(int id);
        Task<EventDto> AdminReject(int id, string? reason);
    }
}
