using AutoMapper;
using EMSBLLLibrary.Interfaces;
using EMSBLLLibrary.Mappings;
using EMSBLLLibrary.Services;
using EMSDALLibrary.Contexts;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.DTOs;
using EMSModelLibrary.Exceptions;
using EMSModelLibrary.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using NUnit.Framework;

namespace EMSTests.Services
{
    [TestFixture]
    public class SeatReservationServiceTests
    {
        private Mock<ISeatReservationRepository> _reservationRepo;
        private Mock<IEventRepository> _eventRepo;
        private Mock<ISeatRepository> _seatRepo;
        private Mock<ITicketTypeRepository> _ticketTypeRepo;
        private Mock<ISeatNotifier> _notifier;
        private IMapper _mapper;

        private static readonly DateTime Future = DateTime.UtcNow.AddDays(5);

        [SetUp]
        public void SetUp()
        {
            _reservationRepo = new Mock<ISeatReservationRepository>();
            _eventRepo = new Mock<IEventRepository>();
            _seatRepo = new Mock<ISeatRepository>();
            _ticketTypeRepo = new Mock<ITicketTypeRepository>();
            _ticketTypeRepo.Setup(r => r.GetById(It.IsAny<int>()))
                .ReturnsAsync(new TicketType { Id = 1, EventId = 1 });
            _notifier = new Mock<ISeatNotifier>();
            _mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>(), Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance).CreateMapper();
        }

