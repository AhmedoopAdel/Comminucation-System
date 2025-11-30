namespace WebApplication10.Models
{
    public class Post
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Content { get; set; }
        public string? ImageUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int? OriginalPostId { get; set; }
        public Post? OriginalPost { get; set; }
        public User User { get; set; }
        public List<Like>? Likes { get; set; }
        public virtual List<Comment> Comments { get; set; } = new List<Comment>();
    }
}
