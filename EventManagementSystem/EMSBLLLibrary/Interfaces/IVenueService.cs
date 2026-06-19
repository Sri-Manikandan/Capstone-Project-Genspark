using EMSModelLibrary.DTOs;

namespace EMSBLLLibrary.Interfaces
{
    public interface IVenueService
    {
        Task<VenueDto> Create(CreateVenueRequest request);
        Task<VenueDto> GetById(int id);
        Task<List<VenueDto>> GetAll();
        Task<VenueDto> Update(int id, UpdateVenueRequest request);
        Task Delete(int id);
    }
}
