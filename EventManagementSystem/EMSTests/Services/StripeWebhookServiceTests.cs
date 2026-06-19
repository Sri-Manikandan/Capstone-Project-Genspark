using EMSBLLLibrary.Services;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.Exceptions;
using EMSModelLibrary.Models;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;
using Stripe;
using StripeEvent = Stripe.Event;

namespace EMSTests.Services
{
    /// <summary>
    /// Test double that bypasses Stripe signature verification so we can
    /// exercise the event-routing logic with pre-built Stripe Event objects.
    /// </summary>
    internal class TestableStripeWebhookService : StripeWebhookService
    {
        private readonly Stripe.Event? _eventToReturn;
        private readonly bool _throwStripeException;

        public TestableStripeWebhookService(
            IPaymentRepository paymentRepo,
            IBookingRepository bookingRepo,
            IConfiguration config,
            Stripe.Event? eventToReturn = null,
            bool throwStripeException = false)
            : base(paymentRepo, bookingRepo, config)
        {
            _eventToReturn = eventToReturn;
            _throwStripeException = throwStripeException;
        }

        protected override Stripe.Event ConstructStripeEvent(string payload, string signature)
        {
            if (_throwStripeException)
                throw new StripeException("Invalid signature");

            return _eventToReturn!;
        }
    }

    [TestFixture]
    public class StripeWebhookServiceTests
    {
        private Mock<IPaymentRepository> _paymentRepo;
        private Mock<IBookingRepository> _bookingRepo;
        private IConfiguration _config;

        [SetUp]
        public void SetUp()
        {
            _paymentRepo = new Mock<IPaymentRepository>();
            _bookingRepo = new Mock<IBookingRepository>();

            var configData = new Dictionary<string, string?>
            {
                ["Stripe:WebhookSecret"] = "whsec_test_secret"
            };
            _config = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();
        }

        private TestableStripeWebhookService MakeSut(Stripe.Event? ev = null, bool throwStripeException = false) =>
            new TestableStripeWebhookService(_paymentRepo.Object, _bookingRepo.Object, _config, ev, throwStripeException);

        private static Stripe.Event MakeStripeEvent(string type, string intentId = "pi_test") =>
            new Stripe.Event
            {
                Type = type,
                Data = new EventData
                {
                    Object = new PaymentIntent { Id = intentId, Status = "succeeded", LatestChargeId = "ch_test" }
                }
            };

        // ── Signature validation ─────────────────────────────────────────────────

        [Test]
        public async Task ProcessAsync_InvalidSignature_ThrowsValidationException()
        {
            var sut = MakeSut(throwStripeException: true);

            await sut.Invoking(s => s.ProcessAsync("payload", "bad_sig"))
                .Should().ThrowAsync<ValidationException>().WithMessage("*Invalid Stripe webhook*");
        }

        // ── payment_intent.succeeded ─────────────────────────────────────────────

        [Test]
        public async Task ProcessAsync_PaymentSucceeded_PendingPayment_UpdatesPaymentAndBooking()
        {
            var payment = new Payment { Id = 1, BookingId = 1, StripePaymentIntentId = "pi_test", Status = "Pending" };
            var booking = new Booking { Id = 1, UserId = 1, BookingStatus = "Pending" };
            _paymentRepo.Setup(r => r.GetByStripePaymentIntentId("pi_test")).ReturnsAsync(payment);
            _bookingRepo.Setup(r => r.GetById(1)).ReturnsAsync(booking);
            _paymentRepo.Setup(r => r.Update(It.IsAny<Payment>())).ReturnsAsync(payment);
            _bookingRepo.Setup(r => r.Update(It.IsAny<Booking>())).ReturnsAsync(booking);

            var sut = MakeSut(MakeStripeEvent(EventTypes.PaymentIntentSucceeded));
            await sut.ProcessAsync("payload", "sig");

            _paymentRepo.Verify(r => r.Update(It.Is<Payment>(p => p.Status == "Succeeded")), Times.Once);
            _bookingRepo.Verify(r => r.Update(It.Is<Booking>(b => b.BookingStatus == "Confirmed")), Times.Once);
        }

        [Test]
        public async Task ProcessAsync_PaymentSucceeded_AlreadySucceeded_DoesNotUpdate()
        {
            var payment = new Payment { Id = 1, BookingId = 1, StripePaymentIntentId = "pi_test", Status = "Succeeded" };
            _paymentRepo.Setup(r => r.GetByStripePaymentIntentId("pi_test")).ReturnsAsync(payment);

            var sut = MakeSut(MakeStripeEvent(EventTypes.PaymentIntentSucceeded));
            await sut.ProcessAsync("payload", "sig");

            _paymentRepo.Verify(r => r.Update(It.IsAny<Payment>()), Times.Never);
        }

