using EMSBLLLibrary.Interfaces;
using Stripe;

namespace EMSBLLLibrary.Services
{
    public class StripeRefundClient : IStripeRefundClient
    {
        private readonly RefundService _service = new();

        public Task<Refund> CreateAsync(RefundCreateOptions options) => _service.CreateAsync(options);
    }
}
