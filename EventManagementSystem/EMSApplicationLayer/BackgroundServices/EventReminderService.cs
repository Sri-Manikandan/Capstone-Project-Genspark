using EMSBLLLibrary.Constants;
using EMSBLLLibrary.Helpers;
using EMSBLLLibrary.Interfaces;
using EMSDALLibrary.Interfaces;

namespace EMSApplicationLayer.BackgroundServices
{
    // Finds screenings starting in 24-25 hours and reminds everyone holding a ticket.
    // Split from the hosted service so a single sweep can be driven in tests with an
    // injected clock.
    public class EventReminderSweeper
    {
        private readonly IScreeningRepository _screeningRepo;
        private readonly IBookingRepository _bookingRepo;
        private readonly IEventRepository _eventRepo;
        private readonly IVenueRepository _venueRepo;
        private readonly IEmailQueue _emailQueue;
        private readonly ILogger<EventReminderSweeper> _logger;

        public EventReminderSweeper(
            IScreeningRepository screeningRepo,
            IBookingRepository bookingRepo,
            IEventRepository eventRepo,
            IVenueRepository venueRepo,
            IEmailQueue emailQueue,
            ILogger<EventReminderSweeper> logger)
        {
            _screeningRepo = screeningRepo;
            _bookingRepo = bookingRepo;
            _eventRepo = eventRepo;
            _venueRepo = venueRepo;
            _emailQueue = emailQueue;
            _logger = logger;
        }

        public async Task SweepOnce(DateTime now)
        {
            // An hourly sweep over a one-hour-wide window 24h out catches every
            // screening exactly once. The dedupe key makes an overlap harmless anyway.
            var screenings = await _screeningRepo.GetStartingBetween(now.AddHours(24), now.AddHours(25));

            foreach (var screening in screenings)
            {
                var holders = await _bookingRepo.GetConfirmedTicketHoldersByScreening(screening.Id);
                if (holders.Count == 0)
                    continue;

                var ev = await _eventRepo.GetById(screening.EventId);
                var venue = ev == null ? null : await _venueRepo.GetById(ev.VenueId);

                var startIst = TimeHelper.UtcToIst(screening.StartTime).ToString("dddd, d MMM yyyy 'at' h:mm tt");

                await _emailQueue.EnqueueMany(holders.Select(h => new QueuedEmail(
                    h.UserEmail, h.UserName, EmailTemplateKey.EventReminder,
                    $"{ev?.Title ?? "Your event"} is tomorrow",
                    new Dictionary<string, string>
                    {
                        ["Name"] = h.UserName,
                        ["EventTitle"] = ev?.Title ?? "Your event",
                        ["ScreeningTime"] = startIst,
                        ["VenueName"] = venue?.Name ?? "-",
                        ["BookingReference"] = h.BookingReference
                    },
                    DedupeKey: $"reminder:booking:{h.BookingId}",
                    SendAfter: null)).ToList());

                _logger.LogInformation("Queued {Count} reminders for screening {ScreeningId}.",
                    holders.Count, screening.Id);
            }
        }
    }

    public class EventReminderService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<EventReminderService> _logger;
        private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

        public EventReminderService(IServiceScopeFactory scopeFactory, ILogger<EventReminderService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var sweeper = scope.ServiceProvider.GetRequiredService<EventReminderSweeper>();
                    await sweeper.SweepOnce(DateTime.UtcNow);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during event reminder sweep.");
                }

                await Task.Delay(Interval, stoppingToken);
            }
        }
    }
}
