using EMSModelLibrary.DTOs;

namespace EMSBLLLibrary.Interfaces
{
    public interface IBookingService
    {
        Task<BookingDto> Create(int userId, CreateBookingRequest request);
        Task<BookingDto> GetById(int id, int userId);
        Task<BookingDto?> GetByReference(string reference, int userId);
        Task<PagedResult<BookingDto>> GetByUserId(int userId, BookingQueryRequest request);
        Task<PagedResult<BookingDto>> GetByEventId(int eventId, BookingQueryRequest request);
        Task Cancel(int id, int userId);
        Task<bool> ValidateQr(ValidateQrRequest request);
    }
}
