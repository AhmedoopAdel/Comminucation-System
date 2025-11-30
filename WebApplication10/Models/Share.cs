namespace WebApplication10.Models
{
    public class Share
    {
        public int Id { get; set; }
        public int PostId { get; set; }
        public int UserId { get; set; }
        public DateTime SharedAt { get; set; } = DateTime.Now;

        public Post Post { get; set; }
        public User User { get; set; }
    }

    public class FeedItemViewModel
    {
        public int OriginalPostId { get; set; }
        public string OriginalPosterName { get; set; }
        public string SharerName { get; set; }
        public string Content { get; set; }
        public string? ImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? SharedAt { get; set; }
        public bool IsShare { get; set; }
    }

}
