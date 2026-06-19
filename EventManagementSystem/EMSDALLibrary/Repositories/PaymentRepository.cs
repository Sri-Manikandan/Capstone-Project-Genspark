using EMSDALLibrary.Contexts;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.Models;
using Microsoft.EntityFrameworkCore;

namespace EMSDALLibrary.Repositories
{
    public class PaymentRepository : AbstractRepository<Payment>, IPaymentRepository
    {
        public PaymentRepository(EventContext context) : base(context) { }

        public async Task<Payment?> GetByBookingId(int bookingId)
        {
            return await _context.Payments.FirstOrDefaultAsync(p => p.BookingId == bookingId);
        }

        public async Task<Payment?> GetByStripePaymentIntentId(string intentId)
        {
            return await _context.Payments.FirstOrDefaultAsync(p => p.StripePaymentIntentId == intentId);
        }
    }
}
