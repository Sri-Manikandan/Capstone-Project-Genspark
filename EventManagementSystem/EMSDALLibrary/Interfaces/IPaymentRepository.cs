using EMSModelLibrary.Models;

namespace EMSDALLibrary.Interfaces
{
    public interface IPaymentRepository : IRepository<Payment>
    {
        Task<Payment?> GetByBookingId(int bookingId);
        Task<Payment?> GetByStripePaymentIntentId(string intentId);
    }
}
