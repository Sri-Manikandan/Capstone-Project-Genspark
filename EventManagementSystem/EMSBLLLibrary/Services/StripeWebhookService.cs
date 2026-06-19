using EMSBLLLibrary.Interfaces;
using EMSModelLibrary.Exceptions;
using EMSDALLibrary.Interfaces;
using Microsoft.Extensions.Configuration;
using Stripe;

namespace EMSBLLLibrary.Services
{
    public class StripeWebhookService : IStripeWebhookService
    {
        private readonly IPaymentRepository _paymentRepo;
        private readonly IBookingRepository _bookingRepo;
        private readonly string _webhookSecret;

        public StripeWebhookService(IPaymentRepository paymentRepo, IBookingRepository bookingRepo, IConfiguration config)
        {
            _paymentRepo = paymentRepo;
            _bookingRepo = bookingRepo;
            _webhookSecret = config["Stripe:WebhookSecret"] ?? throw new Exception("Stripe webhook secret not configured.");
        }

        protected virtual Stripe.Event ConstructStripeEvent(string payload, string signature) =>
            EventUtility.ConstructEvent(payload, signature, _webhookSecret, throwOnApiVersionMismatch: false);

        public async Task ProcessAsync(string payload, string stripeSignature)
        {
            Stripe.Event stripeEvent;
            try
            {
                stripeEvent = ConstructStripeEvent(payload, stripeSignature);
            }
            catch (StripeException ex)
            {
                throw new ValidationException("Invalid Stripe webhook Signature: " + ex.Message);
            }

            if (stripeEvent.Type == EventTypes.PaymentIntentSucceeded)
            {
                if(stripeEvent.Data.Object is PaymentIntent intent)
                {
                    await HandlePaymentSucceeded(intent);
                }
            }else if (stripeEvent.Type == EventTypes.PaymentIntentPaymentFailed)
            {
                if(stripeEvent.Data.Object is PaymentIntent intent)
                {
                    await HandlePaymentFailed(intent);
                }
             }
        }

        private async Task HandlePaymentSucceeded(PaymentIntent intent)
        {
            var payment = await _paymentRepo.GetByStripePaymentIntentId(intent.Id);
            if (payment == null || payment.Status == "Succeeded") return; // No matching payment or already succeeded, ignore

            payment.Status = "Succeeded";
            payment.PaidAt = DateTime.UtcNow;
            payment.StripeChargeId = intent.LatestChargeId?? string.Empty;
            await _paymentRepo.Update(payment);

            // booking.BookingStatus = "Confirmed";
            // await _bookingRepo.Update(booking);

            var booking = await _bookingRepo.GetById(payment.BookingId);
            if(booking != null && booking.BookingStatus != "Confirmed")
            {
                booking.BookingStatus = "Confirmed";
                booking.UpdatedAt = DateTime.UtcNow;
                await _bookingRepo.Update(booking);
            }
        }

        private async Task HandlePaymentFailed(PaymentIntent intent)
        {
            var payment = await _paymentRepo.GetByStripePaymentIntentId(intent.Id);
            if (payment == null) return; // No matching payment, ignore

            payment.Status = "Failed";
            await _paymentRepo.Update(payment);
        }
    }
} 