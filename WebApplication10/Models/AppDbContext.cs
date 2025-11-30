using Microsoft.EntityFrameworkCore;
using System;
using System.Diagnostics.Metrics;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace WebApplication10.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<Like> Likes { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<Message> Messages { get; set; }

        public DbSet<Follower> Followers { get; set; }
        public DbSet<Notification> notifications { get; set; }
        public DbSet<Story> Stories { get; set; }
        public DbSet<Share> Shares { get; set; } // 👈 إضافة DbSet لـ Share

        public DbSet<Reel> Reels { get; set; }

        public DbSet<ReelLike> ReelLikes { get; set; }
        public DbSet<ReelComment> ReelComments { get; set; }
        public DbSet<ReelShare> ReelShares { get; set; }

    }
}
