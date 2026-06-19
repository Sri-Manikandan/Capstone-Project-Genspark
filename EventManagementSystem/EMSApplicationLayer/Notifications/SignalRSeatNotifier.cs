using EMSApplicationLayer.Hubs;
using EMSBLLLibrary.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace EMSApplicationLayer.Notifications
{
    public class SignalRSeatNotifier : ISeatNotifier
    {
        private readonly IHubContext<SeatHub> _hub;

        public SignalRSeatNotifier(IHubContext<SeatHub> hub)
        {
            _hub = hub;
        }

        public Task SeatReserved(int eventId, int seatId) =>
            _hub.Clients.Group($"event-{eventId}").SendAsync("SeatReserved", seatId);

        public Task SeatReleased(int eventId, int seatId) =>
            _hub.Clients.Group($"event-{eventId}").SendAsync("SeatReleased", seatId);

        public Task SeatBooked(int eventId, int seatId) =>
            _hub.Clients.Group($"event-{eventId}").SendAsync("SeatBooked", seatId);
    }
}
