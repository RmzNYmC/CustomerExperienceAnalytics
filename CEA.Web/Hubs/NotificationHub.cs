using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CEA.Web.Hubs
{
    [Authorize]
    public class NotificationHub : Hub
    {
        public async Task SendNotification(string userId, string title, string message, string link)
        {
            await Clients.User(userId).SendAsync("ReceiveNotification", new
            {
                Title = title,
                Message = message,
                Link = link,
                Timestamp = DateTime.Now
            });
        }

        public async Task BroadcastToAdmins(string title, string message, string link = "")
        {
            await Clients.Group("Admins").SendAsync("ReceiveNotification", new
            {
                Title = title,
                Message = message,
                Link = link,
                Timestamp = DateTime.Now
            });
        }

        public override async Task OnConnectedAsync()
        {
            if (Context.User?.IsInRole("Admin") == true)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
            }
            await base.OnConnectedAsync();
        }
    }
}