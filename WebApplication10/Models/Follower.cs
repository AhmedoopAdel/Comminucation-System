using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication10.Models
{
    public class Follower
    {
        public int Id { get; set; }

        [ForeignKey("FollowerUser")]
        public int FollowerId { get; set; } // الشخص اللي بيتابع
        public virtual User FollowerUser { get; set; }

        [ForeignKey("FollowingUser")]
        public int FollowingId { get; set; } // الشخص اللي بيتابعوه
        public virtual User FollowingUser { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
