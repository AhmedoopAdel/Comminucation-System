using System.ComponentModel.DataAnnotations;

namespace WebApplication10.Models
{
    public class Reel
    {
        public int Id { get; set; }
        public int UserId { get; set; } // معرّف المستخدم الذي نشر الريل

        [MaxLength(255)]
        public string VideoPath { get; set; } // مثال: /reels/xyz.mp4

        [MaxLength(500)]
        public string Caption { get; set; } // وصف الريل

        public DateTime UploadDate { get; set; } = DateTime.Now;
        public int LikesCount { get; set; } = 0;
        public int ViewsCount { get; set; } = 0;

        public User User { get; set; }
    }

    public class ReelLike
    {
        public int Id { get; set; }
        public int ReelId { get; set; }
        public int UserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
    public class ReelViewModel
    {
        public int Id { get; set; }
        public string VideoPath { get; set; }
        public string Caption { get; set; }
        public int LikesCount { get; set; }

        // 🔹 خصائص العرض
        public bool IsLikedByMe { get; set; }
        public int CommentsCount { get; set; }
        public int SharesCount { get; set; }

        public string Username { get; set; }
        public string UserId{ get; set; }

    }

    public class ReelComment
    {
        public int Id { get; set; }
        public int ReelId { get; set; }
        public int UserId { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class ReelShare
    {
        public int Id { get; set; }
        public int ReelId { get; set; }
        public int UserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

}
