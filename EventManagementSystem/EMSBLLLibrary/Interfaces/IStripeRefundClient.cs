using Stripe;

namespace EMSBLLLibrary.Interfaces
{
    public interface IStripeRefundClient
    {
        Task<Refund> CreateAsync(RefundCreateOptions options);
    }
}
