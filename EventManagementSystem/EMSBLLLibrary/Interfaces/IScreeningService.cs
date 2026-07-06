using EMSModelLibrary.DTOs;

namespace EMSBLLLibrary.Interfaces
{
    public interface IScreeningService
    {
        Task<List<ScreeningDto>> GetByEventId(int eventId);
        Task<ScreeningDto> GetById(int id);
        Task<ScreeningDto> Create(int organizerId, CreateScreeningRequest request);
        Task<ScreeningDto> Update(int id, int organizerId, UpdateScreeningRequest request);
        Task Delete(int id, int organizerId);
    }
}
