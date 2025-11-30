using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication10.Models
{
    public class Notification
    {
        public int Id { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; } // صاحب الإشعار
        public virtual User User { get; set; }

        public string Message { get; set; }
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? PostId { get; set; }
        public string Type { get; set; } = "General";
    }
}
