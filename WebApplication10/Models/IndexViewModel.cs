namespace WebApplication10.Models
{
    public class IndexViewModel
    {
        public List<Post> Posts { get; set; }
        public List<FeedItemViewModel> FeedItems { get; set; }
        // تجميع القصص حسب مُعرف المستخدم (int)
        public IEnumerable<IGrouping<int, Story>> GroupedStories { get; set; }
        public IEnumerable<User>? SuggestedFriends { get; set; }
    }
}
