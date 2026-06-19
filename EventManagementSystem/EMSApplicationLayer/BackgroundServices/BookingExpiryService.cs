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
                        .FirstOrDefaultAsync(sr => sr.EventId == booking.EventId
                                                && sr.SeatId == item.SeatId
                                                && sr.Status == "Confirmed", ct);
                    if (reservation != null)
                        reservation.Status = "Expired";
                }
            }

            await context.SaveChangesAsync(ct);

            await reservationRepo.DeleteExpired();

            _logger.LogInformation("Expired {Count} pending bookings and restored inventory.", expiredBookings.Count);
        }
    }
}
