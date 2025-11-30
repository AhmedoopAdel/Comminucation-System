namespace WebApplication10.Models
{
    public class ConversationViewModel
    {
        public int OtherUserId { get; set; }
        public string OtherUsername { get; set; }
        public string? LastMessageContent { get; set; }
        public DateTime LastMessageTime { get; set; }
    }
}
