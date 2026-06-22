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
using EmsEvent = EMSModelLibrary.Models.Event;

namespace EMSTests.Services
{
    [TestFixture]
    public class BookingServiceTests
    {
        private Mock<IBookingRepository> _bookingRepo;
        private Mock<IBookingItemRepository> _bookingItemRepo;
        private Mock<ITicketTypeRepository> _ticketTypeRepo;
        private Mock<ISeatRepository> _seatRepo;
        private Mock<ISeatReservationRepository> _reservationRepo;
        private Mock<IEventRepository> _eventRepo;
        private Mock<ISeatNotifier> _notifier;
        private Mock<IPaymentRepository> _paymentRepo;
        private Mock<IStripeRefundClient> _refundClient;
        private IMapper _mapper;
        private BookingService _sut;

        private static readonly DateTime FutureStart = DateTime.UtcNow.AddDays(5);

        [SetUp]
        public void SetUp()
        {
            _bookingRepo = new Mock<IBookingRepository>();
            _bookingItemRepo = new Mock<IBookingItemRepository>();
            _ticketTypeRepo = new Mock<ITicketTypeRepository>();
            _seatRepo = new Mock<ISeatRepository>();
            _reservationRepo = new Mock<ISeatReservationRepository>();
            _eventRepo = new Mock<IEventRepository>();
            _notifier = new Mock<ISeatNotifier>();
            _paymentRepo = new Mock<IPaymentRepository>();
            _refundClient = new Mock<IStripeRefundClient>();
            _mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>(), Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance).CreateMapper();

            _sut = new BookingService(
                _bookingRepo.Object, _bookingItemRepo.Object, _ticketTypeRepo.Object,
                _seatRepo.Object, _reservationRepo.Object, _eventRepo.Object,
                _notifier.Object, _mapper, _paymentRepo.Object, _refundClient.Object);
        }

        private EmsEvent PublishedEvent() => new EmsEvent
        {
            Id = 1, Status = "Published", StartTime = FutureStart, EndTime = FutureStart.AddHours(3), Title = "Fest", VenueId = 1
        };

        private TicketType ActiveTicket(int seatId = 1) => new TicketType
        {
            Id = 1, EventId = 1, Name = "VIP", SeatType = "VIP", Price = 500, AvailableQuantity = 10, TotalQuantity = 100
        };

        private Seat MakeSeat(int id = 1) => new Seat { Id = id, VenueId = 1, SeatType = "VIP", Section = "A", Row = "1", SeatNumber = id };

        private SeatReservation ActiveReservation(int userId = 1) => new SeatReservation
        {
            Id = 1, SeatId = 1, EventId = 1, UserId = userId, Status = "Active",
            ReservedUntil = DateTime.UtcNow.AddMinutes(10)
        };

        private Booking MakeBooking(int userId = 1, string status = "Pending") => new Booking
        {
            Id = 1, UserId = userId, EventId = 1, BookingStatus = status,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };

        // ── Create ───────────────────────────────────────────────────────────────

