using EMSModelLibrary.DTOs;

namespace EMSBLLLibrary.Interfaces
{
    public interface IPaymentService
    {
        Task<PaymentInitiateDto> Initiate(int userId, InitiatePaymentRequest request);
        Task<PaymentDto> Confirm(int userId, ConfirmPaymentRequest request);
        Task<PaymentDto?> GetByBookingId(int bookingId, int userId);
    }
}
