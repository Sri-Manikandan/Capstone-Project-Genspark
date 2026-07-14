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
    public class TicketTypeServiceTests
    {
        private Mock<ITicketTypeRepository> _ttRepo;
        private Mock<IScreeningRepository> _screeningRepo;
        private Mock<IEventRepository> _eventRepo;
        private Mock<ISeatRepository> _seatRepo;
        private IMapper _mapper;
        private TicketTypeService _sut;

        [SetUp]
        public void SetUp()
        {
            _ttRepo = new Mock<ITicketTypeRepository>();
            _screeningRepo = new Mock<IScreeningRepository>();
            _eventRepo = new Mock<IEventRepository>();
            _seatRepo = new Mock<ISeatRepository>();
            _mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>(), Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance).CreateMapper();
            _sut = new TicketTypeService(_ttRepo.Object, _screeningRepo.Object, _eventRepo.Object, _seatRepo.Object, _mapper);
        }

        private Event OrganizerEvent(int organizerId = 10, int venueId = 1) => new Event
        {
            Id = 1, OrganizerId = organizerId, VenueId = venueId, Status = "Draft"
        };

        private Screening MakeScreening(int id = 1, int eventId = 1) => new Screening
        {
            Id = id, EventId = eventId, Screen = "Screen 1", Status = "Scheduled",
            StartTime = DateTime.UtcNow.AddDays(15), EndTime = DateTime.UtcNow.AddDays(15).AddHours(3)
        };

        // Wires up screening → event for the common happy path.
        private void SetupScreeningAndEvent(int screeningId = 1, int organizerId = 10)
        {
            _screeningRepo.Setup(r => r.GetById(screeningId)).ReturnsAsync(MakeScreening(screeningId));
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(OrganizerEvent(organizerId));
        }

        private CreateTicketTypeRequest ValidCreateRequest(string seatType = "VIP") => new CreateTicketTypeRequest
        {
            ScreeningId = 1, Name = "VIP", SeatType = seatType, Price = 500,
            SaleStart = DateTime.UtcNow, SaleEnd = DateTime.UtcNow.AddDays(10)
        };

        // ── Create ───────────────────────────────────────────────────────────────

        [Test]
        public async Task Create_ValidRequest_ReturnsDto()
        {
            SetupScreeningAndEvent();
            _seatRepo.Setup(r => r.CountByVenueSectionAndType(1, "Screen 1", "VIP")).ReturnsAsync(100);
            _ttRepo.Setup(r => r.GetByScreeningId(1)).ReturnsAsync(new List<TicketType>());
            _ttRepo.Setup(r => r.Add(It.IsAny<TicketType>())).ReturnsAsync((TicketType t) => t);

            var result = await _sut.Create(10, ValidCreateRequest());

            result.Name.Should().Be("VIP");
        }

        [Test]
        public async Task Create_SetsQuantityToSeatTypeCapacity()
        {
            SetupScreeningAndEvent();
            _seatRepo.Setup(r => r.CountByVenueSectionAndType(1, "Screen 1", "VIP")).ReturnsAsync(100);
            _ttRepo.Setup(r => r.GetByScreeningId(1)).ReturnsAsync(new List<TicketType>());
            _ttRepo.Setup(r => r.Add(It.IsAny<TicketType>())).ReturnsAsync((TicketType t) => t);

            var result = await _sut.Create(10, ValidCreateRequest());

            result.TotalQuantity.Should().Be(100);
            result.AvailableQuantity.Should().Be(100);
        }

        [Test]
        public async Task Create_ScreeningNotFound_ThrowsNotFoundException()
        {
            _screeningRepo.Setup(r => r.GetById(99)).ReturnsAsync((Screening?)null);

            var request = ValidCreateRequest();
            request.ScreeningId = 99;

            await _sut.Invoking(s => s.Create(10, request)).Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task Create_WrongOrganizer_ThrowsUnauthorizedException()
        {
            SetupScreeningAndEvent(organizerId: 10);

            await _sut.Invoking(s => s.Create(99, ValidCreateRequest())).Should().ThrowAsync<UnauthorizedException>();
        }

        [Test]
        public async Task Create_SaleEndBeforeSaleStart_ThrowsValidationException()
        {
            SetupScreeningAndEvent();
            var now = DateTime.UtcNow;
            var request = ValidCreateRequest();
            request.SaleStart = now.AddDays(5);
            request.SaleEnd = now;

            await _sut.Invoking(s => s.Create(10, request))
                .Should().ThrowAsync<ValidationException>().WithMessage("*SaleEnd*");
        }

        [Test]
        public async Task Create_SaleEndsAfterScreeningStart_ThrowsValidationException()
        {
            // Screening starts in 15 days; a sale window ending in 20 days closes after the show begins.
            SetupScreeningAndEvent();
            var request = ValidCreateRequest();
            request.SaleStart = DateTime.UtcNow;
            request.SaleEnd = DateTime.UtcNow.AddDays(20);

            await _sut.Invoking(s => s.Create(10, request))
                .Should().ThrowAsync<ValidationException>().WithMessage("*before the screening*");
        }

        [Test]
        public async Task Create_DuplicateSeatType_ThrowsValidationException()
        {
            SetupScreeningAndEvent();
            _ttRepo.Setup(r => r.GetByScreeningId(1)).ReturnsAsync(new List<TicketType>
            {
                new TicketType { Id = 2, SeatType = "VIP", TotalQuantity = 10 }
            });

            await _sut.Invoking(s => s.Create(10, ValidCreateRequest("VIP")))
                .Should().ThrowAsync<ValidationException>().WithMessage("*already exists*");
        }

        [Test]
        public async Task Create_NoSeatsOfType_ThrowsValidationException()
        {
            SetupScreeningAndEvent();
            _seatRepo.Setup(r => r.CountByVenueSectionAndType(1, "Screen 1", "VIP")).ReturnsAsync(0);
            _ttRepo.Setup(r => r.GetByScreeningId(1)).ReturnsAsync(new List<TicketType>());

            await _sut.Invoking(s => s.Create(10, ValidCreateRequest()))
                .Should().ThrowAsync<ValidationException>().WithMessage("*No seats*");
        }

        [Test]
        public async Task Create_ScopesQuantityToTheScreeningsScreen_NotTheWholeVenue()
        {
            SetupScreeningAndEvent(); // screening.Screen == "Screen 1"
            // Screen 1 has 40 VIP seats; the venue has 100 VIP seats across all screens.
            _seatRepo.Setup(r => r.CountByVenueSectionAndType(1, "Screen 1", "VIP")).ReturnsAsync(40);
            _seatRepo.Setup(r => r.CountByVenueAndType(1, "VIP")).ReturnsAsync(100);
            _ttRepo.Setup(r => r.GetByScreeningId(1)).ReturnsAsync(new List<TicketType>());
            _ttRepo.Setup(r => r.Add(It.IsAny<TicketType>())).ReturnsAsync((TicketType t) => t);

            var result = await _sut.Create(10, ValidCreateRequest());

            result.TotalQuantity.Should().Be(40);
            _seatRepo.Verify(r => r.CountByVenueAndType(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task Create_WholeVenueScreening_CountsSeatsAcrossEveryScreen()
        {
            // A screening with no specific screen represents a whole-venue event.
            var screening = MakeScreening();
            screening.Screen = "";
            _screeningRepo.Setup(r => r.GetById(1)).ReturnsAsync(screening);
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(OrganizerEvent(10));
            _seatRepo.Setup(r => r.CountByVenueAndType(1, "VIP")).ReturnsAsync(100);
            _ttRepo.Setup(r => r.GetByScreeningId(1)).ReturnsAsync(new List<TicketType>());
            _ttRepo.Setup(r => r.Add(It.IsAny<TicketType>())).ReturnsAsync((TicketType t) => t);

            var result = await _sut.Create(10, ValidCreateRequest());

            result.TotalQuantity.Should().Be(100);
        }

        // ── GetById / GetByScreeningId / GetActiveByScreeningId ───────────────────

        [Test]
        public async Task GetById_Found_ReturnsDto()
        {
            _ttRepo.Setup(r => r.GetById(1)).ReturnsAsync(new TicketType { Id = 1, Name = "VIP" });

            var result = await _sut.GetById(1);

            result.Name.Should().Be("VIP");
        }

        [Test]
        public async Task GetById_NotFound_ThrowsNotFoundException()
        {
            _ttRepo.Setup(r => r.GetById(99)).ReturnsAsync((TicketType?)null);

            await _sut.Invoking(s => s.GetById(99)).Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task GetByScreeningId_ReturnsMappedList()
        {
            _ttRepo.Setup(r => r.GetByScreeningId(1)).ReturnsAsync(new List<TicketType> { new TicketType { Id = 1 } });

            var result = await _sut.GetByScreeningId(1);

            result.Should().HaveCount(1);
        }

        [Test]
        public async Task GetActiveByScreeningId_ReturnsMappedList()
        {
            _ttRepo.Setup(r => r.GetActiveByScreeningId(1)).ReturnsAsync(new List<TicketType> { new TicketType { Id = 1 } });

            var result = await _sut.GetActiveByScreeningId(1);

            result.Should().HaveCount(1);
        }

        // ── Update ───────────────────────────────────────────────────────────────

        private UpdateTicketTypeRequest ValidUpdateRequest(string seatType = "VIP") => new UpdateTicketTypeRequest
        {
            Name = "VIP Ticket", SeatType = seatType, Price = 500, IsActive = true,
            SaleStart = DateTime.UtcNow, SaleEnd = DateTime.UtcNow.AddDays(10)
        };

        [Test]
        public async Task Update_ValidRequest_ReturnsUpdatedDto()
        {
            var tt = new TicketType { Id = 1, ScreeningId = 1, SeatType = "VIP", TotalQuantity = 50, AvailableQuantity = 50 };
            _ttRepo.Setup(r => r.GetById(1)).ReturnsAsync(tt);
            SetupScreeningAndEvent();
            _seatRepo.Setup(r => r.CountByVenueSectionAndType(1, "Screen 1", "VIP")).ReturnsAsync(100);
            _ttRepo.Setup(r => r.Update(It.IsAny<TicketType>())).ReturnsAsync((TicketType t) => t);

            var request = ValidUpdateRequest();
            request.Name = "VIP+";
            var result = await _sut.Update(1, 10, request);

            result.Name.Should().Be("VIP+");
        }

        [Test]
        public async Task Update_SameSeatType_RecomputesQuantityKeepingSold()
        {
            // 10 already sold (50 total, 40 available); capacity recomputes to 100.
            var tt = new TicketType { Id = 1, ScreeningId = 1, SeatType = "VIP", TotalQuantity = 50, AvailableQuantity = 40 };
            _ttRepo.Setup(r => r.GetById(1)).ReturnsAsync(tt);
            SetupScreeningAndEvent();
            _seatRepo.Setup(r => r.CountByVenueSectionAndType(1, "Screen 1", "VIP")).ReturnsAsync(100);
            _ttRepo.Setup(r => r.Update(It.IsAny<TicketType>())).ReturnsAsync((TicketType t) => t);

            var result = await _sut.Update(1, 10, ValidUpdateRequest("VIP"));

            result.TotalQuantity.Should().Be(100);
            result.AvailableQuantity.Should().Be(90);
        }

        [Test]
        public async Task Update_SeatTypeChangedNoSales_RemapsQuantity()
        {
            var tt = new TicketType { Id = 1, ScreeningId = 1, SeatType = "VIP", TotalQuantity = 50, AvailableQuantity = 50 };
            _ttRepo.Setup(r => r.GetById(1)).ReturnsAsync(tt);
            SetupScreeningAndEvent();
            _ttRepo.Setup(r => r.GetByScreeningId(1)).ReturnsAsync(new List<TicketType> { tt });
            _seatRepo.Setup(r => r.CountByVenueSectionAndType(1, "Screen 1", "Balcony")).ReturnsAsync(30);
            _ttRepo.Setup(r => r.Update(It.IsAny<TicketType>())).ReturnsAsync((TicketType t) => t);

            var result = await _sut.Update(1, 10, ValidUpdateRequest("Balcony"));

            result.SeatType.Should().Be("Balcony");
            result.TotalQuantity.Should().Be(30);
            result.AvailableQuantity.Should().Be(30);
        }

        [Test]
        public async Task Update_SeatTypeChangedAfterSales_ThrowsValidationException()
        {
            // 10 sold, so the seat type is locked.
            var tt = new TicketType { Id = 1, ScreeningId = 1, SeatType = "VIP", TotalQuantity = 50, AvailableQuantity = 40 };
            _ttRepo.Setup(r => r.GetById(1)).ReturnsAsync(tt);
            SetupScreeningAndEvent();

            await _sut.Invoking(s => s.Update(1, 10, ValidUpdateRequest("Balcony")))
                .Should().ThrowAsync<ValidationException>().WithMessage("*Cannot change seat type*");
        }

        [Test]
        public async Task Update_SeatTypeChangedToDuplicate_ThrowsValidationException()
        {
            var tt = new TicketType { Id = 1, ScreeningId = 1, SeatType = "VIP", TotalQuantity = 50, AvailableQuantity = 50 };
            _ttRepo.Setup(r => r.GetById(1)).ReturnsAsync(tt);
            SetupScreeningAndEvent();
            _ttRepo.Setup(r => r.GetByScreeningId(1)).ReturnsAsync(new List<TicketType>
            {
                tt,
                new TicketType { Id = 2, SeatType = "Gold", TotalQuantity = 20 }
            });

            await _sut.Invoking(s => s.Update(1, 10, ValidUpdateRequest("Gold")))
                .Should().ThrowAsync<ValidationException>().WithMessage("*already exists*");
        }

        [Test]
        public async Task Update_NotFound_ThrowsNotFoundException()
        {
            _ttRepo.Setup(r => r.GetById(99)).ReturnsAsync((TicketType?)null);

            await _sut.Invoking(s => s.Update(99, 10, ValidUpdateRequest())).Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task Update_ScreeningNotFound_ThrowsNotFoundException()
        {
            _ttRepo.Setup(r => r.GetById(1)).ReturnsAsync(new TicketType { Id = 1, ScreeningId = 99 });
            _screeningRepo.Setup(r => r.GetById(99)).ReturnsAsync((Screening?)null);

            await _sut.Invoking(s => s.Update(1, 10, ValidUpdateRequest())).Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task Update_WrongOrganizer_ThrowsUnauthorizedException()
        {
            _ttRepo.Setup(r => r.GetById(1)).ReturnsAsync(new TicketType { Id = 1, ScreeningId = 1 });
            SetupScreeningAndEvent(organizerId: 10);

            await _sut.Invoking(s => s.Update(1, 99, ValidUpdateRequest())).Should().ThrowAsync<UnauthorizedException>();
        }

        [Test]
        public async Task Update_NoSeatsOfType_ThrowsValidationException()
        {
            var tt = new TicketType { Id = 1, ScreeningId = 1, SeatType = "VIP", TotalQuantity = 50, AvailableQuantity = 50 };
            _ttRepo.Setup(r => r.GetById(1)).ReturnsAsync(tt);
            SetupScreeningAndEvent();
            _seatRepo.Setup(r => r.CountByVenueSectionAndType(1, "Screen 1", "VIP")).ReturnsAsync(0);

            await _sut.Invoking(s => s.Update(1, 10, ValidUpdateRequest()))
                .Should().ThrowAsync<ValidationException>().WithMessage("*No seats*");
        }

        // ── Delete ───────────────────────────────────────────────────────────────

        [Test]
        public async Task Delete_OwnTicketType_CallsDelete()
        {
            _ttRepo.Setup(r => r.GetById(1)).ReturnsAsync(new TicketType { Id = 1, ScreeningId = 1 });
            SetupScreeningAndEvent();
            _ttRepo.Setup(r => r.Delete(1)).Returns(Task.CompletedTask);

            await _sut.Delete(1, 10);

            _ttRepo.Verify(r => r.Delete(1), Times.Once);
        }

        [Test]
        public async Task Delete_NotFound_ThrowsNotFoundException()
        {
            _ttRepo.Setup(r => r.GetById(99)).ReturnsAsync((TicketType?)null);

            await _sut.Invoking(s => s.Delete(99, 10)).Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task Delete_ScreeningNotFound_ThrowsNotFoundException()
        {
            _ttRepo.Setup(r => r.GetById(1)).ReturnsAsync(new TicketType { Id = 1, ScreeningId = 99 });
            _screeningRepo.Setup(r => r.GetById(99)).ReturnsAsync((Screening?)null);

            await _sut.Invoking(s => s.Delete(1, 10)).Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task Delete_WrongOrganizer_ThrowsUnauthorizedException()
        {
            _ttRepo.Setup(r => r.GetById(1)).ReturnsAsync(new TicketType { Id = 1, ScreeningId = 1 });
            SetupScreeningAndEvent(organizerId: 10);

            await _sut.Invoking(s => s.Delete(1, 99)).Should().ThrowAsync<UnauthorizedException>();
        }
    }
}
