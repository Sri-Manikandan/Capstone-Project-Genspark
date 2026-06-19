using EMSBLLLibrary.Interfaces;
using Stripe;

namespace EMSBLLLibrary.Services
{
    public class StripePaymentIntentClient : IStripePaymentIntentClient
    {
        private readonly PaymentIntentService _service = new();

        public Task<PaymentIntent> GetAsync(string id) => _service.GetAsync(id);
        public Task<PaymentIntent> CreateAsync(PaymentIntentCreateOptions options) => _service.CreateAsync(options);
    }
}
