using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EMSApplicationLayer.Hubs
{
    // Guests browsing public event pages receive live seat-availability updates,
    // so the hub is intentionally reachable without authentication.
    [AllowAnonymous]
    public class SeatHub : Hub
    {
        public async Task JoinScreeningRoom(int screeningId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"screening-{screeningId}");
        }

        public async Task LeaveScreeningRoom(int screeningId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"screening-{screeningId}");
        }
    }
}
