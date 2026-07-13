using System.Text;
using EMSBLLLibrary.Constants;
using EMSBLLLibrary.Helpers;
using EMSBLLLibrary.Interfaces;
using EMSModelLibrary.Exceptions;
using EMSModelLibrary.Models;
using EMSDALLibrary.Interfaces;
using Microsoft.Extensions.Configuration;
using Stripe;

namespace EMSBLLLibrary.Services
{
    public class StripeWebhookService : IStripeWebhookService
    {
        private readonly IPaymentRepository _paymentRepo;
        private readonly IBookingRepository _bookingRepo;
        private readonly IUserRepository _userRepo;
        private readonly IBookingItemRepository _bookingItemRepo;
        private readonly IScreeningRepository _screeningRepo;
        private readonly IEventRepository _eventRepo;
        private readonly IVenueRepository _venueRepo;
        private readonly ITicketTypeRepository _ticketTypeRepo;
        private readonly ISeatRepository _seatRepo;
        private readonly IEmailQueue _emailQueue;
        private readonly string _webhookSecret;

        public StripeWebhookService(
            IPaymentRepository paymentRepo,
            IBookingRepository bookingRepo,
            IUserRepository userRepo,
            IBookingItemRepository bookingItemRepo,
            IScreeningRepository screeningRepo,
            IEventRepository eventRepo,
            IVenueRepository venueRepo,
            ITicketTypeRepository ticketTypeRepo,
            ISeatRepository seatRepo,
            IEmailQueue emailQueue,
            IConfiguration config)
        {
            _paymentRepo = paymentRepo;
            _bookingRepo = bookingRepo;
            _userRepo = userRepo;
            _bookingItemRepo = bookingItemRepo;
            _screeningRepo = screeningRepo;
            _eventRepo = eventRepo;
            _venueRepo = venueRepo;
            _ticketTypeRepo = ticketTypeRepo;
            _seatRepo = seatRepo;
            _emailQueue = emailQueue;
            _webhookSecret = config["Stripe:WebhookSecret"]
                ?? throw new Exception("Stripe:WebhookSecret is not configured. Set the Stripe__WebhookSecret environment variable.");
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

            var booking = await _bookingRepo.GetById(payment.BookingId);
            if(booking != null && booking.BookingStatus != "Confirmed")
            {
                booking.BookingStatus = "Confirmed";
                booking.UpdatedAt = DateTime.UtcNow;
                await _bookingRepo.Update(booking);

                // Inside the not-already-Confirmed guard on purpose: Stripe retries
                // webhooks, and sending someone their ticket twice is a support call.
                await EnqueueBookingConfirmed(booking);
            }
        }

        private async Task HandlePaymentFailed(PaymentIntent intent)
        {
            var payment = await _paymentRepo.GetByStripePaymentIntentId(intent.Id);
            if (payment == null) return; // No matching payment, ignore

            payment.Status = "Failed";
            await _paymentRepo.Update(payment);

            var booking = await _bookingRepo.GetById(payment.BookingId);
            if (booking == null) return;

            var user = await _userRepo.GetById(booking.UserId);
            if (user == null) return;

            var screening = await _screeningRepo.GetById(booking.ScreeningId);
            var ev = screening == null ? null : await _eventRepo.GetById(screening.EventId);

            await _emailQueue.Enqueue(user.Email, user.Name, EmailTemplateKey.PaymentFailed,
                "Your payment didn't go through",
                new Dictionary<string, string>
                {
                    ["Name"] = user.Name,
                    ["EventTitle"] = ev?.Title ?? "your event",
                    ["BookingReference"] = booking.BookingReference
                });
        }

        private async Task EnqueueBookingConfirmed(Booking booking)
        {
            var user = await _userRepo.GetById(booking.UserId);
            if (user == null) return;

            var screening = await _screeningRepo.GetById(booking.ScreeningId);
            var ev = screening == null ? null : await _eventRepo.GetById(screening.EventId);
            var venue = ev == null ? null : await _venueRepo.GetById(ev.VenueId);
            var items = await _bookingItemRepo.GetByBookingId(booking.Id);

            // Token substitution cannot loop, so the item rows are built here and passed
            // to the template as one pre-rendered token.
            var rows = new StringBuilder();
            foreach (var item in items)
            {
                var ticketType = await _ticketTypeRepo.GetById(item.TicketTypeId);
                var seat = await _seatRepo.GetById(item.SeatId);
                var seatLabel = seat == null ? "-" : $"{seat.Row}{seat.SeatNumber}";

                rows.Append("<tr>")
                    .Append($"<td style=\"padding:8px 0;border-bottom:1px solid #262a33;color:#c8ccd4;\">{ticketType?.Name ?? "Ticket"}</td>")
                    .Append($"<td style=\"padding:8px 0;border-bottom:1px solid #262a33;color:#c8ccd4;\">{seatLabel}</td>")
                    .Append($"<td align=\"right\" style=\"padding:8px 0;border-bottom:1px solid #262a33;color:#c8ccd4;\">&#8377;{item.UnitPrice:0.00}</td>")
                    .Append("</tr>");
            }

            await _emailQueue.Enqueue(user.Email, user.Name, EmailTemplateKey.BookingConfirmed,
                $"Your tickets for {ev?.Title ?? "your event"}",
                new Dictionary<string, string>
                {
                    ["Name"] = user.Name,
                    ["EventTitle"] = ev?.Title ?? "Your event",
                    ["VenueName"] = venue?.Name ?? "-",
                    ["ScreeningTime"] = screening == null
                        ? "-"
                        : TimeHelper.UtcToIst(screening.StartTime).ToString("dddd, d MMM yyyy 'at' h:mm tt"),
                    ["BookingReference"] = booking.BookingReference,
                    ["TotalAmount"] = booking.TotalAmount.ToString("0.00"),
                    ["ItemsHtml"] = rows.ToString(),

                    // Stripped out by the dispatcher and turned into a PNG attachment.
                    ["QrBase64"] = booking.QrCode
                });
        }
    }
}
