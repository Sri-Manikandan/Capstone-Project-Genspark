using EMSBLLLibrary.Constants;
using EMSBLLLibrary.Interfaces;
using EMSDALLibrary.Contexts;
using EMSDALLibrary.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EMSApplicationLayer.BackgroundServices
{
    public class BookingExpiryService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BookingExpiryService> _logger;
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

        public BookingExpiryService(IServiceScopeFactory scopeFactory, ILogger<BookingExpiryService> logger)
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
                    await ExpireBookings(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during booking expiry sweep.");
                }

                await Task.Delay(Interval, stoppingToken);
            }
        }

        private async Task ExpireBookings(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<EventContext>();
            var reservationRepo = scope.ServiceProvider.GetRequiredService<ISeatReservationRepository>();
            var ticketTypeRepo = scope.ServiceProvider.GetRequiredService<ITicketTypeRepository>();
            var emailQueue = scope.ServiceProvider.GetRequiredService<IEmailQueue>();

            var now = DateTime.UtcNow;

            var expiredBookings = await context.Bookings
                .Where(b => b.BookingStatus == "Pending" && b.ExpiresAt < now)
                .ToListAsync(ct);

            if (expiredBookings.Count == 0)
            {
                await reservationRepo.DeleteExpired();
                return;
            }

            foreach (var booking in expiredBookings)
            {
                booking.BookingStatus = "Cancelled";
                booking.UpdatedAt = now;

                var items = await context.BookingItems
                    .Where(bi => bi.BookingId == booking.Id)
                    .ToListAsync(ct);

                foreach (var item in items)
                {
                    item.TicketStatus = "Cancelled";
                    await ticketTypeRepo.IncrementAvailableQuantity(item.TicketTypeId);

                    var reservation = await context.SeatReservations
                        .FirstOrDefaultAsync(sr => sr.ScreeningId == booking.ScreeningId
                                                && sr.SeatId == item.SeatId
                                                && sr.Status == "Confirmed", ct);
                    if (reservation != null)
                        reservation.Status = "Expired";
                }

                var user = await context.Users.FirstOrDefaultAsync(u => u.Id == booking.UserId, ct);
                var screening = await context.Screenings.FirstOrDefaultAsync(s => s.Id == booking.ScreeningId, ct);
                var ev = screening == null
                    ? null
                    : await context.Events.FirstOrDefaultAsync(e => e.Id == screening.EventId, ct);

                if (user != null)
                {
                    // The dedupe key matters: this sweep runs every 60s, and a booking
                    // whose save fails stays eligible on the next pass.
                    await emailQueue.Enqueue(user.Email, user.Name, EmailTemplateKey.BookingExpired,
                        "Your booking expired",
                        new Dictionary<string, string>
                        {
                            ["Name"] = user.Name,
                            ["EventTitle"] = ev?.Title ?? "your event",
                            ["BookingReference"] = booking.BookingReference
                        },
                        dedupeKey: $"expired:booking:{booking.Id}");
                }
            }

            await context.SaveChangesAsync(ct);

            await reservationRepo.DeleteExpired();

            _logger.LogInformation("Expired {Count} pending bookings and restored inventory.", expiredBookings.Count);
        }
    }
}
