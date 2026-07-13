using EMSBLLLibrary.Constants;
using EMSBLLLibrary.Interfaces;
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
            IUserRepository userRepo,
            IBookingItemRepository bookingItemRepo,
            IScreeningRepository screeningRepo,
            IEventRepository eventRepo,
            IVenueRepository venueRepo,
            ITicketTypeRepository ticketTypeRepo,
            ISeatRepository seatRepo,
            IEmailQueue emailQueue,
            IConfiguration config,
            Stripe.Event? eventToReturn = null,
            bool throwStripeException = false)
            : base(paymentRepo, bookingRepo, userRepo, bookingItemRepo, screeningRepo, eventRepo,
                   venueRepo, ticketTypeRepo, seatRepo, emailQueue, config)
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
        private Mock<IUserRepository> _userRepo;
        private Mock<IBookingItemRepository> _bookingItemRepo;
        private Mock<IScreeningRepository> _screeningRepo;
        private Mock<IEventRepository> _eventRepo;
        private Mock<IVenueRepository> _venueRepo;
        private Mock<ITicketTypeRepository> _ticketTypeRepo;
        private Mock<ISeatRepository> _seatRepo;
        private Mock<IEmailQueue> _emailQueue;
        private IConfiguration _config;

        [SetUp]
        public void SetUp()
        {
            _paymentRepo = new Mock<IPaymentRepository>();
            _bookingRepo = new Mock<IBookingRepository>();
            _userRepo = new Mock<IUserRepository>();
            _bookingItemRepo = new Mock<IBookingItemRepository>();
            _screeningRepo = new Mock<IScreeningRepository>();
            _eventRepo = new Mock<IEventRepository>();
            _venueRepo = new Mock<IVenueRepository>();
            _ticketTypeRepo = new Mock<ITicketTypeRepository>();
            _seatRepo = new Mock<ISeatRepository>();
            _emailQueue = new Mock<IEmailQueue>();

            _userRepo.Setup(r => r.GetById(1))
                .ReturnsAsync(new User { Id = 1, Name = "Asha", Email = "a@b.com" });
            _screeningRepo.Setup(r => r.GetById(It.IsAny<int>()))
                .ReturnsAsync(new Screening { Id = 1, EventId = 7, StartTime = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc) });
            _eventRepo.Setup(r => r.GetById(7))
                .ReturnsAsync(new EMSModelLibrary.Models.Event { Id = 7, Title = "Vaaranam Aayiram", VenueId = 3 });
            _venueRepo.Setup(r => r.GetById(3))
                .ReturnsAsync(new Venue { Id = 3, Name = "Sathyam Cinemas" });
            _bookingItemRepo.Setup(r => r.GetByBookingId(It.IsAny<int>()))
                .ReturnsAsync(new List<BookingItem>());

            var configData = new Dictionary<string, string?>
            {
                ["Stripe:WebhookSecret"] = "whsec_test_secret"
            };
            _config = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();
        }

        private TestableStripeWebhookService MakeSut(Stripe.Event? ev = null, bool throwStripeException = false) =>
            new TestableStripeWebhookService(_paymentRepo.Object, _bookingRepo.Object, _userRepo.Object,
                _bookingItemRepo.Object, _screeningRepo.Object, _eventRepo.Object, _venueRepo.Object,
                _ticketTypeRepo.Object, _seatRepo.Object, _emailQueue.Object, _config, ev, throwStripeException);

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
        public async Task ProcessAsync_PaymentSucceeded_EnqueuesBookingConfirmedWithQrAttachment()
        {
            var payment = new Payment { Id = 1, BookingId = 1, StripePaymentIntentId = "pi_test", Status = "Pending" };
            var booking = new Booking
            {
                Id = 1,
                UserId = 1,
                ScreeningId = 1,
                BookingStatus = "Pending",
                BookingReference = "BK1",
                QrCode = "aGVsbG8=",
                TotalAmount = 450m
            };
            _paymentRepo.Setup(r => r.GetByStripePaymentIntentId("pi_test")).ReturnsAsync(payment);
            _bookingRepo.Setup(r => r.GetById(1)).ReturnsAsync(booking);
            _paymentRepo.Setup(r => r.Update(It.IsAny<Payment>())).ReturnsAsync(payment);
            _bookingRepo.Setup(r => r.Update(It.IsAny<Booking>())).ReturnsAsync(booking);

            IDictionary<string, string>? tokens = null;
            _emailQueue.Setup(q => q.Enqueue(It.IsAny<string>(), It.IsAny<string>(), EmailTemplateKey.BookingConfirmed,
                    It.IsAny<string>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<string?>(), It.IsAny<DateTime?>()))
                .Callback<string, string, string, string, IDictionary<string, string>, string?, DateTime?>(
                    (_, _, _, _, t, _, _) => tokens = t)
                .Returns(Task.CompletedTask);

            var sut = MakeSut(MakeStripeEvent(EventTypes.PaymentIntentSucceeded));
            await sut.ProcessAsync("payload", "sig");

            tokens.Should().NotBeNull();
            tokens!["QrBase64"].Should().Be("aGVsbG8=");
            tokens["BookingReference"].Should().Be("BK1");
            tokens["EventTitle"].Should().Be("Vaaranam Aayiram");
            tokens.Should().ContainKey("ItemsHtml");
        }

        // Stripe retries webhooks. Sending someone their ticket twice is a support call.
        [Test]
        public async Task ProcessAsync_PaymentSucceeded_AlreadyConfirmedBooking_DoesNotEnqueueTwice()
        {
            var payment = new Payment { Id = 1, BookingId = 1, StripePaymentIntentId = "pi_test", Status = "Pending" };
            var booking = new Booking { Id = 1, UserId = 1, ScreeningId = 1, BookingStatus = "Confirmed", BookingReference = "BK1" };
            _paymentRepo.Setup(r => r.GetByStripePaymentIntentId("pi_test")).ReturnsAsync(payment);
            _bookingRepo.Setup(r => r.GetById(1)).ReturnsAsync(booking);
            _paymentRepo.Setup(r => r.Update(It.IsAny<Payment>())).ReturnsAsync(payment);

            var sut = MakeSut(MakeStripeEvent(EventTypes.PaymentIntentSucceeded));
            await sut.ProcessAsync("payload", "sig");

            _emailQueue.Verify(q => q.Enqueue(It.IsAny<string>(), It.IsAny<string>(), EmailTemplateKey.BookingConfirmed,
                It.IsAny<string>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<string?>(), It.IsAny<DateTime?>()),
                Times.Never);
        }

        [Test]
        public async Task ProcessAsync_PaymentFailed_EnqueuesPaymentFailedEmail()
        {
            var payment = new Payment { Id = 1, BookingId = 1, StripePaymentIntentId = "pi_test", Status = "Pending" };
            var booking = new Booking { Id = 1, UserId = 1, ScreeningId = 1, BookingStatus = "Pending", BookingReference = "BK1" };
            _paymentRepo.Setup(r => r.GetByStripePaymentIntentId("pi_test")).ReturnsAsync(payment);
            _bookingRepo.Setup(r => r.GetById(1)).ReturnsAsync(booking);
            _paymentRepo.Setup(r => r.Update(It.IsAny<Payment>())).ReturnsAsync(payment);

            var sut = MakeSut(MakeStripeEvent(EventTypes.PaymentIntentPaymentFailed));
            await sut.ProcessAsync("payload", "sig");

            _emailQueue.Verify(q => q.Enqueue("a@b.com", "Asha", EmailTemplateKey.PaymentFailed,
                It.IsAny<string>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<string?>(), It.IsAny<DateTime?>()),
                Times.Once);
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
                new TestableStripeWebhookService(_paymentRepo.Object, _bookingRepo.Object, _userRepo.Object,
                    _bookingItemRepo.Object, _screeningRepo.Object, _eventRepo.Object, _venueRepo.Object,
                    _ticketTypeRepo.Object, _seatRepo.Object, _emailQueue.Object, badConfig))
                .Should().Throw<Exception>();
        }
    }
}