        [Test]
        public async Task ProcessAsync_PaymentSucceeded_PaymentNotFound_DoesNotUpdate()
        {
            _paymentRepo.Setup(r => r.GetByStripePaymentIntentId("pi_test")).ReturnsAsync((Payment?)null);

            var sut = MakeSut(MakeStripeEvent(EventTypes.PaymentIntentSucceeded));
            await sut.ProcessAsync("payload", "sig");

            _paymentRepo.Verify(r => r.Update(It.IsAny<Payment>()), Times.Never);
        }

        [Test]
        public async Task ProcessAsync_PaymentSucceeded_BookingAlreadyConfirmed_SkipsBookingUpdate()
        {
            var payment = new Payment { Id = 1, BookingId = 1, StripePaymentIntentId = "pi_test", Status = "Pending" };
            var booking = new Booking { Id = 1, BookingStatus = "Confirmed" };
            _paymentRepo.Setup(r => r.GetByStripePaymentIntentId("pi_test")).ReturnsAsync(payment);
            _bookingRepo.Setup(r => r.GetById(1)).ReturnsAsync(booking);
            _paymentRepo.Setup(r => r.Update(It.IsAny<Payment>())).ReturnsAsync(payment);

            var sut = MakeSut(MakeStripeEvent(EventTypes.PaymentIntentSucceeded));
            await sut.ProcessAsync("payload", "sig");

            _bookingRepo.Verify(r => r.Update(It.IsAny<Booking>()), Times.Never);
        }

        [Test]
        public async Task ProcessAsync_PaymentSucceeded_BookingNull_OnlyUpdatesPayment()
        {
            var payment = new Payment { Id = 1, BookingId = 1, StripePaymentIntentId = "pi_test", Status = "Pending" };
            _paymentRepo.Setup(r => r.GetByStripePaymentIntentId("pi_test")).ReturnsAsync(payment);
            _bookingRepo.Setup(r => r.GetById(1)).ReturnsAsync((Booking?)null);
            _paymentRepo.Setup(r => r.Update(It.IsAny<Payment>())).ReturnsAsync(payment);

            var sut = MakeSut(MakeStripeEvent(EventTypes.PaymentIntentSucceeded));
            await sut.ProcessAsync("payload", "sig");

            _paymentRepo.Verify(r => r.Update(It.Is<Payment>(p => p.Status == "Succeeded")), Times.Once);
            _bookingRepo.Verify(r => r.Update(It.IsAny<Booking>()), Times.Never);
        }

        // ── payment_intent.payment_failed ────────────────────────────────────────

        [Test]
        public async Task ProcessAsync_PaymentFailed_ExistingPayment_SetsStatusFailed()
        {
            var payment = new Payment { Id = 1, BookingId = 1, StripePaymentIntentId = "pi_test", Status = "Pending" };
            _paymentRepo.Setup(r => r.GetByStripePaymentIntentId("pi_test")).ReturnsAsync(payment);
            _paymentRepo.Setup(r => r.Update(It.IsAny<Payment>())).ReturnsAsync(payment);

            var sut = MakeSut(MakeStripeEvent(EventTypes.PaymentIntentPaymentFailed));
            await sut.ProcessAsync("payload", "sig");

            _paymentRepo.Verify(r => r.Update(It.Is<Payment>(p => p.Status == "Failed")), Times.Once);
        }

        [Test]
        public async Task ProcessAsync_PaymentFailed_PaymentNotFound_DoesNotUpdate()
        {
            _paymentRepo.Setup(r => r.GetByStripePaymentIntentId("pi_test")).ReturnsAsync((Payment?)null);

            var sut = MakeSut(MakeStripeEvent(EventTypes.PaymentIntentPaymentFailed));
            await sut.ProcessAsync("payload", "sig");

            _paymentRepo.Verify(r => r.Update(It.IsAny<Payment>()), Times.Never);
        }

        // ── Unknown event type ────────────────────────────────────────────────────

        [Test]
        public async Task ProcessAsync_UnknownEventType_DoesNothing()
        {
            var sut = MakeSut(MakeStripeEvent("charge.succeeded"));
            await sut.ProcessAsync("payload", "sig");

            _paymentRepo.Verify(r => r.Update(It.IsAny<Payment>()), Times.Never);
            _bookingRepo.Verify(r => r.Update(It.IsAny<Booking>()), Times.Never);
        }

        // ── Missing webhook secret ────────────────────────────────────────────────

        [Test]
        public void Constructor_MissingWebhookSecret_ThrowsException()
        {
            var badConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>())
                .Build();

            FluentActions.Invoking(() =>
                new TestableStripeWebhookService(_paymentRepo.Object, _bookingRepo.Object, badConfig))
                .Should().Throw<Exception>();
        }
    }
}
