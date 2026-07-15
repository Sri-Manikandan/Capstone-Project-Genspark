using AutoMapper;
using EMSBLLLibrary.Mappings;
using EMSBLLLibrary.Services;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.DTOs;
using EMSModelLibrary.Exceptions;
using EMSModelLibrary.Models;
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace EMSTests.Services
{
    [TestFixture]
    public class ScreeningServiceTests
    {
        private Mock<IScreeningRepository> _screeningRepo;
        private Mock<IEventRepository> _eventRepo;
        private Mock<ISeatRepository> _seatRepo;
        private IMapper _mapper;
        private ScreeningService _sut;

        private static readonly DateTime Start = DateTime.UtcNow.AddDays(3);
        private static readonly DateTime End = DateTime.UtcNow.AddDays(3).AddHours(2);

        [SetUp]
        public void SetUp()
        {
            _screeningRepo = new Mock<IScreeningRepository>();
            _eventRepo = new Mock<IEventRepository>();
            _seatRepo = new Mock<ISeatRepository>();
            _seatRepo.Setup(r => r.GetByVenueId(1)).ReturnsAsync(new List<Seat> { new Seat { Id = 1, VenueId = 1, Section = "A", SeatType = "Silver" } });
            // Create/Update/Delete recompute the event window from its screenings; default to none.
            _screeningRepo.Setup(r => r.GetByEventId(It.IsAny<int>())).ReturnsAsync(new List<Screening>());
            _eventRepo.Setup(r => r.Update(It.IsAny<Event>())).ReturnsAsync((Event e) => e);
            _mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>(), Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance).CreateMapper();
            _sut = new ScreeningService(_screeningRepo.Object, _eventRepo.Object, _seatRepo.Object, _mapper);
        }

        private Event OrganizerEvent(int organizerId = 10) => new Event
        {
            Id = 1, OrganizerId = organizerId, VenueId = 1,
            StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddDays(30)
        };

        private CreateScreeningRequest ValidCreate() => new CreateScreeningRequest
        {
            EventId = 1, Screen = "Screen 1", StartTime = Start, EndTime = End
        };

        [Test]
        public async Task Create_Valid_ReturnsDto()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(OrganizerEvent());
            _screeningRepo.Setup(r => r.Add(It.IsAny<Screening>())).ReturnsAsync((Screening s) => s);

            var result = await _sut.Create(10, ValidCreate());

            result.Screen.Should().Be("Screen 1");
            _screeningRepo.Verify(r => r.Add(It.Is<Screening>(s => s.Screen == "Screen 1" && s.EventId == 1)), Times.Once);
        }

        [Test]
        public async Task Create_EventNotFound_ThrowsNotFoundException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync((Event?)null);

            await _sut.Invoking(s => s.Create(10, ValidCreate())).Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task Create_WrongOrganizer_ThrowsUnauthorizedException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(OrganizerEvent(organizerId: 10));

            await _sut.Invoking(s => s.Create(99, ValidCreate())).Should().ThrowAsync<UnauthorizedException>();
        }

        [Test]
        public async Task Create_EmptyScreen_ThrowsValidationException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(OrganizerEvent());

            await _sut.Invoking(s => s.Create(10, new CreateScreeningRequest { EventId = 1, Screen = "  ", StartTime = Start, EndTime = End }))
                .Should().ThrowAsync<ValidationException>().WithMessage("*Screen*");
        }

        [Test]
        public async Task Create_VenueHasNoSeats_ThrowsValidationException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(OrganizerEvent());
            _seatRepo.Setup(r => r.GetByVenueId(1)).ReturnsAsync(new List<Seat>());

            await _sut.Invoking(s => s.Create(10, ValidCreate()))
                .Should().ThrowAsync<ValidationException>().WithMessage("*no seats*");
        }

        [Test]
        public async Task Create_EndBeforeStart_ThrowsValidationException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(OrganizerEvent());

            await _sut.Invoking(s => s.Create(10, new CreateScreeningRequest { EventId = 1, Screen = "Screen 1", StartTime = End, EndTime = Start }))
                .Should().ThrowAsync<ValidationException>().WithMessage("*EndTime*");
        }

        [Test]
        public async Task Create_StartInPast_ThrowsValidationException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(OrganizerEvent());
            var past = DateTime.UtcNow.AddDays(-1);

            await _sut.Invoking(s => s.Create(10, new CreateScreeningRequest { EventId = 1, Screen = "Screen 1", StartTime = past, EndTime = past.AddHours(2) }))
                .Should().ThrowAsync<ValidationException>().WithMessage("*future*");
        }

        [Test]
        public async Task Create_OutsideOriginalEventWindow_IsAllowed_AndWidensWindow()
        {
            // The event window is derived from its screenings, so a screening outside the old
            // window is fine — it just extends the window rather than being rejected.
            var ev = OrganizerEvent();
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(ev);
            _screeningRepo.Setup(r => r.Add(It.IsAny<Screening>())).ReturnsAsync((Screening s) => s);
            var outsideStart = DateTime.UtcNow.AddDays(40);
            var outsideEnd = outsideStart.AddHours(2);
            _screeningRepo.Setup(r => r.GetByEventId(1)).ReturnsAsync(new List<Screening>
            {
                new Screening { Id = 7, EventId = 1, StartTime = outsideStart, EndTime = outsideEnd }
            });

            await _sut.Create(10, new CreateScreeningRequest { EventId = 1, Screen = "Screen 1", StartTime = outsideStart, EndTime = outsideEnd });

            ev.StartTime.Should().Be(outsideStart);
            ev.EndTime.Should().Be(outsideEnd);
            _eventRepo.Verify(r => r.Update(It.Is<Event>(e => e.Id == 1)), Times.Once);
        }

        [Test]
        public async Task Create_RecomputesEventWindowAsScreeningSpan()
        {
            var ev = OrganizerEvent();
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(ev);
            _screeningRepo.Setup(r => r.Add(It.IsAny<Screening>())).ReturnsAsync((Screening s) => s);
            var earliest = DateTime.UtcNow.AddDays(2);
            var latest = DateTime.UtcNow.AddDays(9);
            _screeningRepo.Setup(r => r.GetByEventId(1)).ReturnsAsync(new List<Screening>
            {
                new Screening { Id = 1, EventId = 1, StartTime = earliest, EndTime = earliest.AddHours(2) },
                new Screening { Id = 2, EventId = 1, StartTime = latest.AddHours(-2), EndTime = latest },
            });

            await _sut.Create(10, ValidCreate());

            ev.StartTime.Should().Be(earliest);
            ev.EndTime.Should().Be(latest);
        }

        [Test]
        public async Task Delete_RecomputesEventWindowFromRemainingScreenings()
        {
            _screeningRepo.Setup(r => r.GetById(1)).ReturnsAsync(new Screening { Id = 1, EventId = 1 });
            var ev = OrganizerEvent();
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(ev);
            _screeningRepo.Setup(r => r.HasActivity(1)).ReturnsAsync(false);
            _screeningRepo.Setup(r => r.Delete(1)).Returns(Task.CompletedTask);
            var remainingStart = DateTime.UtcNow.AddDays(5);
            var remainingEnd = remainingStart.AddHours(3);
            _screeningRepo.Setup(r => r.GetByEventId(1)).ReturnsAsync(new List<Screening>
            {
                new Screening { Id = 2, EventId = 1, StartTime = remainingStart, EndTime = remainingEnd }
            });

            await _sut.Delete(1, 10);

            ev.StartTime.Should().Be(remainingStart);
            ev.EndTime.Should().Be(remainingEnd);
        }

        [Test]
        public async Task GetByEventId_ReturnsMappedList()
        {
            _screeningRepo.Setup(r => r.GetByEventId(1)).ReturnsAsync(new List<Screening> { new Screening { Id = 1, EventId = 1 } });

            var result = await _sut.GetByEventId(1);

            result.Should().HaveCount(1);
        }

        [Test]
        public async Task GetById_NotFound_ThrowsNotFoundException()
        {
            _screeningRepo.Setup(r => r.GetById(99)).ReturnsAsync((Screening?)null);

            await _sut.Invoking(s => s.GetById(99)).Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task Update_HasActivity_ThrowsValidationException()
        {
            _screeningRepo.Setup(r => r.GetById(1)).ReturnsAsync(new Screening { Id = 1, EventId = 1 });
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(OrganizerEvent());
            _screeningRepo.Setup(r => r.HasActivity(1)).ReturnsAsync(true);

            await _sut.Invoking(s => s.Update(1, 10, new UpdateScreeningRequest { Screen = "Screen 1", StartTime = Start, EndTime = End }))
                .Should().ThrowAsync<ValidationException>().WithMessage("*bookings*");
        }

        [Test]
        public async Task Delete_HasActivity_ThrowsValidationException()
        {
            _screeningRepo.Setup(r => r.GetById(1)).ReturnsAsync(new Screening { Id = 1, EventId = 1 });
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(OrganizerEvent());
            _screeningRepo.Setup(r => r.HasActivity(1)).ReturnsAsync(true);

            await _sut.Invoking(s => s.Delete(1, 10)).Should().ThrowAsync<ValidationException>().WithMessage("*bookings*");
        }

        [Test]
        public async Task Delete_Valid_CallsDelete()
        {
            _screeningRepo.Setup(r => r.GetById(1)).ReturnsAsync(new Screening { Id = 1, EventId = 1 });
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(OrganizerEvent());
            _screeningRepo.Setup(r => r.HasActivity(1)).ReturnsAsync(false);
            _screeningRepo.Setup(r => r.Delete(1)).Returns(Task.CompletedTask);

            await _sut.Delete(1, 10);

            _screeningRepo.Verify(r => r.Delete(1), Times.Once);
        }
    }
}
