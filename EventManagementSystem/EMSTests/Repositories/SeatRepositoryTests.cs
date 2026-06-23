using EMSDALLibrary.Contexts;
using EMSDALLibrary.Repositories;
using EMSModelLibrary.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NUnit.Framework;

namespace EMSTests.Repositories
{
    [TestFixture]
    public class SeatRepositoryTests
    {
        private EventContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<EventContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new EventContext(options);
        }

        [Test]
        public async Task GetAvailableByEventId_FiltersBySection_WhenEventScreenSet()
        {
            using var ctx = CreateContext();
            ctx.Venues.Add(new Venue { Id = 1, Name = "V" });
            ctx.Events.Add(new Event { Id = 1, VenueId = 1, Screen = "Screen 2" });
            ctx.Seats.AddRange(
                new Seat { Id = 1, VenueId = 1, Section = "Screen 1", Row = "A", SeatNumber = 1, SeatType = "Normal" },
                new Seat { Id = 2, VenueId = 1, Section = "Screen 2", Row = "A", SeatNumber = 1, SeatType = "Normal" });
            await ctx.SaveChangesAsync();

            var repo = new SeatRepository(ctx);
            var result = await repo.GetAvailableByEventId(1);

            result.Should().ContainSingle().Which.Id.Should().Be(2);
        }

        [Test]
        public async Task GetAvailableByEventId_ReturnsWholeVenue_WhenScreenEmpty()
        {
            using var ctx = CreateContext();
            ctx.Venues.Add(new Venue { Id = 1, Name = "V" });
            ctx.Events.Add(new Event { Id = 1, VenueId = 1, Screen = "" });
            ctx.Seats.AddRange(
                new Seat { Id = 1, VenueId = 1, Section = "Screen 1", Row = "A", SeatNumber = 1, SeatType = "Normal" },
                new Seat { Id = 2, VenueId = 1, Section = "Screen 2", Row = "A", SeatNumber = 1, SeatType = "Normal" });
            await ctx.SaveChangesAsync();

            var repo = new SeatRepository(ctx);
            var result = await repo.GetAvailableByEventId(1);

            result.Should().HaveCount(2);
        }

        [Test]
        public async Task ReplaceScreenSeats_RemovesOldAndAddsNew()
        {
            using var ctx = CreateContext();
            ctx.Seats.Add(new Seat { Id = 5, VenueId = 1, Section = "Screen 1", Row = "A", SeatNumber = 1, SeatType = "Normal" });
            await ctx.SaveChangesAsync();
            var repo = new SeatRepository(ctx);

            await repo.ReplaceScreenSeats(1, "Screen 1", new List<Seat>
            {
                new Seat { VenueId = 1, Section = "Screen 1", Row = "A", SeatNumber = 1, SeatType = "Premium" },
                new Seat { VenueId = 1, Section = "Screen 1", Row = "A", SeatNumber = 2, SeatType = "Premium" },
            });

            var remaining = await repo.GetByVenueId(1);
            remaining.Should().HaveCount(2).And.OnlyContain(s => s.SeatType == "Premium");
        }

        [Test]
        public async Task ScreenHasActiveSeatUsage_True_WhenSeatBooked()
        {
            using var ctx = CreateContext();
            ctx.Seats.Add(new Seat { Id = 7, VenueId = 1, Section = "Screen 1", Row = "A", SeatNumber = 1, SeatType = "Normal" });
            ctx.Bookings.Add(new Booking { Id = 3, EventId = 1, BookingStatus = "Confirmed" });
            ctx.BookingItems.Add(new BookingItem { Id = 9, BookingId = 3, SeatId = 7 });
            await ctx.SaveChangesAsync();
            var repo = new SeatRepository(ctx);

            (await repo.ScreenHasActiveSeatUsage(1, "Screen 1")).Should().BeTrue();
        }

        [Test]
        public async Task ScreenHasActiveSeatUsage_False_WhenNoUsage()
        {
            using var ctx = CreateContext();
            ctx.Seats.Add(new Seat { Id = 8, VenueId = 1, Section = "Screen 1", Row = "A", SeatNumber = 1, SeatType = "Normal" });
            await ctx.SaveChangesAsync();
            var repo = new SeatRepository(ctx);

            (await repo.ScreenHasActiveSeatUsage(1, "Screen 1")).Should().BeFalse();
        }
    }
}
