using AutoMapper;
using EMSBLLLibrary.Interfaces;
using EMSBLLLibrary.Mappings;
using EMSBLLLibrary.Services;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.DTOs;
using EMSModelLibrary.Exceptions;
using EMSModelLibrary.Models;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using Stripe;

namespace EMSTests.Services
{
    [TestFixture]
    public class PaymentServiceTests
    {
        private Mock<IPaymentRepository> _paymentRepo;
        private Mock<IBookingRepository> _bookingRepo;
        private Mock<IStripePaymentIntentClient> _stripeClient;
        private IMapper _mapper;
        private PaymentService _sut;

        [SetUp]
        public void SetUp()
        {
            _paymentRepo = new Mock<IPaymentRepository>();
            _bookingRepo = new Mock<IBookingRepository>();
            _stripeClient = new Mock<IStripePaymentIntentClient>();
            _mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>(), Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance).CreateMapper();
            _sut = new PaymentService(_paymentRepo.Object, _bookingRepo.Object, _stripeClient.Object, _mapper);
        }

        private Booking MakeBooking(int userId = 1, string status = "Pending", decimal total = 500m) => new Booking
        {
            Id = 1, UserId = userId, EventId = 1, TotalAmount = total,
            BookingStatus = status, ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };

        private PaymentIntent MakeIntent(string id = "pi_test", string status = "requires_payment_method", string clientSecret = "secret_123") =>
            new PaymentIntent { Id = id, Status = status, ClientSecret = clientSecret, LatestChargeId = "ch_test" };

        // ── Initiate ─────────────────────────────────────────────────────────────

        [Test]
        public async Task Initiate_NewPayment_CreatesIntentAndReturnsClientSecret()
        {
            _bookingRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeBooking());
            _paymentRepo.Setup(r => r.GetByBookingId(1)).ReturnsAsync((Payment?)null);
            _stripeClient.Setup(c => c.CreateAsync(It.IsAny<PaymentIntentCreateOptions>()))
                .ReturnsAsync(MakeIntent(clientSecret: "pi_secret_xyz"));
            _paymentRepo.Setup(r => r.Add(It.IsAny<Payment>())).ReturnsAsync((Payment p) => p);

            var result = await _sut.Initiate(1, new InitiatePaymentRequest { BookingId = 1, Currency = "inr" });

