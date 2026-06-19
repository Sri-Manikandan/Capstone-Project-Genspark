using Stripe;

namespace EMSBLLLibrary.Interfaces
{
    public interface IStripePaymentIntentClient
    {
        Task<PaymentIntent> GetAsync(string id);
        Task<PaymentIntent> CreateAsync(PaymentIntentCreateOptions options);
    }
}
