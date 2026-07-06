using System.Text.Json;
using EMSDALLibrary.Constants;
using EMSDALLibrary.Contexts;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;

namespace EMSTests.Services
{
    [TestFixture]
    public class EventContextAuditTests
    {
        private Mock<ICurrentUserAccessor> _currentUser;

        [SetUp]
        public void SetUp()
        {
            _currentUser = new Mock<ICurrentUserAccessor>();
            _currentUser.Setup(c => c.GetUserId()).Returns(7);
            _currentUser.Setup(c => c.GetUserRole()).Returns("Admin");
        }

        private EventContext NewContext() => new EventContext(
            new DbContextOptionsBuilder<EventContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            _currentUser.Object);

        private static Venue MakeVenue() =>
            new Venue { Name = "Hall", Address = "1 St", City = "Chennai", TotalCapacity = 100, LayoutConfig = "{}" };

        [Test]
        public async Task Insert_WritesCreateChangeLog_WithActorAndKey()
        {
            using var ctx = NewContext();
            var venue = MakeVenue();
            ctx.Venues.Add(venue);
            await ctx.SaveChangesAsync();

            var log = ctx.ChangeLogs.Single();
            log.EntityName.Should().Be(nameof(Venue));
            log.Action.Should().Be(ChangeAction.Create);
            log.EntityKey.Should().Be(venue.Id.ToString());
            log.UserId.Should().Be(7);
            log.UserRole.Should().Be("Admin");
            log.Changes.Should().Contain("Name");
        }

        [Test]
        public async Task Update_WritesUpdateChangeLog_WithOnlyChangedField()
        {
            using var ctx = NewContext();
            var venue = MakeVenue();
            ctx.Venues.Add(venue);
            await ctx.SaveChangesAsync();

            venue.Name = "Renamed";
            await ctx.SaveChangesAsync();

            var log = ctx.ChangeLogs.Single(c => c.Action == ChangeAction.Update);
            log.EntityKey.Should().Be(venue.Id.ToString());

            var diff = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, JsonElement>>>(log.Changes)!;
            diff.Should().ContainKey("Name");
            diff["Name"]["old"].GetString().Should().Be("Hall");
            diff["Name"]["new"].GetString().Should().Be("Renamed");
            diff.Should().NotContainKey("City");
        }

        [Test]
        public async Task Delete_WritesDeleteChangeLog()
        {
            using var ctx = NewContext();
            var venue = MakeVenue();
            ctx.Venues.Add(venue);
            await ctx.SaveChangesAsync();
            var id = venue.Id;

            ctx.Venues.Remove(venue);
            await ctx.SaveChangesAsync();

            var log = ctx.ChangeLogs.Single(c => c.Action == ChangeAction.Delete);
            log.EntityName.Should().Be(nameof(Venue));
            log.EntityKey.Should().Be(id.ToString());
        }

        [Test]
        public async Task NoAuthenticatedUser_WritesNoChangeLog()
        {
            _currentUser.Setup(c => c.GetUserId()).Returns((int?)null);

            using var ctx = NewContext();
            ctx.Venues.Add(MakeVenue());
            await ctx.SaveChangesAsync();

            ctx.ChangeLogs.Should().BeEmpty();
        }
    }
}
