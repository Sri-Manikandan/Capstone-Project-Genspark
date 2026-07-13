using EMSApplicationLayer.BackgroundServices;
using EMSBLLLibrary.Constants;
using EMSBLLLibrary.Interfaces;
using EMSDALLibrary.Interfaces;
using EMSModelLibrary.DTOs;
using EMSModelLibrary.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace EMSTests.Services
{
    [TestFixture]
    public class EventReminderServiceTests
    {
        private Mock<IScreeningRepository> _screeningRepo;
        private Mock<IBookingRepository> _bookingRepo;
        private Mock<IEventRepository> _eventRepo;
        private Mock<IVenueRepository> _venueRepo;
        private Mock<IEmailQueue> _emailQueue;
        private EventReminderSweeper _sut;

        private static readonly DateTime Now = new(2026, 7, 13, 10, 0, 0, DateTimeKind.Utc);

        [SetUp]
        public void SetUp()
        {
            _screeningRepo = new Mock<IScreeningRepository>();
            _bookingRepo = new Mock<IBookingRepository>();
            _eventRepo = new Mock<IEventRepository>();
            _venueRepo = new Mock<IVenueRepository>();
            _emailQueue = new Mock<IEmailQueue>();

            _eventRepo.Setup(r => r.GetById(It.IsAny<int>()))
                .ReturnsAsync(new Event { Id = 7, Title = "Vaaranam Aayiram", VenueId = 3 });
            _venueRepo.Setup(r => r.GetById(3))
                .ReturnsAsync(new Venue { Id = 3, Name = "Sathyam Cinemas" });

            _sut = new EventReminderSweeper(_screeningRepo.Object, _bookingRepo.Object, _eventRepo.Object,
                _venueRepo.Object, _emailQueue.Object, NullLogger<EventReminderSweeper>.Instance);
        }

        private void GivenScreeningInWindow() =>
            _screeningRepo.Setup(r => r.GetStartingBetween(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Screening>
                {
                    new() { Id = 1, EventId = 7, StartTime = Now.AddHours(24.5) }
                });

        [Test]
        public async Task SweepOnce_ShouldEnqueueReminder_ForEachTicketHolder()
        {
            GivenScreeningInWindow();
            _bookingRepo.Setup(r => r.GetConfirmedTicketHoldersByScreening(1))
                .ReturnsAsync(new List<TicketHolderDto>
                {
                    new() { BookingId = 1, BookingReference = "BK1", UserEmail = "a@b.com", UserName = "Asha" },
                    new() { BookingId = 2, BookingReference = "BK2", UserEmail = "c@d.com", UserName = "Chandra" }
                });

            List<QueuedEmail>? captured = null;
            _emailQueue.Setup(q => q.EnqueueMany(It.IsAny<List<QueuedEmail>>()))
                .Callback<List<QueuedEmail>>(e => captured = e)
                .Returns(Task.CompletedTask);

            await _sut.SweepOnce(Now);

            captured.Should().HaveCount(2);
            captured![0].TemplateKey.Should().Be(EmailTemplateKey.EventReminder);
            captured[0].Tokens["EventTitle"].Should().Be("Vaaranam Aayiram");
            captured[0].Tokens["VenueName"].Should().Be("Sathyam Cinemas");
        }

        // The stable dedupe key is what stops a restart, or an overlapping sweep, from
        // reminding the same person twice.
        [Test]
        public async Task SweepOnce_ShouldUseStableDedupeKeyPerBooking()
        {
            GivenScreeningInWindow();
            _bookingRepo.Setup(r => r.GetConfirmedTicketHoldersByScreening(1))
                .ReturnsAsync(new List<TicketHolderDto>
                {
                    new() { BookingId = 42, BookingReference = "BK1", UserEmail = "a@b.com", UserName = "Asha" }
                });

            List<QueuedEmail>? captured = null;
            _emailQueue.Setup(q => q.EnqueueMany(It.IsAny<List<QueuedEmail>>()))
                .Callback<List<QueuedEmail>>(e => captured = e)
                .Returns(Task.CompletedTask);

            await _sut.SweepOnce(Now);

            captured![0].DedupeKey.Should().Be("reminder:booking:42");
        }

        // An hourly sweep over a one-hour-wide window 24h out catches every screening
        // exactly once.
        [Test]
        public async Task SweepOnce_ShouldQueryWindowOf24To25HoursAhead()
        {
            DateTime from = default, to = default;
            _screeningRepo.Setup(r => r.GetStartingBetween(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .Callback<DateTime, DateTime>((f, t) => { from = f; to = t; })
                .ReturnsAsync(new List<Screening>());

            await _sut.SweepOnce(Now);

            from.Should().Be(Now.AddHours(24));
            to.Should().Be(Now.AddHours(25));
        }

        [Test]
        public async Task SweepOnce_ShouldNotEnqueue_WhenScreeningHasNoTicketHolders()
        {
            GivenScreeningInWindow();
            _bookingRepo.Setup(r => r.GetConfirmedTicketHoldersByScreening(1))
                .ReturnsAsync(new List<TicketHolderDto>());

            await _sut.SweepOnce(Now);

            _emailQueue.Verify(q => q.EnqueueMany(It.IsAny<List<QueuedEmail>>()), Times.Never);
        }
    }
}
