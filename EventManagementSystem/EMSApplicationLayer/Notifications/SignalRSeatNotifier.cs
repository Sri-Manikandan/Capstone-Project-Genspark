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

        public Task SeatReserved(int screeningId, int seatId) =>
            _hub.Clients.Group($"screening-{screeningId}").SendAsync("SeatReserved", seatId);

        public Task SeatReleased(int screeningId, int seatId) =>
            _hub.Clients.Group($"screening-{screeningId}").SendAsync("SeatReleased", seatId);

        public Task SeatBooked(int screeningId, int seatId) =>
            _hub.Clients.Group($"screening-{screeningId}").SendAsync("SeatBooked", seatId);
    }
}
