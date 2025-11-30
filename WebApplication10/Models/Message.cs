namespace WebApplication10.Models
{
    public class Message
    {
        public int Id { get; set; }
        public int SenderId { get; set; }
        public int ReceiverId { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? FileUrl { get; set; }
        public User Sender { get; set; }
        public User Receiver { get; set; }
    }
}
