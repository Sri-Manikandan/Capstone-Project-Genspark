using AutoMapper;
using EMSBLLLibrary.Mappings;
using EMSBLLLibrary.Services;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.DTOs;
using EMSModelLibrary.Models;
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace EMSTests.Services
{
    [TestFixture]
    public class ChangeLogServiceTests
    {
        private Mock<IChangeLogRepository> _changeLogRepo;
        private IMapper _mapper;
        private ChangeLogService _sut;

        [SetUp]
        public void SetUp()
        {
            _changeLogRepo = new Mock<IChangeLogRepository>();
            _mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>(), Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance).CreateMapper();
            _sut = new ChangeLogService(_changeLogRepo.Object, _mapper);
        }

        [Test]
        public async Task Search_MapsItemsAndPaging()
        {
            var logs = new List<ChangeLog>
            {
                new() { Id = 1, EntityName = "Event", Action = "Update", UserId = 3, CreatedAt = DateTime.UtcNow }
            };
            _changeLogRepo.Setup(r => r.SearchPaged("Event", 3, "Update", 1, 20))
                          .ReturnsAsync((logs, 1));

            var request = new ChangeLogQueryRequest { EntityName = "Event", UserId = 3, Action = "Update", Page = 1, PageSize = 20 };
            var result = await _sut.Search(request);

            result.TotalCount.Should().Be(1);
            result.Page.Should().Be(1);
            result.PageSize.Should().Be(20);
            result.Items.Should().ContainSingle();
            result.Items[0].EntityName.Should().Be("Event");
            result.Items[0].UserId.Should().Be(3);
        }

        [Test]
        public async Task Search_PassesFiltersToRepository()
        {
            _changeLogRepo.Setup(r => r.SearchPaged(It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>()))
                          .ReturnsAsync((new List<ChangeLog>(), 0));

            await _sut.Search(new ChangeLogQueryRequest { EntityName = "Booking", Page = 2, PageSize = 5 });

            _changeLogRepo.Verify(r => r.SearchPaged("Booking", null, null, 2, 5), Times.Once);
        }
    }
}
