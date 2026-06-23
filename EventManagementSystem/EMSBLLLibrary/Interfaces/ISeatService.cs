using EMSModelLibrary.DTOs;

namespace EMSBLLLibrary.Interfaces
{
    public interface ISeatService
    {
        Task<SeatDto> Create(CreateSeatRequest request);
        Task<List<SeatDto>> BulkCreate(BulkCreateSeatsRequest request);
        Task<List<SeatDto>> GetByVenueId(int venueId);
        Task<List<SeatDto>> GetAvailableByEventId(int eventId);
        Task Delete(int id);
        Task<List<SeatDto>> SetScreenSeats(SetScreenSeatsRequest request);
        Task DeleteScreen(int venueId, string screen);
    }
}
