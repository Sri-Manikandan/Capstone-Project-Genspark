using Microsoft.AspNetCore.SignalR;

namespace EMSApplicationLayer.Hubs
{
    public class SeatHub : Hub
    {
        public async Task JoinEventRoom(int eventId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"event-{eventId}");
        }

        public async Task LeaveEventRoom(int eventId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"event-{eventId}");
        }
    }
}