        private EventContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<EventContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new EventContext(options);
        }

        private SeatReservationService CreateSut(EventContext context) =>
            new SeatReservationService(_reservationRepo.Object, _eventRepo.Object, _seatRepo.Object,
                _ticketTypeRepo.Object, _notifier.Object, context, _mapper);

        private Event PublishedEvent() => new Event
        {
            Id = 1, Status = "Published", StartTime = Future, EndTime = Future.AddHours(2)
        };

        // ── Reserve – pre-transaction validation ─────────────────────────────────

        [Test]
        public async Task Reserve_EventNotFound_ThrowsNotFoundException()
        {
            _eventRepo.Setup(r => r.GetById(99)).ReturnsAsync((Event?)null);
            using var ctx = CreateContext();
            var sut = CreateSut(ctx);

            await sut.Invoking(s => s.Reserve(1, new ReserveSeatRequest { EventId = 99, SeatId = 1, TicketTypeId = 1 }))
                .Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task Reserve_EventNotPublished_ThrowsValidationException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(new Event { Id = 1, Status = "Draft", StartTime = Future });
            using var ctx = CreateContext();
            var sut = CreateSut(ctx);

            await sut.Invoking(s => s.Reserve(1, new ReserveSeatRequest { EventId = 1, SeatId = 1, TicketTypeId = 1 }))
                .Should().ThrowAsync<ValidationException>().WithMessage("*published*");
        }

        [Test]
        public async Task Reserve_EventAlreadyStarted_ThrowsValidationException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(new Event
            {
                Id = 1, Status = "Published", StartTime = DateTime.UtcNow.AddMinutes(-1)
            });
            using var ctx = CreateContext();
            var sut = CreateSut(ctx);

            await sut.Invoking(s => s.Reserve(1, new ReserveSeatRequest { EventId = 1, SeatId = 1, TicketTypeId = 1 }))
                .Should().ThrowAsync<ValidationException>().WithMessage("*already started*");
        }

        [Test]
        public async Task Reserve_SeatNotFound_ThrowsNotFoundException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(PublishedEvent());
            _seatRepo.Setup(r => r.GetById(99)).ReturnsAsync((Seat?)null);
            using var ctx = CreateContext();
            var sut = CreateSut(ctx);

            await sut.Invoking(s => s.Reserve(1, new ReserveSeatRequest { EventId = 1, SeatId = 99, TicketTypeId = 1 }))
                .Should().ThrowAsync<NotFoundException>();
        }

        // ── Reserve – database validation ────────────────────────────────────────

        [Test]
        public async Task Reserve_SeatAlreadyReserved_ThrowsValidationException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(PublishedEvent());
            _seatRepo.Setup(r => r.GetById(1)).ReturnsAsync(new Seat { Id = 1 });
            _notifier.Setup(n => n.SeatReserved(It.IsAny<int>(), It.IsAny<int>())).Returns(Task.CompletedTask);

            using var ctx = CreateContext();
            ctx.SeatReservations.Add(new SeatReservation
            {
                Id = 1, EventId = 1, SeatId = 1, UserId = 2, TicketTypeId = 1,
                Status = "Active", ReservedUntil = DateTime.UtcNow.AddMinutes(10)
            });
            await ctx.SaveChangesAsync();

            var sut = CreateSut(ctx);

            await sut.Invoking(s => s.Reserve(1, new ReserveSeatRequest { EventId = 1, SeatId = 1, TicketTypeId = 1 }))
                .Should().ThrowAsync<ValidationException>().WithMessage("*already reserved*");
        }

        [Test]
        public async Task Reserve_SeatAlreadyBooked_ThrowsValidationException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(PublishedEvent());
            _seatRepo.Setup(r => r.GetById(1)).ReturnsAsync(new Seat { Id = 1 });

            using var ctx = CreateContext();
            var booking = new Booking
            {
                Id = 1, UserId = 1, EventId = 1, BookingStatus = "Confirmed",
                BookingReference = "BK12345678", ExpiresAt = DateTime.UtcNow.AddMinutes(30)
            };
            var ticketType = new TicketType
            {
                Id = 1, EventId = 1, Name = "VIP", SeatType = "VIP",
                Price = 500, TotalQuantity = 100, AvailableQuantity = 100,
                SaleStart = DateTime.UtcNow, SaleEnd = DateTime.UtcNow.AddDays(10)
            };
            var seat = new Seat { Id = 1, VenueId = 1, SeatType = "VIP", Section = "A", Row = "1", SeatNumber = 1 };
            ctx.Seats.Add(seat);
            ctx.TicketTypes.Add(ticketType);
            ctx.Bookings.Add(booking);
            await ctx.SaveChangesAsync();

            ctx.BookingItems.Add(new BookingItem
            {
                Id = 1, BookingId = booking.Id, TicketTypeId = ticketType.Id, SeatId = 1,
                UnitPrice = 500, TicketStatus = "Active"
            });
            await ctx.SaveChangesAsync();

            var sut = CreateSut(ctx);

            await sut.Invoking(s => s.Reserve(1, new ReserveSeatRequest { EventId = 1, SeatId = 1, TicketTypeId = 1 }))
                .Should().ThrowAsync<ValidationException>().WithMessage("*already booked*");
        }

        [Test]
        public async Task Reserve_ValidRequest_CreatesReservationAndNotifies()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(PublishedEvent());
            _seatRepo.Setup(r => r.GetById(1)).ReturnsAsync(new Seat { Id = 1 });
            _notifier.Setup(n => n.SeatReserved(1, 1)).Returns(Task.CompletedTask);

            using var ctx = CreateContext();
            var sut = CreateSut(ctx);

            var result = await sut.Reserve(1, new ReserveSeatRequest { EventId = 1, SeatId = 1, TicketTypeId = 1 });

            result.Should().NotBeNull();
            result.Status.Should().Be("Active");
            _notifier.Verify(n => n.SeatReserved(1, 1), Times.Once);
        }

        // ── Release ───────────────────────────────────────────────────────────────

        [Test]
        public async Task Release_OwnActiveReservation_SetsReleasedAndNotifies()
        {
            var reservation = new SeatReservation
            {
                Id = 1, SeatId = 1, EventId = 1, UserId = 1, Status = "Active",
                ReservedUntil = DateTime.UtcNow.AddMinutes(10)
            };
            _reservationRepo.Setup(r => r.GetById(1)).ReturnsAsync(reservation);
            _reservationRepo.Setup(r => r.Update(It.IsAny<SeatReservation>())).ReturnsAsync(reservation);
            _notifier.Setup(n => n.SeatReleased(1, 1)).Returns(Task.CompletedTask);

            using var ctx = CreateContext();
            var sut = CreateSut(ctx);

            await sut.Release(1, 1);

            reservation.Status.Should().Be("Released");
            _notifier.Verify(n => n.SeatReleased(1, 1), Times.Once);
        }

        [Test]
        public async Task Release_NotFound_ThrowsNotFoundException()
        {
            _reservationRepo.Setup(r => r.GetById(99)).ReturnsAsync((SeatReservation?)null);
            using var ctx = CreateContext();
            var sut = CreateSut(ctx);

            await sut.Invoking(s => s.Release(99, 1)).Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task Release_WrongUser_ThrowsUnauthorizedException()
        {
            _reservationRepo.Setup(r => r.GetById(1)).ReturnsAsync(new SeatReservation { Id = 1, UserId = 2, Status = "Active" });
            using var ctx = CreateContext();
            var sut = CreateSut(ctx);

            await sut.Invoking(s => s.Release(1, 1)).Should().ThrowAsync<UnauthorizedException>();
        }

        [Test]
        public async Task Release_NotActive_ThrowsValidationException()
        {
            _reservationRepo.Setup(r => r.GetById(1)).ReturnsAsync(new SeatReservation { Id = 1, UserId = 1, Status = "Released" });
            using var ctx = CreateContext();
            var sut = CreateSut(ctx);

            await sut.Invoking(s => s.Release(1, 1)).Should().ThrowAsync<ValidationException>().WithMessage("*not active*");
        }
    }
}
