using EMSModelLibrary.DTOs;

namespace EMSBLLLibrary.Interfaces
{
    public interface ISeatReservationService
    {
        Task<SeatReservationDto> Reserve(int userId, ReserveSeatRequest request);
        Task Release(int reservationId, int userId);
    }
}
