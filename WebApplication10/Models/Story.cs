namespace WebApplication10.Models
{
    // Story.cs Model
    public class Story
    {
        public int Id { get; set; }

        // مُعرف المستخدم الذي قام بإنشاء القصة
        public int UserId { get; set; }
        public User User { get; set; } // خاصية التنقل

        // مسار الصورة أو الفيديو الخاص بالقصة
        public string MediaUrl { get; set; }

        // نوع القصة (صورة أو فيديو)
        public string MediaType { get; set; } // مثال: "Image" أو "Video"

        // وقت إنشاء القصة (مهم جداً لحذفها بعد 24 ساعة)
        public DateTime CreatedAt { get; set; }

        // (اختياري) جدول لرؤية القصة لمعرفة من شاهدها
        // public ICollection<StoryView> Views { get; set; }
    }
}
