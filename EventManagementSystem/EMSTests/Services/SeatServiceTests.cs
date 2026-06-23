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
    public class SeatServiceTests
    {
        private Mock<ISeatRepository> _seatRepo;
        private IMapper _mapper;
        private SeatService _sut;

        [SetUp]
        public void SetUp()
        {
            _seatRepo = new Mock<ISeatRepository>();
            _mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>(), Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance).CreateMapper();
            _sut = new SeatService(_seatRepo.Object, _mapper);
        }

        [Test]
        public async Task Create_ValidRequest_ReturnsSeatDto()
        {
            _seatRepo.Setup(r => r.Add(It.IsAny<Seat>())).ReturnsAsync((Seat s) => s);

            var result = await _sut.Create(new CreateSeatRequest
            {
                VenueId = 1, Section = "A", Row = "1", SeatNumber = 1, SeatType = "VIP"
            });

            result.Section.Should().Be("A");
        }

        [Test]
        public async Task BulkCreate_Range_ReturnsCorrectCount()
        {
            _seatRepo.Setup(r => r.Add(It.IsAny<Seat>())).ReturnsAsync((Seat s) => s);

            var result = await _sut.BulkCreate(new BulkCreateSeatsRequest
            {
                VenueId = 1, Section = "B", Row = "2", SeatType = "General", StartNumber = 1, EndNumber = 5
            });

            result.Should().HaveCount(5);
        }

        [Test]
        public async Task GetByVenueId_ReturnsMappedList()
        {
            _seatRepo.Setup(r => r.GetByVenueId(1)).ReturnsAsync(new List<Seat> { new Seat { Id = 1 } });

            var result = await _sut.GetByVenueId(1);

            result.Should().HaveCount(1);
        }

        [Test]
        public async Task GetAvailableByEventId_ReturnsMappedList()
        {
            _seatRepo.Setup(r => r.GetAvailableByEventId(1)).ReturnsAsync(new List<Seat> { new Seat { Id = 2 } });

            var result = await _sut.GetAvailableByEventId(1);

            result.Should().HaveCount(1);
        }

        [Test]
        public async Task Delete_ExistingSeat_CallsDeleteOnRepo()
        {
            _seatRepo.Setup(r => r.GetById(1)).ReturnsAsync(new Seat { Id = 1 });
            _seatRepo.Setup(r => r.Delete(1)).Returns(Task.CompletedTask);

            await _sut.Delete(1);

            _seatRepo.Verify(r => r.Delete(1), Times.Once);
        }

        [Test]
        public async Task Delete_NotFound_ThrowsNotFoundException()
        {
            _seatRepo.Setup(r => r.GetById(99)).ReturnsAsync((Seat?)null);

            await _sut.Invoking(s => s.Delete(99)).Should().ThrowAsync<NotFoundException>();
        }

        // ── Create – validation guards ────────────────────────────────────────────

        [Test]
        public async Task Create_InvalidVenueId_ThrowsValidationException()
        {
            await _sut.Invoking(s => s.Create(new CreateSeatRequest
            {
                VenueId = 0, Section = "A", Row = "1", SeatNumber = 1, SeatType = "VIP"
            })).Should().ThrowAsync<ValidationException>().WithMessage("*VenueId*");
        }

        [Test]
        public async Task Create_MissingSection_ThrowsValidationException()
        {
            await _sut.Invoking(s => s.Create(new CreateSeatRequest
            {
                VenueId = 1, Section = "", Row = "1", SeatNumber = 1, SeatType = "VIP"
            })).Should().ThrowAsync<ValidationException>().WithMessage("*Section*");
        }

        [Test]
        public async Task Create_MissingRow_ThrowsValidationException()
        {
            await _sut.Invoking(s => s.Create(new CreateSeatRequest
            {
                VenueId = 1, Section = "A", Row = "", SeatNumber = 1, SeatType = "VIP"
            })).Should().ThrowAsync<ValidationException>().WithMessage("*Row*");
        }

        [Test]
        public async Task Create_InvalidSeatNumber_ThrowsValidationException()
        {
            await _sut.Invoking(s => s.Create(new CreateSeatRequest
            {
                VenueId = 1, Section = "A", Row = "1", SeatNumber = 0, SeatType = "VIP"
            })).Should().ThrowAsync<ValidationException>().WithMessage("*SeatNumber*");
        }

        [Test]
        public async Task Create_MissingSeatType_ThrowsValidationException()
        {
            await _sut.Invoking(s => s.Create(new CreateSeatRequest
            {
                VenueId = 1, Section = "A", Row = "1", SeatNumber = 1, SeatType = ""
            })).Should().ThrowAsync<ValidationException>().WithMessage("*SeatType*");
        }

        // ── BulkCreate – validation guards ────────────────────────────────────────

        [Test]
        public async Task BulkCreate_InvalidVenueId_ThrowsValidationException()
        {
            await _sut.Invoking(s => s.BulkCreate(new BulkCreateSeatsRequest
            {
                VenueId = 0, Section = "A", Row = "1", SeatType = "VIP", StartNumber = 1, EndNumber = 5
            })).Should().ThrowAsync<ValidationException>().WithMessage("*VenueId*");
        }

        [Test]
        public async Task BulkCreate_MissingSection_ThrowsValidationException()
        {
            await _sut.Invoking(s => s.BulkCreate(new BulkCreateSeatsRequest
            {
                VenueId = 1, Section = "", Row = "1", SeatType = "VIP", StartNumber = 1, EndNumber = 5
            })).Should().ThrowAsync<ValidationException>().WithMessage("*Section*");
        }

        [Test]
        public async Task BulkCreate_MissingRow_ThrowsValidationException()
        {
            await _sut.Invoking(s => s.BulkCreate(new BulkCreateSeatsRequest
            {
                VenueId = 1, Section = "A", Row = "", SeatType = "VIP", StartNumber = 1, EndNumber = 5
            })).Should().ThrowAsync<ValidationException>().WithMessage("*Row*");
        }

        [Test]
        public async Task BulkCreate_MissingSeatType_ThrowsValidationException()
        {
            await _sut.Invoking(s => s.BulkCreate(new BulkCreateSeatsRequest
            {
                VenueId = 1, Section = "A", Row = "1", SeatType = "", StartNumber = 1, EndNumber = 5
            })).Should().ThrowAsync<ValidationException>().WithMessage("*SeatType*");
        }

        [Test]
        public async Task BulkCreate_InvalidStartNumber_ThrowsValidationException()
        {
            await _sut.Invoking(s => s.BulkCreate(new BulkCreateSeatsRequest
            {
                VenueId = 1, Section = "A", Row = "1", SeatType = "VIP", StartNumber = 0, EndNumber = 5
            })).Should().ThrowAsync<ValidationException>().WithMessage("*StartNumber*");
        }

        [Test]
        public async Task BulkCreate_EndBeforeStart_ThrowsValidationException()
        {
            await _sut.Invoking(s => s.BulkCreate(new BulkCreateSeatsRequest
            {
                VenueId = 1, Section = "A", Row = "1", SeatType = "VIP", StartNumber = 5, EndNumber = 3
            })).Should().ThrowAsync<ValidationException>().WithMessage("*EndNumber*");
        }

        [Test]
        public async Task BulkCreate_TooManySeats_ThrowsValidationException()
        {
            await _sut.Invoking(s => s.BulkCreate(new BulkCreateSeatsRequest
            {
                VenueId = 1, Section = "A", Row = "1", SeatType = "VIP", StartNumber = 1, EndNumber = 1001
            })).Should().ThrowAsync<ValidationException>().WithMessage("*1000*");
        }

        [Test]
        public async Task SetScreenSeats_ReplacesSeats_WhenNoActiveUsage()
        {
            _seatRepo.Setup(r => r.ScreenHasActiveSeatUsage(1, "Screen 1")).ReturnsAsync(false);
            _seatRepo.Setup(r => r.ReplaceScreenSeats(1, "Screen 1", It.IsAny<List<Seat>>())).Returns(Task.CompletedTask);

            var req = new SetScreenSeatsRequest
            {
                VenueId = 1, Screen = "Screen 1",
                Seats = new() { new ScreenSeatDto { Row = "A", SeatNumber = 1, SeatType = "Normal" } }
            };

            var result = await _sut.SetScreenSeats(req);

            result.Should().HaveCount(1);
            _seatRepo.Verify(r => r.ReplaceScreenSeats(1, "Screen 1",
                It.Is<List<Seat>>(l => l.Count == 1 && l[0].Section == "Screen 1" && l[0].SeatType == "Normal")), Times.Once);
        }

        [Test]
        public async Task SetScreenSeats_Throws_WhenScreenInUse()
        {
            _seatRepo.Setup(r => r.ScreenHasActiveSeatUsage(1, "Screen 1")).ReturnsAsync(true);

            var req = new SetScreenSeatsRequest
            {
                VenueId = 1, Screen = "Screen 1",
                Seats = new() { new ScreenSeatDto { Row = "A", SeatNumber = 1, SeatType = "Normal" } }
            };

            var act = async () => await _sut.SetScreenSeats(req);

            await act.Should().ThrowAsync<ValidationException>().WithMessage("*bookings*");
            _seatRepo.Verify(r => r.ReplaceScreenSeats(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<List<Seat>>()), Times.Never);
        }
    }
}