        [Test]
        public async Task Create_ValidRequest_ReturnsBookingDto()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(PublishedEvent());
            _ticketTypeRepo.Setup(r => r.GetById(1)).ReturnsAsync(ActiveTicket());
            _reservationRepo.Setup(r => r.GetActiveByEventAndSeat(1, 1)).ReturnsAsync(ActiveReservation());
            _seatRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeSeat());
            _bookingRepo.Setup(r => r.Add(It.IsAny<Booking>())).ReturnsAsync((Booking b) => b);
            _bookingItemRepo.Setup(r => r.Add(It.IsAny<BookingItem>())).ReturnsAsync((BookingItem bi) => bi);
            _ticketTypeRepo.Setup(r => r.TryDecrementAvailableQuantity(1)).ReturnsAsync(true);
            _reservationRepo.Setup(r => r.Update(It.IsAny<SeatReservation>())).ReturnsAsync(ActiveReservation());
            _notifier.Setup(n => n.SeatBooked(1, 1)).Returns(Task.CompletedTask);
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(PublishedEvent());

            var result = await _sut.Create(1, new CreateBookingRequest
            {
                EventId = 1,
                Items = new List<BookingItemRequest> { new BookingItemRequest { TicketTypeId = 1, SeatId = 1 } }
            });

            result.Should().NotBeNull();
            result.BookingStatus.Should().Be("Pending");
        }

        [Test]
        public async Task Create_EventNotFound_ThrowsNotFoundException()
        {
            _eventRepo.Setup(r => r.GetById(99)).ReturnsAsync((EmsEvent?)null);

            await _sut.Invoking(s => s.Create(1, new CreateBookingRequest
            {
                EventId = 99, Items = new List<BookingItemRequest> { new BookingItemRequest { TicketTypeId = 1, SeatId = 1 } }
            })).Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task Create_EventNotPublished_ThrowsValidationException()
        {
            var ev = PublishedEvent();
            ev.Status = "Draft";
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(ev);

            await _sut.Invoking(s => s.Create(1, new CreateBookingRequest
            {
                EventId = 1, Items = new List<BookingItemRequest> { new BookingItemRequest { TicketTypeId = 1, SeatId = 1 } }
            })).Should().ThrowAsync<ValidationException>().WithMessage("*published*");
        }

        [Test]
        public async Task Create_EventAlreadyStarted_ThrowsValidationException()
        {
            var ev = PublishedEvent();
            ev.StartTime = DateTime.UtcNow.AddMinutes(-1);
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(ev);

            await _sut.Invoking(s => s.Create(1, new CreateBookingRequest
            {
                EventId = 1, Items = new List<BookingItemRequest> { new BookingItemRequest { TicketTypeId = 1, SeatId = 1 } }
            })).Should().ThrowAsync<ValidationException>().WithMessage("*already started*");
        }

        [Test]
        public async Task Create_NoItems_ThrowsValidationException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(PublishedEvent());

            await _sut.Invoking(s => s.Create(1, new CreateBookingRequest
            {
                EventId = 1, Items = new List<BookingItemRequest>()
            })).Should().ThrowAsync<ValidationException>().WithMessage("*At least one*");
        }

        [Test]
        public async Task Create_TicketTypeNotFound_ThrowsNotFoundException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(PublishedEvent());
            _ticketTypeRepo.Setup(r => r.GetById(99)).ReturnsAsync((TicketType?)null);

            await _sut.Invoking(s => s.Create(1, new CreateBookingRequest
            {
                EventId = 1, Items = new List<BookingItemRequest> { new BookingItemRequest { TicketTypeId = 99, SeatId = 1 } }
            })).Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task Create_TicketTypeWrongEvent_ThrowsValidationException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(PublishedEvent());
            _ticketTypeRepo.Setup(r => r.GetById(1)).ReturnsAsync(new TicketType { Id = 1, EventId = 99, SeatType = "VIP", AvailableQuantity = 10 });

            await _sut.Invoking(s => s.Create(1, new CreateBookingRequest
            {
                EventId = 1, Items = new List<BookingItemRequest> { new BookingItemRequest { TicketTypeId = 1, SeatId = 1 } }
            })).Should().ThrowAsync<ValidationException>().WithMessage("*does not belong*");
        }

        [Test]
        public async Task Create_TicketSoldOut_ThrowsValidationException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(PublishedEvent());
            _ticketTypeRepo.Setup(r => r.GetById(1)).ReturnsAsync(new TicketType { Id = 1, EventId = 1, Name = "VIP", SeatType = "VIP", AvailableQuantity = 0 });

            await _sut.Invoking(s => s.Create(1, new CreateBookingRequest
            {
                EventId = 1, Items = new List<BookingItemRequest> { new BookingItemRequest { TicketTypeId = 1, SeatId = 1 } }
            })).Should().ThrowAsync<ValidationException>().WithMessage("*sold out*");
        }

        [Test]
        public async Task Create_SeatNotReserved_ThrowsValidationException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(PublishedEvent());
            _ticketTypeRepo.Setup(r => r.GetById(1)).ReturnsAsync(ActiveTicket());
            _reservationRepo.Setup(r => r.GetActiveByEventAndSeat(1, 1)).ReturnsAsync((SeatReservation?)null);

            await _sut.Invoking(s => s.Create(1, new CreateBookingRequest
            {
                EventId = 1, Items = new List<BookingItemRequest> { new BookingItemRequest { TicketTypeId = 1, SeatId = 1 } }
            })).Should().ThrowAsync<ValidationException>().WithMessage("*not reserved*");
        }

        [Test]
        public async Task Create_SeatReservedByOtherUser_ThrowsValidationException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(PublishedEvent());
            _ticketTypeRepo.Setup(r => r.GetById(1)).ReturnsAsync(ActiveTicket());
            _reservationRepo.Setup(r => r.GetActiveByEventAndSeat(1, 1)).ReturnsAsync(ActiveReservation(userId: 99));

            await _sut.Invoking(s => s.Create(1, new CreateBookingRequest
            {
                EventId = 1, Items = new List<BookingItemRequest> { new BookingItemRequest { TicketTypeId = 1, SeatId = 1 } }
            })).Should().ThrowAsync<ValidationException>().WithMessage("*another user*");
        }

        [Test]
        public async Task Create_SeatTypeMismatch_ThrowsValidationException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(PublishedEvent());
            _ticketTypeRepo.Setup(r => r.GetById(1)).ReturnsAsync(ActiveTicket());
            _reservationRepo.Setup(r => r.GetActiveByEventAndSeat(1, 1)).ReturnsAsync(ActiveReservation());
            _seatRepo.Setup(r => r.GetById(1)).ReturnsAsync(new Seat { Id = 1, SeatType = "General" });

            await _sut.Invoking(s => s.Create(1, new CreateBookingRequest
            {
                EventId = 1, Items = new List<BookingItemRequest> { new BookingItemRequest { TicketTypeId = 1, SeatId = 1 } }
            })).Should().ThrowAsync<ValidationException>().WithMessage("*type*");
        }

        [Test]
        public async Task Create_DecrementFails_ThrowsValidationException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(PublishedEvent());
            _ticketTypeRepo.Setup(r => r.GetById(1)).ReturnsAsync(ActiveTicket());
            _reservationRepo.Setup(r => r.GetActiveByEventAndSeat(1, 1)).ReturnsAsync(ActiveReservation());
            _seatRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeSeat());
            _bookingRepo.Setup(r => r.Add(It.IsAny<Booking>())).ReturnsAsync((Booking b) => b);
            _bookingItemRepo.Setup(r => r.Add(It.IsAny<BookingItem>())).ReturnsAsync((BookingItem bi) => bi);
            _ticketTypeRepo.Setup(r => r.TryDecrementAvailableQuantity(1)).ReturnsAsync(false);

            await _sut.Invoking(s => s.Create(1, new CreateBookingRequest
            {
                EventId = 1, Items = new List<BookingItemRequest> { new BookingItemRequest { TicketTypeId = 1, SeatId = 1 } }
            })).Should().ThrowAsync<ValidationException>().WithMessage("*sold out*");
        }

        // ── GetById ──────────────────────────────────────────────────────────────

        [Test]
        public async Task GetById_OwnBooking_ReturnsDto()
        {
            _bookingRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeBooking());
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(PublishedEvent());
            _bookingItemRepo.Setup(r => r.GetByBookingId(1)).ReturnsAsync(new List<BookingItem>());

            var result = await _sut.GetById(1, 1);

            result.Should().NotBeNull();
        }

        [Test]
        public async Task GetById_NotFound_ThrowsNotFoundException()
        {
            _bookingRepo.Setup(r => r.GetById(99)).ReturnsAsync((Booking?)null);

            await _sut.Invoking(s => s.GetById(99, 1)).Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task GetById_WrongUser_ThrowsUnauthorizedException()
        {
            _bookingRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeBooking(userId: 2));

            await _sut.Invoking(s => s.GetById(1, 1)).Should().ThrowAsync<UnauthorizedException>();
        }

        // ── GetByReference ───────────────────────────────────────────────────────

        [Test]
        public async Task GetByReference_OwnBooking_ReturnsDto()
        {
            _bookingRepo.Setup(r => r.GetByReference("BK123")).ReturnsAsync(MakeBooking());
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(PublishedEvent());
            _bookingItemRepo.Setup(r => r.GetByBookingId(1)).ReturnsAsync(new List<BookingItem>());

            var result = await _sut.GetByReference("BK123", 1);

            result.Should().NotBeNull();
        }

        [Test]
        public async Task GetByReference_NotFound_ReturnsNull()
        {
            _bookingRepo.Setup(r => r.GetByReference("NONE")).ReturnsAsync((Booking?)null);

            var result = await _sut.GetByReference("NONE", 1);

            result.Should().BeNull();
        }

        [Test]
        public async Task GetByReference_WrongUser_ThrowsUnauthorizedException()
        {
            _bookingRepo.Setup(r => r.GetByReference("BK123")).ReturnsAsync(MakeBooking(userId: 2));

            await _sut.Invoking(s => s.GetByReference("BK123", 1)).Should().ThrowAsync<UnauthorizedException>();
        }

        // ── GetByUserId / GetByEventId ────────────────────────────────────────────

        [Test]
        public async Task GetByUserId_ReturnsMappedList()
        {
            _bookingRepo.Setup(r => r.SearchByUserId(1, null, 1, 10))
                        .ReturnsAsync((new List<Booking> { MakeBooking() }, 1));
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(PublishedEvent());
            _bookingItemRepo.Setup(r => r.GetByBookingId(1)).ReturnsAsync(new List<BookingItem>());

            var result = await _sut.GetByUserId(1, new BookingQueryRequest { Page = 1, PageSize = 10 });

            result.Items.Should().HaveCount(1);
            result.TotalCount.Should().Be(1);
        }

        [Test]
        public async Task GetByEventId_ReturnsMappedList()
        {
            _bookingRepo.Setup(r => r.SearchByEventId(1, null, 1, 10))
                        .ReturnsAsync((new List<Booking> { MakeBooking() }, 1));
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(PublishedEvent());
            _bookingItemRepo.Setup(r => r.GetByBookingId(1)).ReturnsAsync(new List<BookingItem>());

            var result = await _sut.GetByEventId(1, new BookingQueryRequest { Page = 1, PageSize = 10 });

            result.Items.Should().HaveCount(1);
            result.TotalCount.Should().Be(1);
        }

        // ── Cancel ───────────────────────────────────────────────────────────────

        [Test]
        public async Task Cancel_PendingBooking_CancelsWithoutRefund()
        {
            _bookingRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeBooking(status: "Pending"));
            _bookingRepo.Setup(r => r.Update(It.IsAny<Booking>())).ReturnsAsync((Booking b) => b);
            _bookingItemRepo.Setup(r => r.GetByBookingId(1)).ReturnsAsync(new List<BookingItem>());

            await _sut.Cancel(1, 1);

            _refundClient.Verify(r => r.CreateAsync(It.IsAny<RefundCreateOptions>()), Times.Never);
            _bookingRepo.Verify(r => r.Update(It.Is<Booking>(b => b.BookingStatus == "Cancelled")), Times.Once);
        }

        [Test]
        public async Task Cancel_ConfirmedBookingWithPayment_IssuesRefund()
        {
            var payment = new Payment { StripePaymentIntentId = "pi_test" };
            _bookingRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeBooking(status: "Confirmed"));
            _paymentRepo.Setup(r => r.GetByBookingId(1)).ReturnsAsync(payment);
            _refundClient.Setup(c => c.CreateAsync(It.IsAny<RefundCreateOptions>())).ReturnsAsync(new Refund());
            _bookingRepo.Setup(r => r.Update(It.IsAny<Booking>())).ReturnsAsync((Booking b) => b);
            _bookingItemRepo.Setup(r => r.GetByBookingId(1)).ReturnsAsync(new List<BookingItem>());

            await _sut.Cancel(1, 1);

            _refundClient.Verify(r => r.CreateAsync(It.Is<RefundCreateOptions>(o => o.PaymentIntent == "pi_test")), Times.Once);
        }

        [Test]
        public async Task Cancel_ConfirmedBookingWithoutPayment_SkipsRefund()
        {
            _bookingRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeBooking(status: "Confirmed"));
            _paymentRepo.Setup(r => r.GetByBookingId(1)).ReturnsAsync((Payment?)null);
            _bookingRepo.Setup(r => r.Update(It.IsAny<Booking>())).ReturnsAsync((Booking b) => b);
            _bookingItemRepo.Setup(r => r.GetByBookingId(1)).ReturnsAsync(new List<BookingItem>());

            await _sut.Cancel(1, 1);

            _refundClient.Verify(r => r.CreateAsync(It.IsAny<RefundCreateOptions>()), Times.Never);
        }

        [Test]
        public async Task Cancel_ConfirmedBookingWithEmptyIntentId_SkipsRefund()
        {
            var payment = new Payment { StripePaymentIntentId = "" };
            _bookingRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeBooking(status: "Confirmed"));
            _paymentRepo.Setup(r => r.GetByBookingId(1)).ReturnsAsync(payment);
            _bookingRepo.Setup(r => r.Update(It.IsAny<Booking>())).ReturnsAsync((Booking b) => b);
            _bookingItemRepo.Setup(r => r.GetByBookingId(1)).ReturnsAsync(new List<BookingItem>());

            await _sut.Cancel(1, 1);

            _refundClient.Verify(r => r.CreateAsync(It.IsAny<RefundCreateOptions>()), Times.Never);
        }

        [Test]
        public async Task Cancel_WithBookingItems_ReleasesSeatsAndIncrementsTickets()
        {
            var item = new BookingItem { Id = 1, TicketTypeId = 1, SeatId = 1 };
            _bookingRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeBooking(status: "Pending"));
            _bookingRepo.Setup(r => r.Update(It.IsAny<Booking>())).ReturnsAsync((Booking b) => b);
            _bookingItemRepo.Setup(r => r.GetByBookingId(1)).ReturnsAsync(new List<BookingItem> { item });
            _bookingItemRepo.Setup(r => r.Update(It.IsAny<BookingItem>())).ReturnsAsync(item);
            _ticketTypeRepo.Setup(r => r.IncrementAvailableQuantity(1)).Returns(Task.CompletedTask);
            _notifier.Setup(n => n.SeatReleased(1, 1)).Returns(Task.CompletedTask);

            await _sut.Cancel(1, 1);

            _ticketTypeRepo.Verify(r => r.IncrementAvailableQuantity(1), Times.Once);
            _notifier.Verify(n => n.SeatReleased(1, 1), Times.Once);
        }

        [Test]
        public async Task Cancel_NotFound_ThrowsNotFoundException()
        {
            _bookingRepo.Setup(r => r.GetById(99)).ReturnsAsync((Booking?)null);

            await _sut.Invoking(s => s.Cancel(99, 1)).Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task Cancel_WrongUser_ThrowsUnauthorizedException()
        {
            _bookingRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeBooking(userId: 2));

            await _sut.Invoking(s => s.Cancel(1, 1)).Should().ThrowAsync<UnauthorizedException>();
        }

        [Test]
        public async Task Cancel_AlreadyCancelled_ThrowsValidationException()
        {
            _bookingRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeBooking(status: "Cancelled"));

            await _sut.Invoking(s => s.Cancel(1, 1)).Should().ThrowAsync<ValidationException>().WithMessage("*already cancelled*");
        }

        [Test]
        public async Task Cancel_AttendedBooking_ThrowsValidationException()
        {
            _bookingRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeBooking(status: "Attended"));

            await _sut.Invoking(s => s.Cancel(1, 1)).Should().ThrowAsync<ValidationException>().WithMessage("*attended*");
        }

        // ── EnrichBooking paths ───────────────────────────────────────────────────

        [Test]
        public async Task GetById_WithItems_SeatAndTicketFound_PopulatesSeatLabelAndTicketName()
        {
            var item = new BookingItem { Id = 1, BookingId = 1, TicketTypeId = 1, SeatId = 1, UnitPrice = 500, TicketStatus = "Active" };
            _bookingRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeBooking());
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(PublishedEvent());
            _bookingItemRepo.Setup(r => r.GetByBookingId(1)).ReturnsAsync(new List<BookingItem> { item });
            _ticketTypeRepo.Setup(r => r.GetById(1)).ReturnsAsync(new TicketType { Id = 1, Name = "VIP", SeatType = "VIP" });
            _seatRepo.Setup(r => r.GetById(1)).ReturnsAsync(new Seat { Id = 1, Section = "A", Row = "1", SeatNumber = 5 });

            var result = await _sut.GetById(1, 1);

            result.Items.Should().HaveCount(1);
            result.Items[0].TicketTypeName.Should().Be("VIP");
            result.Items[0].SeatLabel.Should().Be("A-15");
            result.EventTitle.Should().Be("Fest");
        }

        [Test]
        public async Task GetById_WithItems_TicketTypeNull_EmptyTicketName()
        {
            var item = new BookingItem { Id = 1, BookingId = 1, TicketTypeId = 99, SeatId = 1, UnitPrice = 500, TicketStatus = "Active" };
            _bookingRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeBooking());
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(PublishedEvent());
            _bookingItemRepo.Setup(r => r.GetByBookingId(1)).ReturnsAsync(new List<BookingItem> { item });
            _ticketTypeRepo.Setup(r => r.GetById(99)).ReturnsAsync((TicketType?)null);
            _seatRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeSeat());

            var result = await _sut.GetById(1, 1);

            result.Items[0].TicketTypeName.Should().Be("");
        }

        [Test]
        public async Task GetById_WithItems_SeatNull_EmptySeatLabel()
        {
            var item = new BookingItem { Id = 1, BookingId = 1, TicketTypeId = 1, SeatId = 99, UnitPrice = 500, TicketStatus = "Active" };
            _bookingRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeBooking());
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(PublishedEvent());
            _bookingItemRepo.Setup(r => r.GetByBookingId(1)).ReturnsAsync(new List<BookingItem> { item });
            _ticketTypeRepo.Setup(r => r.GetById(1)).ReturnsAsync(new TicketType { Id = 1, Name = "VIP", SeatType = "VIP" });
            _seatRepo.Setup(r => r.GetById(99)).ReturnsAsync((Seat?)null);

            var result = await _sut.GetById(1, 1);

            result.Items[0].SeatLabel.Should().Be("");
        }

        [Test]
        public async Task GetById_EventNull_EmptyEventTitle()
        {
            _bookingRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeBooking());
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync((EmsEvent?)null);
            _bookingItemRepo.Setup(r => r.GetByBookingId(1)).ReturnsAsync(new List<BookingItem>());

            var result = await _sut.GetById(1, 1);

            result.EventTitle.Should().Be("");
        }

        // ── ValidateQr ───────────────────────────────────────────────────────────

        [Test]
        public async Task ValidateQr_ValidConfirmedBooking_ReturnsTrueAndSetsAttended()
        {
            var booking = MakeBooking(status: "Confirmed");
            booking.QrPayload = "qr_payload_abc";
            _bookingRepo.Setup(r => r.GetAll()).ReturnsAsync(new List<Booking> { booking });
            _bookingRepo.Setup(r => r.Update(It.IsAny<Booking>())).ReturnsAsync(booking);

            var result = await _sut.ValidateQr(new ValidateQrRequest { QrPayload = "qr_payload_abc", ScannedBy = 5 });

            result.Should().BeTrue();
            booking.BookingStatus.Should().Be("Attended");
        }

        [Test]
        public async Task ValidateQr_NotFound_ReturnsFalse()
        {
            _bookingRepo.Setup(r => r.GetAll()).ReturnsAsync(new List<Booking>());

            var result = await _sut.ValidateQr(new ValidateQrRequest { QrPayload = "bad_payload" });

            result.Should().BeFalse();
        }

        [Test]
        public async Task ValidateQr_NotConfirmed_ReturnsFalse()
        {
            var booking = MakeBooking(status: "Pending");
            booking.QrPayload = "qr_payload_abc";
            _bookingRepo.Setup(r => r.GetAll()).ReturnsAsync(new List<Booking> { booking });

            var result = await _sut.ValidateQr(new ValidateQrRequest { QrPayload = "qr_payload_abc" });

            result.Should().BeFalse();
        }
    }
}
