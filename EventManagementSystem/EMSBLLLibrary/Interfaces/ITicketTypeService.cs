using EMSModelLibrary.DTOs;

namespace EMSBLLLibrary.Interfaces
{
    public interface ITicketTypeService
    {
        Task<TicketTypeDto> Create(int organizerId, CreateTicketTypeRequest request);
        Task<TicketTypeDto> GetById(int id);
        Task<List<TicketTypeDto>> GetByScreeningId(int screeningId);
        Task<List<TicketTypeDto>> GetActiveByScreeningId(int screeningId);
        Task<TicketTypeDto> Update(int id, int organizerId, UpdateTicketTypeRequest request);
        Task Delete(int id, int organizerId);
    }
}