            result.ClientSecret.Should().Be("pi_secret_xyz");
            result.Status.Should().Be("Pending");
        }

        [Test]
        public async Task Initiate_ExistingPendingPayment_ReturnsExistingWithClientSecret()
        {
            var existingPayment = new Payment { Id = 5, BookingId = 1, StripePaymentIntentId = "pi_existing", Amount = 500, Currency = "inr", Status = "Pending" };
            _bookingRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeBooking());
            _paymentRepo.Setup(r => r.GetByBookingId(1)).ReturnsAsync(existingPayment);
            _stripeClient.Setup(c => c.GetAsync("pi_existing")).ReturnsAsync(MakeIntent(id: "pi_existing", clientSecret: "pi_existing_secret"));

            var result = await _sut.Initiate(1, new InitiatePaymentRequest { BookingId = 1, Currency = "inr" });

            result.ClientSecret.Should().Be("pi_existing_secret");
            result.Id.Should().Be(5);
        }

        [Test]
        public async Task Initiate_BookingNotFound_ThrowsNotFoundException()
        {
            _bookingRepo.Setup(r => r.GetById(99)).ReturnsAsync((Booking?)null);

            await _sut.Invoking(s => s.Initiate(1, new InitiatePaymentRequest { BookingId = 99 }))
                .Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task Initiate_WrongUser_ThrowsUnauthorizedException()
        {
            _bookingRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeBooking(userId: 2));

            await _sut.Invoking(s => s.Initiate(1, new InitiatePaymentRequest { BookingId = 1 }))
                .Should().ThrowAsync<UnauthorizedException>();
        }

        [Test]
        public async Task Initiate_AlreadyConfirmed_ThrowsValidationException()
        {
            _bookingRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeBooking(status: "Confirmed"));

            await _sut.Invoking(s => s.Initiate(1, new InitiatePaymentRequest { BookingId = 1 }))
                .Should().ThrowAsync<ValidationException>().WithMessage("*already paid*");
        }

        [Test]
        public async Task Initiate_CancelledBooking_ThrowsValidationException()
        {
            _bookingRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeBooking(status: "Cancelled"));

            await _sut.Invoking(s => s.Initiate(1, new InitiatePaymentRequest { BookingId = 1 }))
                .Should().ThrowAsync<ValidationException>().WithMessage("*cancelled*");
        }

        [Test]
        public async Task Initiate_ExpiredBooking_ThrowsValidationException()
        {
            var booking = new Booking
            {
                Id = 1, UserId = 1, BookingStatus = "Pending",
                ExpiresAt = DateTime.UtcNow.AddMinutes(-5)
            };
            _bookingRepo.Setup(r => r.GetById(1)).ReturnsAsync(booking);

            await _sut.Invoking(s => s.Initiate(1, new InitiatePaymentRequest { BookingId = 1 }))
                .Should().ThrowAsync<ValidationException>().WithMessage("*expired*");
        }

        // ── Confirm ──────────────────────────────────────────────────────────────

        [Test]
        public async Task Confirm_SucceededIntent_UpdatesPaymentAndBooking()
        {
            var payment = new Payment { Id = 1, BookingId = 1, StripePaymentIntentId = "pi_test", Status = "Pending" };
            var booking = new Booking { Id = 1, UserId = 1, BookingStatus = "Pending" };
            _stripeClient.Setup(c => c.GetAsync("pi_test")).ReturnsAsync(MakeIntent(status: "succeeded"));
            _paymentRepo.Setup(r => r.GetByStripePaymentIntentId("pi_test")).ReturnsAsync(payment);
            _bookingRepo.Setup(r => r.GetById(1)).ReturnsAsync(booking);
            _paymentRepo.Setup(r => r.Update(It.IsAny<Payment>())).ReturnsAsync((Payment p) => p);
            _bookingRepo.Setup(r => r.Update(It.IsAny<Booking>())).ReturnsAsync((Booking b) => b);

            var result = await _sut.Confirm(1, new ConfirmPaymentRequest { StripePaymentIntentId = "pi_test" });

            result.Status.Should().Be("Succeeded");
            booking.BookingStatus.Should().Be("Confirmed");
        }

        [Test]
        public async Task Confirm_AlreadySucceeded_ReturnsMappedDto()
        {
            var payment = new Payment { Id = 1, BookingId = 1, StripePaymentIntentId = "pi_test", Status = "Succeeded" };
            var booking = new Booking { Id = 1, UserId = 1, BookingStatus = "Confirmed" };
            _stripeClient.Setup(c => c.GetAsync("pi_test")).ReturnsAsync(MakeIntent(status: "succeeded"));
            _paymentRepo.Setup(r => r.GetByStripePaymentIntentId("pi_test")).ReturnsAsync(payment);
            _bookingRepo.Setup(r => r.GetById(1)).ReturnsAsync(booking);

            var result = await _sut.Confirm(1, new ConfirmPaymentRequest { StripePaymentIntentId = "pi_test" });

            result.Status.Should().Be("Succeeded");
            _paymentRepo.Verify(r => r.Update(It.IsAny<Payment>()), Times.Never);
        }

        [Test]
        public async Task Confirm_NotSucceeded_ThrowsValidationException()
        {
            _stripeClient.Setup(c => c.GetAsync("pi_test")).ReturnsAsync(MakeIntent(status: "requires_payment_method"));

            await _sut.Invoking(s => s.Confirm(1, new ConfirmPaymentRequest { StripePaymentIntentId = "pi_test" }))
                .Should().ThrowAsync<ValidationException>().WithMessage("*not succeeded*");
        }

        [Test]
        public async Task Confirm_PaymentNotFound_ThrowsNotFoundException()
        {
            _stripeClient.Setup(c => c.GetAsync("pi_test")).ReturnsAsync(MakeIntent(status: "succeeded"));
            _paymentRepo.Setup(r => r.GetByStripePaymentIntentId("pi_test")).ReturnsAsync((Payment?)null);

            await _sut.Invoking(s => s.Confirm(1, new ConfirmPaymentRequest { StripePaymentIntentId = "pi_test" }))
                .Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task Confirm_BookingNotFound_ThrowsNotFoundException()
        {
            var payment = new Payment { Id = 1, BookingId = 99, StripePaymentIntentId = "pi_test", Status = "Pending" };
            _stripeClient.Setup(c => c.GetAsync("pi_test")).ReturnsAsync(MakeIntent(status: "succeeded"));
            _paymentRepo.Setup(r => r.GetByStripePaymentIntentId("pi_test")).ReturnsAsync(payment);
            _bookingRepo.Setup(r => r.GetById(99)).ReturnsAsync((Booking?)null);

            await _sut.Invoking(s => s.Confirm(1, new ConfirmPaymentRequest { StripePaymentIntentId = "pi_test" }))
                .Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task Confirm_WrongUser_ThrowsUnauthorizedException()
        {
            var payment = new Payment { Id = 1, BookingId = 1, StripePaymentIntentId = "pi_test", Status = "Pending" };
            var booking = new Booking { Id = 1, UserId = 2, BookingStatus = "Pending" };
            _stripeClient.Setup(c => c.GetAsync("pi_test")).ReturnsAsync(MakeIntent(status: "succeeded"));
            _paymentRepo.Setup(r => r.GetByStripePaymentIntentId("pi_test")).ReturnsAsync(payment);
            _bookingRepo.Setup(r => r.GetById(1)).ReturnsAsync(booking);

            await _sut.Invoking(s => s.Confirm(1, new ConfirmPaymentRequest { StripePaymentIntentId = "pi_test" }))
                .Should().ThrowAsync<UnauthorizedException>();
        }

        // ── GetByBookingId ───────────────────────────────────────────────────────

        [Test]
        public async Task GetByBookingId_WithPayment_ReturnsMappedDto()
        {
            _bookingRepo.Setup(r => r.GetById(1)).ReturnsAsync(new Booking { Id = 1, UserId = 1 });
            _paymentRepo.Setup(r => r.GetByBookingId(1)).ReturnsAsync(new Payment { Id = 1, BookingId = 1, Status = "Succeeded" });

            var result = await _sut.GetByBookingId(1, 1);

            result.Should().NotBeNull();
            result!.Status.Should().Be("Succeeded");
        }

        [Test]
        public async Task GetByBookingId_NoPayment_ReturnsNull()
        {
            _bookingRepo.Setup(r => r.GetById(1)).ReturnsAsync(new Booking { Id = 1, UserId = 1 });
            _paymentRepo.Setup(r => r.GetByBookingId(1)).ReturnsAsync((Payment?)null);

            var result = await _sut.GetByBookingId(1, 1);

            result.Should().BeNull();
        }

        [Test]
        public async Task GetByBookingId_BookingNotFound_ThrowsNotFoundException()
        {
            _bookingRepo.Setup(r => r.GetById(99)).ReturnsAsync((Booking?)null);

            await _sut.Invoking(s => s.GetByBookingId(99, 1)).Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task GetByBookingId_WrongUser_ThrowsUnauthorizedException()
        {
            _bookingRepo.Setup(r => r.GetById(1)).ReturnsAsync(new Booking { Id = 1, UserId = 2 });

            await _sut.Invoking(s => s.GetByBookingId(1, 1)).Should().ThrowAsync<UnauthorizedException>();
        }
    }
}
