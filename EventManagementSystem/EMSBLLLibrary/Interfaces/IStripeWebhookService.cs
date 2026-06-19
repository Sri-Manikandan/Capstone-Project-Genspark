namespace EMSBLLLibrary.Interfaces
{
    public interface IStripeWebhookService
    {
        Task ProcessAsync(string payload, string stripeSignature);
    }
}