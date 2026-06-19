using AutoMapper;
using EMSModelLibrary.DTOs;
using EMSBLLLibrary.Interfaces;
using EMSModelLibrary.Exceptions;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.Models;
using Stripe;

namespace EMSBLLLibrary.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IPaymentRepository _paymentRepo;
        private readonly IBookingRepository _bookingRepo;
        private readonly IStripePaymentIntentClient _stripeClient;
        private readonly IMapper _mapper;

        public PaymentService(
            IPaymentRepository paymentRepo,
            IBookingRepository bookingRepo,
            IStripePaymentIntentClient stripeClient,
            IMapper mapper)
        {
            _paymentRepo = paymentRepo;
            _bookingRepo = bookingRepo;
            _stripeClient = stripeClient;
            _mapper = mapper;
        }

        public async Task<PaymentInitiateDto> Initiate(int userId, InitiatePaymentRequest request)
        {
            var booking = await _bookingRepo.GetById(request.BookingId)
                ?? throw new NotFoundException($"Booking {request.BookingId} not found.");

            if (booking.UserId != userId)
                throw new UnauthorizedException("Not authorized to pay for this booking.");

            if (booking.BookingStatus == "Confirmed")
                throw new ValidationException("Booking is already paid.");

            if (booking.BookingStatus == "Cancelled")
                throw new ValidationException("Cannot pay for a cancelled booking.");

            if (booking.BookingStatus == "Pending" && booking.ExpiresAt < DateTime.UtcNow)
                throw new ValidationException("This booking has expired. Please create a new booking.");

            var existing = await _paymentRepo.GetByBookingId(request.BookingId);
            if (existing != null)
            {
                var existingIntent = await _stripeClient.GetAsync(existing.StripePaymentIntentId);
                return new PaymentInitiateDto
                {
                    Id = existing.Id,
                    BookingId = existing.BookingId,
                    StripePaymentIntentId = existing.StripePaymentIntentId,
                    Amount = existing.Amount,
                    Currency = existing.Currency,
                    Status = existing.Status,
                    PaidAt = existing.PaidAt,
                    CreatedAt = existing.CreatedAt,
                    ClientSecret = existingIntent.ClientSecret
                };
            }

            var options = new PaymentIntentCreateOptions
            {
                Amount = (long)(booking.TotalAmount * 100),
                Currency = request.Currency,
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true,
                    AllowRedirects = "never"
                },
                Metadata = new Dictionary<string, string>
                {
                    { "bookingId", request.BookingId.ToString() }
                }
            };
            var intent = await _stripeClient.CreateAsync(options);

            var payment = new Payment
            {
                BookingId = request.BookingId,
                StripePaymentIntentId = intent.Id,
                StripeChargeId = string.Empty,
                StripeCustomerId = string.Empty,
                Amount = booking.TotalAmount,
                Currency = request.Currency,
                Status = "Pending"
            };
            await _paymentRepo.Add(payment);

            return new PaymentInitiateDto
            {
                Id = payment.Id,
                BookingId = payment.BookingId,
                StripePaymentIntentId = payment.StripePaymentIntentId,
                Amount = payment.Amount,
                Currency = payment.Currency,
                Status = payment.Status,
                PaidAt = payment.PaidAt,
                CreatedAt = payment.CreatedAt,
                ClientSecret = intent.ClientSecret
            };
        }

        public async Task<PaymentDto> Confirm(int userId, ConfirmPaymentRequest request)
        {
            var intent = await _stripeClient.GetAsync(request.StripePaymentIntentId);

            if (intent.Status != "succeeded")
                throw new ValidationException("Payment has not succeeded yet. Stripe status: " + intent.Status);

            var payment = await _paymentRepo.GetByStripePaymentIntentId(request.StripePaymentIntentId)
                ?? throw new NotFoundException("Payment intent not found.");

            var booking = await _bookingRepo.GetById(payment.BookingId)
                ?? throw new NotFoundException("Booking not found.");

            if (booking.UserId != userId)
                throw new UnauthorizedException("Not authorized to confirm this payment.");

            if (payment.Status == "Succeeded")
                return _mapper.Map<PaymentDto>(payment);

            payment.StripeChargeId = intent.LatestChargeId ?? string.Empty;
            payment.Status = "Succeeded";
            payment.PaidAt = DateTime.UtcNow;
            await _paymentRepo.Update(payment);

            booking.BookingStatus = "Confirmed";
            booking.UpdatedAt = DateTime.UtcNow;
            await _bookingRepo.Update(booking);

            return _mapper.Map<PaymentDto>(payment);
        }

        public async Task<PaymentDto?> GetByBookingId(int bookingId, int userId)
        {
            var booking = await _bookingRepo.GetById(bookingId)
                ?? throw new NotFoundException($"Booking {bookingId} not found.");

            if (booking.UserId != userId)
                throw new UnauthorizedException("Not authorized to view this payment.");

            var payment = await _paymentRepo.GetByBookingId(bookingId);
            return payment == null ? null : _mapper.Map<PaymentDto>(payment);
        }
    }
}
