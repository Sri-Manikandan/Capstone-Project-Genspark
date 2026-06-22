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
    public class VenueServiceTests
    {
        private Mock<IVenueRepository> _venueRepo;
        private IMapper _mapper;
        private VenueService _sut;

        [SetUp]
        public void SetUp()
        {
            _venueRepo = new Mock<IVenueRepository>();
            _mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>(), Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance).CreateMapper();
            _sut = new VenueService(_venueRepo.Object, _mapper);
        }

        [Test]
        public async Task Create_ValidRequest_ReturnsVenueDto()
        {
            _venueRepo.Setup(r => r.Add(It.IsAny<Venue>())).ReturnsAsync((Venue v) => v);

            var result = await _sut.Create(new CreateVenueRequest
            {
                Name = "Hall A", Address = "123 St", City = "Chennai", TotalCapacity = 500, LayoutConfig = "{}"
            });

            result.Name.Should().Be("Hall A");
        }

        [Test]
        public async Task GetById_ExistingId_ReturnsVenueDto()
        {
            _venueRepo.Setup(r => r.GetById(1)).ReturnsAsync(new Venue { Id = 1, Name = "Hall A" });

            var result = await _sut.GetById(1);

            result.Name.Should().Be("Hall A");
        }

        [Test]
        public async Task GetById_NotFound_ThrowsNotFoundException()
        {
            _venueRepo.Setup(r => r.GetById(99)).ReturnsAsync((Venue?)null);

            await _sut.Invoking(s => s.GetById(99)).Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task GetAll_ReturnsAllVenues()
        {
            _venueRepo.Setup(r => r.GetAll()).ReturnsAsync(new List<Venue>
            {
                new Venue { Id = 1, Name = "A" },
                new Venue { Id = 2, Name = "B" }
            });

            var result = await _sut.GetAll();

            result.Should().HaveCount(2);
        }

        [Test]
        public async Task Update_ExistingVenue_ReturnsUpdatedDto()
        {
            var venue = new Venue { Id = 1, Name = "Old" };
            _venueRepo.Setup(r => r.GetById(1)).ReturnsAsync(venue);
            _venueRepo.Setup(r => r.Update(It.IsAny<Venue>())).ReturnsAsync((Venue v) => v);

            var result = await _sut.Update(1, new UpdateVenueRequest
            {
                Name = "New", Address = "Addr", City = "City", TotalCapacity = 200, LayoutConfig = "{}"
            });

            result.Name.Should().Be("New");
        }

        [Test]
        public async Task Update_NotFound_ThrowsNotFoundException()
        {
            _venueRepo.Setup(r => r.GetById(99)).ReturnsAsync((Venue?)null);

            await _sut.Invoking(s => s.Update(99, new UpdateVenueRequest
            {
                Name = "X", Address = "X", City = "X", TotalCapacity = 100, LayoutConfig = "{}"
            })).Should().ThrowAsync<NotFoundException>();
        }

        [Test]
        public async Task Delete_ExistingVenue_CallsDeleteOnRepo()
        {
            _venueRepo.Setup(r => r.GetById(1)).ReturnsAsync(new Venue { Id = 1 });
            _venueRepo.Setup(r => r.Delete(1)).Returns(Task.CompletedTask);

            await _sut.Delete(1);

            _venueRepo.Verify(r => r.Delete(1), Times.Once);
        }

        [Test]
        public async Task Delete_NotFound_ThrowsNotFoundException()
        {
            _venueRepo.Setup(r => r.GetById(99)).ReturnsAsync((Venue?)null);

            await _sut.Invoking(s => s.Delete(99)).Should().ThrowAsync<NotFoundException>();
        }
    }
}
