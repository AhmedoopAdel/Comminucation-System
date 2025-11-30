namespace WebApplication10.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string? FullName { get; set; }
        public string? Bio { get; set; }
        public string? ProfilePicUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual List<Post> Posts { get; set; } = new List<Post>();
    }
}
