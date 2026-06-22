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
        private Mock<IEventRepository> _eventRepo;
        private Mock<IVenueRepository> _venueRepo;
        private Mock<ISeatRepository> _seatRepo;
        private IMapper _mapper;
        private TicketTypeService _sut;

        [SetUp]
        public void SetUp()
        {
            _ttRepo = new Mock<ITicketTypeRepository>();
            _eventRepo = new Mock<IEventRepository>();
            _venueRepo = new Mock<IVenueRepository>();
            _seatRepo = new Mock<ISeatRepository>();
            _mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>(), Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance).CreateMapper();
            _sut = new TicketTypeService(_ttRepo.Object, _eventRepo.Object, _venueRepo.Object, _seatRepo.Object, _mapper);
        }

        private Event OrganizerEvent(int organizerId = 10, int venueId = 1) => new Event
        {
            Id = 1, OrganizerId = organizerId, VenueId = venueId, Status = "Draft"
        };

        private Venue MakeVenue(int capacity = 500) => new Venue { Id = 1, TotalCapacity = capacity };

        // ── Create ───────────────────────────────────────────────────────────────

        [Test]
        public async Task Create_ValidRequest_ReturnsDto()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(OrganizerEvent());
            _venueRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeVenue());
            _seatRepo.Setup(r => r.CountByVenueAndType(1, "VIP")).ReturnsAsync(100);
            _ttRepo.Setup(r => r.GetByEventId(1)).ReturnsAsync(new List<TicketType>());
            _ttRepo.Setup(r => r.Add(It.IsAny<TicketType>())).ReturnsAsync((TicketType t) => t);

            var result = await _sut.Create(10, new CreateTicketTypeRequest
            {
                EventId = 1, Name = "VIP", SeatType = "VIP", Price = 500, TotalQuantity = 50,
                SaleStart = DateTime.UtcNow, SaleEnd = DateTime.UtcNow.AddDays(10)
            });

            result.Name.Should().Be("VIP");
        }

        [Test]
        public async Task Create_EventNotFound_ThrowsNotFoundException()
        {
            _eventRepo.Setup(r => r.GetById(99)).ReturnsAsync((Event?)null);

            await _sut.Invoking(s => s.Create(10, new CreateTicketTypeRequest
            {
                EventId = 99, Name = "VIP", SeatType = "VIP", TotalQuantity = 10,
                SaleStart = DateTime.UtcNow, SaleEnd = DateTime.UtcNow.AddDays(1)
            })).Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task Create_WrongOrganizer_ThrowsUnauthorizedException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(OrganizerEvent(organizerId: 10));

            await _sut.Invoking(s => s.Create(99, new CreateTicketTypeRequest
            {
                EventId = 1, Name = "VIP", SeatType = "VIP", TotalQuantity = 10,
                SaleStart = DateTime.UtcNow, SaleEnd = DateTime.UtcNow.AddDays(1)
            })).Should().ThrowAsync<UnauthorizedException>();
        }

        [Test]
        public async Task Create_SaleEndBeforeSaleStart_ThrowsValidationException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(OrganizerEvent());
            var now = DateTime.UtcNow;

            await _sut.Invoking(s => s.Create(10, new CreateTicketTypeRequest
            {
                EventId = 1, Name = "VIP", SeatType = "VIP", TotalQuantity = 10,
                SaleStart = now.AddDays(5), SaleEnd = now
            })).Should().ThrowAsync<ValidationException>().WithMessage("*SaleEnd*");
        }

        [Test]
        public async Task Create_VenueNotFound_ThrowsNotFoundException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(OrganizerEvent());
            _venueRepo.Setup(r => r.GetById(1)).ReturnsAsync((Venue?)null);

            await _sut.Invoking(s => s.Create(10, new CreateTicketTypeRequest
            {
                EventId = 1, Name = "VIP", SeatType = "VIP", TotalQuantity = 10,
                SaleStart = DateTime.UtcNow, SaleEnd = DateTime.UtcNow.AddDays(1)
            })).Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task Create_NoSeatsOfType_ThrowsValidationException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(OrganizerEvent());
            _venueRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeVenue());
            _seatRepo.Setup(r => r.CountByVenueAndType(1, "VIP")).ReturnsAsync(0);

            await _sut.Invoking(s => s.Create(10, new CreateTicketTypeRequest
            {
                EventId = 1, Name = "VIP", SeatType = "VIP", TotalQuantity = 10,
                SaleStart = DateTime.UtcNow, SaleEnd = DateTime.UtcNow.AddDays(1)
            })).Should().ThrowAsync<ValidationException>().WithMessage("*No seats*");
        }

        [Test]
        public async Task Create_ExceedsTypeSeatCount_ThrowsValidationException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(OrganizerEvent());
            _venueRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeVenue());
            _seatRepo.Setup(r => r.CountByVenueAndType(1, "VIP")).ReturnsAsync(10);
            _ttRepo.Setup(r => r.GetByEventId(1)).ReturnsAsync(new List<TicketType>
            {
                new TicketType { SeatType = "VIP", TotalQuantity = 8 }
            });

            await _sut.Invoking(s => s.Create(10, new CreateTicketTypeRequest
            {
                EventId = 1, Name = "VIP", SeatType = "VIP", TotalQuantity = 5,
                SaleStart = DateTime.UtcNow, SaleEnd = DateTime.UtcNow.AddDays(1)
            })).Should().ThrowAsync<ValidationException>().WithMessage("*exceeds available*");
        }

        [Test]
        public async Task Create_ExceedsVenueCapacity_ThrowsValidationException()
        {
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(OrganizerEvent());
            _venueRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeVenue(capacity: 10));
            _seatRepo.Setup(r => r.CountByVenueAndType(1, "VIP")).ReturnsAsync(100);
            _ttRepo.Setup(r => r.GetByEventId(1)).ReturnsAsync(new List<TicketType>
            {
                new TicketType { SeatType = "General", TotalQuantity = 8 }
            });

            await _sut.Invoking(s => s.Create(10, new CreateTicketTypeRequest
            {
                EventId = 1, Name = "VIP", SeatType = "VIP", TotalQuantity = 5,
                SaleStart = DateTime.UtcNow, SaleEnd = DateTime.UtcNow.AddDays(1)
            })).Should().ThrowAsync<ValidationException>().WithMessage("*venue capacity*");
        }

        // ── GetById / GetByEventId / GetActiveByEventId ───────────────────────────

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
        public async Task GetByEventId_ReturnsMappedList()
        {
            _ttRepo.Setup(r => r.GetByEventId(1)).ReturnsAsync(new List<TicketType> { new TicketType { Id = 1 } });

            var result = await _sut.GetByEventId(1);

            result.Should().HaveCount(1);
        }

        [Test]
        public async Task GetActiveByEventId_ReturnsMappedList()
        {
            _ttRepo.Setup(r => r.GetActiveByEventId(1)).ReturnsAsync(new List<TicketType> { new TicketType { Id = 1 } });

            var result = await _sut.GetActiveByEventId(1);

            result.Should().HaveCount(1);
        }

        // ── Update ───────────────────────────────────────────────────────────────

        [Test]
        public async Task Update_ValidRequest_ReturnsUpdatedDto()
        {
            var tt = new TicketType { Id = 1, EventId = 1, SeatType = "VIP", TotalQuantity = 50, AvailableQuantity = 50 };
            _ttRepo.Setup(r => r.GetById(1)).ReturnsAsync(tt);
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(OrganizerEvent());
            _venueRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeVenue());
            _seatRepo.Setup(r => r.CountByVenueAndType(1, "VIP")).ReturnsAsync(100);
            _ttRepo.Setup(r => r.GetByEventId(1)).ReturnsAsync(new List<TicketType> { tt });
            _ttRepo.Setup(r => r.Update(It.IsAny<TicketType>())).ReturnsAsync((TicketType t) => t);

            var result = await _sut.Update(1, 10, new UpdateTicketTypeRequest
            {
                Name = "VIP+", SeatType = "VIP", Price = 600, TotalQuantity = 60, IsActive = true,
                SaleStart = DateTime.UtcNow, SaleEnd = DateTime.UtcNow.AddDays(10)
            });

            result.Name.Should().Be("VIP+");
        }

        private UpdateTicketTypeRequest ValidUpdateRequest(int qty = 50, string seatType = "VIP") => new UpdateTicketTypeRequest
        {
            Name = "VIP Ticket", SeatType = seatType, Price = 500, TotalQuantity = qty, IsActive = true,
            SaleStart = DateTime.UtcNow, SaleEnd = DateTime.UtcNow.AddDays(10)
        };

        [Test]
        public async Task Update_NotFound_ThrowsNotFoundException()
        {
            _ttRepo.Setup(r => r.GetById(99)).ReturnsAsync((TicketType?)null);

            await _sut.Invoking(s => s.Update(99, 10, ValidUpdateRequest())).Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task Update_EventNotFound_ThrowsNotFoundException()
        {
            _ttRepo.Setup(r => r.GetById(1)).ReturnsAsync(new TicketType { Id = 1, EventId = 99 });
            _eventRepo.Setup(r => r.GetById(99)).ReturnsAsync((Event?)null);

            await _sut.Invoking(s => s.Update(1, 10, ValidUpdateRequest())).Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task Update_WrongOrganizer_ThrowsUnauthorizedException()
        {
            _ttRepo.Setup(r => r.GetById(1)).ReturnsAsync(new TicketType { Id = 1, EventId = 1 });
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(OrganizerEvent(organizerId: 10));

            await _sut.Invoking(s => s.Update(1, 99, ValidUpdateRequest())).Should().ThrowAsync<UnauthorizedException>();
        }

        [Test]
        public async Task Update_QuantityBelowSold_ThrowsValidationException()
        {
            var tt = new TicketType { Id = 1, EventId = 1, TotalQuantity = 50, AvailableQuantity = 40 };
            _ttRepo.Setup(r => r.GetById(1)).ReturnsAsync(tt);
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(OrganizerEvent());

            await _sut.Invoking(s => s.Update(1, 10, ValidUpdateRequest(qty: 5)))
                .Should().ThrowAsync<ValidationException>().WithMessage("*Cannot reduce*");
        }

        [Test]
        public async Task Update_VenueNotFound_ThrowsNotFoundException()
        {
            var tt = new TicketType { Id = 1, EventId = 1, TotalQuantity = 50, AvailableQuantity = 50 };
            _ttRepo.Setup(r => r.GetById(1)).ReturnsAsync(tt);
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(OrganizerEvent());
            _venueRepo.Setup(r => r.GetById(1)).ReturnsAsync((Venue?)null);

            await _sut.Invoking(s => s.Update(1, 10, ValidUpdateRequest())).Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task Update_NoSeatsOfType_ThrowsValidationException()
        {
            var tt = new TicketType { Id = 1, EventId = 1, TotalQuantity = 50, AvailableQuantity = 50 };
            _ttRepo.Setup(r => r.GetById(1)).ReturnsAsync(tt);
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(OrganizerEvent());
            _venueRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeVenue());
            _seatRepo.Setup(r => r.CountByVenueAndType(1, "VIP")).ReturnsAsync(0);

            await _sut.Invoking(s => s.Update(1, 10, ValidUpdateRequest()))
                .Should().ThrowAsync<ValidationException>().WithMessage("*No seats*");
        }

        [Test]
        public async Task Update_ExceedsTypeSeatCount_ThrowsValidationException()
        {
            var tt = new TicketType { Id = 1, EventId = 1, SeatType = "VIP", TotalQuantity = 10, AvailableQuantity = 10 };
            _ttRepo.Setup(r => r.GetById(1)).ReturnsAsync(tt);
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(OrganizerEvent());
            _venueRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeVenue());
            _seatRepo.Setup(r => r.CountByVenueAndType(1, "VIP")).ReturnsAsync(10);
            _ttRepo.Setup(r => r.GetByEventId(1)).ReturnsAsync(new List<TicketType>
            {
                tt,
                new TicketType { Id = 2, SeatType = "VIP", TotalQuantity = 8 }
            });

            await _sut.Invoking(s => s.Update(1, 10, ValidUpdateRequest(qty: 5)))
                .Should().ThrowAsync<ValidationException>().WithMessage("*exceeds available*");
        }

        [Test]
        public async Task Update_ExceedsVenueCapacity_ThrowsValidationException()
        {
            var tt = new TicketType { Id = 1, EventId = 1, SeatType = "VIP", TotalQuantity = 5, AvailableQuantity = 5 };
            _ttRepo.Setup(r => r.GetById(1)).ReturnsAsync(tt);
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(OrganizerEvent());
            _venueRepo.Setup(r => r.GetById(1)).ReturnsAsync(MakeVenue(capacity: 10));
            _seatRepo.Setup(r => r.CountByVenueAndType(1, "VIP")).ReturnsAsync(100);
            _ttRepo.Setup(r => r.GetByEventId(1)).ReturnsAsync(new List<TicketType>
            {
                tt,
                new TicketType { Id = 2, SeatType = "General", TotalQuantity = 8 }
            });

            await _sut.Invoking(s => s.Update(1, 10, ValidUpdateRequest(qty: 5)))
                .Should().ThrowAsync<ValidationException>().WithMessage("*venue capacity*");
        }

        // ── Delete ───────────────────────────────────────────────────────────────

        [Test]
        public async Task Delete_OwnTicketType_CallsDelete()
        {
            _ttRepo.Setup(r => r.GetById(1)).ReturnsAsync(new TicketType { Id = 1, EventId = 1 });
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(OrganizerEvent());
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
        public async Task Delete_EventNotFound_ThrowsNotFoundException()
        {
            _ttRepo.Setup(r => r.GetById(1)).ReturnsAsync(new TicketType { Id = 1, EventId = 99 });
            _eventRepo.Setup(r => r.GetById(99)).ReturnsAsync((Event?)null);

            await _sut.Invoking(s => s.Delete(1, 10)).Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task Delete_WrongOrganizer_ThrowsUnauthorizedException()
        {
            _ttRepo.Setup(r => r.GetById(1)).ReturnsAsync(new TicketType { Id = 1, EventId = 1 });
            _eventRepo.Setup(r => r.GetById(1)).ReturnsAsync(OrganizerEvent(organizerId: 10));

            await _sut.Invoking(s => s.Delete(1, 99)).Should().ThrowAsync<UnauthorizedException>();
        }
    }
}
