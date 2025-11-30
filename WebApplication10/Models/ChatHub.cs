using Microsoft.AspNetCore.SignalR;

namespace WebApplication10.Models
{
    public class ChatHub : Hub
    {
        // تخزين الـ ConnectionId لكل مستخدم
        private static Dictionary<int, string> UserConnections = new();

        public override Task OnConnectedAsync()
        {
            var userId = Context.GetHttpContext().Session.GetInt32("UserId");
            if (userId != null)
                UserConnections[userId.Value] = Context.ConnectionId;

            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            var item = UserConnections.FirstOrDefault(x => x.Value == Context.ConnectionId);
            if (item.Key != 0)
                UserConnections.Remove(item.Key);

            return base.OnDisconnectedAsync(exception);
        }

        // دالة إرسال رسالة لمستخدم محدد
        public async Task SendMessageToUser(int receiverId, string message, string senderUsername, string? fileUrl)
        {
            if (UserConnections.TryGetValue(receiverId, out var connectionId))
            {
                await Clients.Client(connectionId)
                    .SendAsync("ReceiveMessage", message, senderUsername, fileUrl);
            }
        }
    }

}
