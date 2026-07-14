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
        public async Task GetAvailableByScreeningId_ReturnsWholeVenueGrid_AllAvailableWhenNothingBooked()
        {
            using var ctx = CreateContext();
            ctx.Venues.Add(new Venue { Id = 1, Name = "V" });
            ctx.Events.Add(new Event { Id = 1, VenueId = 1 });
            ctx.Screenings.Add(new Screening { Id = 1, EventId = 1, Screen = "Screen 1", Status = "Scheduled" });
            ctx.Seats.AddRange(
                new Seat { Id = 1, VenueId = 1, Section = "A", Row = "A", SeatNumber = 1, SeatType = "Normal" },
                new Seat { Id = 2, VenueId = 1, Section = "A", Row = "A", SeatNumber = 2, SeatType = "Normal" });
            await ctx.SaveChangesAsync();

            var repo = new SeatRepository(ctx);
            var result = await repo.GetAvailableByScreeningId(1);

            result.Should().HaveCount(2).And.OnlyContain(s => s.IsAvailable);
        }

        [Test]
        public async Task GetAvailableByScreeningId_KeepsBookedSeatButFlagsItUnavailableForThatScreeningOnly()
        {
            using var ctx = CreateContext();
            ctx.Venues.Add(new Venue { Id = 1, Name = "V" });
            ctx.Events.Add(new Event { Id = 1, VenueId = 1 });
            ctx.Screenings.AddRange(
                new Screening { Id = 1, EventId = 1, Screen = "Screen 1", Status = "Scheduled" },
                new Screening { Id = 2, EventId = 1, Screen = "Screen 2", Status = "Scheduled" });
            ctx.Seats.AddRange(
                new Seat { Id = 1, VenueId = 1, Section = "A", Row = "A", SeatNumber = 1, SeatType = "Normal" },
                new Seat { Id = 2, VenueId = 1, Section = "A", Row = "A", SeatNumber = 2, SeatType = "Normal" });
            ctx.Bookings.Add(new Booking { Id = 3, ScreeningId = 1, BookingStatus = "Confirmed" });
            ctx.BookingItems.Add(new BookingItem { Id = 9, BookingId = 3, SeatId = 1 });
            await ctx.SaveChangesAsync();

            var repo = new SeatRepository(ctx);

            // Seat 1 is booked in screening 1: still returned there (so it renders as taken,
            // not vanishing) but flagged unavailable — while staying free in screening 2.
            var screening1 = await repo.GetAvailableByScreeningId(1);
            screening1.Should().HaveCount(2);
            screening1.Single(s => s.Seat.Id == 1).IsAvailable.Should().BeFalse();
            screening1.Single(s => s.Seat.Id == 2).IsAvailable.Should().BeTrue();

            (await repo.GetAvailableByScreeningId(2)).Should().HaveCount(2).And.OnlyContain(s => s.IsAvailable);
        }

        [Test]
        public async Task GetAvailableByScreeningId_ReturnsSeatsOrderedBySectionRowNumber()
        {
            using var ctx = CreateContext();
            ctx.Venues.Add(new Venue { Id = 1, Name = "V" });
            ctx.Events.Add(new Event { Id = 1, VenueId = 1 });
            ctx.Screenings.Add(new Screening { Id = 1, EventId = 1, Screen = "Screen 1", Status = "Scheduled" });
            // Inserted deliberately out of order; the result must still be Section→Row→SeatNumber
            // so the seat layout never reshuffles between fetches.
            ctx.Seats.AddRange(
                new Seat { Id = 1, VenueId = 1, Section = "B", Row = "A", SeatNumber = 2, SeatType = "Normal" },
                new Seat { Id = 2, VenueId = 1, Section = "A", Row = "B", SeatNumber = 1, SeatType = "Normal" },
                new Seat { Id = 3, VenueId = 1, Section = "A", Row = "A", SeatNumber = 2, SeatType = "Normal" },
                new Seat { Id = 4, VenueId = 1, Section = "A", Row = "A", SeatNumber = 1, SeatType = "Normal" });
            await ctx.SaveChangesAsync();

            var repo = new SeatRepository(ctx);
            var result = await repo.GetAvailableByScreeningId(1);

            result.Select(s => s.Seat.Id).Should().ContainInOrder(4, 3, 2, 1);
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
            ctx.Bookings.Add(new Booking { Id = 3, ScreeningId = 1, BookingStatus = "Confirmed" });
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

        [Test]
        public async Task ReplaceScreenSeats_Throws_WhenScreenInUse()
        {
            using var ctx = CreateContext();
            ctx.Seats.Add(new Seat { Id = 11, VenueId = 1, Section = "Screen 1", Row = "A", SeatNumber = 1, SeatType = "Normal" });
            ctx.Bookings.Add(new Booking { Id = 4, ScreeningId = 1, BookingStatus = "Confirmed" });
            ctx.BookingItems.Add(new BookingItem { Id = 12, BookingId = 4, SeatId = 11 });
            await ctx.SaveChangesAsync();
            var repo = new SeatRepository(ctx);

            var act = async () => await repo.ReplaceScreenSeats(1, "Screen 1", new List<Seat>
            {
                new Seat { VenueId = 1, Section = "Screen 1", Row = "A", SeatNumber = 1, SeatType = "Premium" },
            });

            await act.Should().ThrowAsync<EMSModelLibrary.Exceptions.ValidationException>().WithMessage("*bookings*");
            (await repo.GetByVenueId(1)).Should().ContainSingle(s => s.SeatType == "Normal"); // unchanged
        }
    }
}
