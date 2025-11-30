using Microsoft.AspNetCore.SignalR;

namespace WebApplication10.Models
{
    public class NotificationHub : Hub
    {
        
        public async Task SendNotification(string userId, string message, int unreadCount)
        {
            await Clients.User(userId).SendAsync("ReceiveNotification", message, unreadCount);
        }
        public async Task UpdateUnreadCount(string userId, int newUnreadCount)
        {
            await Clients.User(userId).SendAsync("UpdateUnreadCount", newUnreadCount);
        }
    }

    
}
