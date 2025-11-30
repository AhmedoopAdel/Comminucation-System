using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using WebApplication10.Models;

namespace WebApplication10.Controllers
{
    
    public class PostController : Controller
    {
        private readonly IHubContext<NotificationHub> _hub;
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _webHostEnvironment;
        public PostController(AppDbContext db, IHubContext<NotificationHub> hub, IWebHostEnvironment webHostEnvironment)
        {
            _db = db;
            _hub = hub;
            _webHostEnvironment = webHostEnvironment;
        }


        // عرض كل المنشورات (Feed)
        public async Task<IActionResult> Index()
        {
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (currentUserId == null) return RedirectToAction("Login", "Account");

            // جلب قائمة المتابعين
            var followingIds = await _db.Followers
                .Where(f => f.FollowerId == currentUserId)
                .Select(f => f.FollowingId)
                .ToListAsync();

            // 1. جلب المنشورات
            var posts = await _db.Posts
                .Include(p => p.User)
                .Include(p => p.OriginalPost)
                .Include(p => p.Likes)
                .Include(p => p.Comments)
                    .ThenInclude(c => c.User)
                .Where(p => followingIds.Contains(p.UserId) || p.UserId == currentUserId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            // 2. جلب القصص النشطة (نفس منطق GetStoriesFeed)
            var twentyFourHoursAgo = DateTime.Now.AddHours(-24);
            var storyUserIds = followingIds.ToList();
            storyUserIds.Add(currentUserId.Value); // أضف قصص المستخدم الحالي

            var activeStories = await _db.Stories
                .Include(s => s.User)
                .Where(s => s.CreatedAt >= twentyFourHoursAgo && storyUserIds.Contains(s.UserId))
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            var groupedStories = activeStories.GroupBy(s => s.UserId);

            // =======================================================
            // 3. منطق ترشيحات الأصدقاء (Followers' Friends)
            // =======================================================

            // جلب مُعرّفات الأشخاص الذين يتابعهم متابعوك (Followings' Followings)
            var recommendedIds = await _db.Followers
                .Where(f => followingIds.Contains(f.FollowerId))
                .Select(f => f.FollowingId)
                .Distinct()
                .ToListAsync();

            // إعداد قائمة IDs لاستبعادها (أنت + من تتابعهم بالفعل)
            var idsToExclude = followingIds.ToList();
            idsToExclude.Add(currentUserId.Value);

            // تصفية الترشيحات
            var finalSuggestionsIds = recommendedIds
                .Where(id => !idsToExclude.Contains(id))
                .Distinct()
                .ToList();

            // جلب تفاصيل المستخدمين المتبقيين للترشيح
            var suggestedUsers = await _db.Users
                .Where(u => finalSuggestionsIds.Contains(u.Id))
                .Take(5) // عرض أول 5 ترشيحات فقط
                .ToListAsync();

            // 4. تجميع البيانات في ViewModel
            var viewModel = new IndexViewModel
            {
                Posts = posts,
                GroupedStories = groupedStories,
                // ✅ إضافة الترشيحات إلى ViewModel
                SuggestedFriends = suggestedUsers
            };

            return View(viewModel);
        }

        // =======================================================
        // 📸 دالة إنشاء القصة (CreateStory)
        // =======================================================
        [HttpPost]
        public async Task<IActionResult> CreateStory(IFormFile mediaFile)
        {
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (currentUserId == null) return Unauthorized();

            if (mediaFile == null || mediaFile.Length == 0)
            {
                TempData["StoryError"] = "يجب اختيار ملف صورة أو فيديو.";
                return RedirectToAction("Index");
            }

            var uploadResult = await SaveMediaFile(mediaFile);

            if (uploadResult == null)
            {
                TempData["StoryError"] = "فشل تحميل الملف. تأكد من أنه صورة أو فيديو صالح.";
                return RedirectToAction("Index");
            }

            var story = new Story
            {
                UserId = currentUserId.Value,
                MediaUrl = uploadResult.Url,
                MediaType = uploadResult.MediaType,
                CreatedAt = DateTime.Now
            };

            _db.Stories.Add(story);
            await _db.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        // =======================================================
        // 👁️ دالة مشاهدة القصص (ViewStories) - تعرض شاشة القصة بملء الشاشة
        // =======================================================
        [HttpGet]
        public async Task<IActionResult> ViewStories(int userId)
        {
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (currentUserId == null) return RedirectToAction("Login", "Account");

            // جلب القصص النشطة لهذا المستخدم
            var twentyFourHoursAgo = DateTime.Now.AddHours(-24);
            var stories = await _db.Stories
                .Include(s => s.User)
                .Where(s => s.UserId == userId && s.CreatedAt >= twentyFourHoursAgo)
                .OrderBy(s => s.CreatedAt)
                .ToListAsync();

            if (!stories.Any())
            {
                return RedirectToAction("Index");
            }

            // [ملاحظة]: يجب إضافة منطق هنا لتسجيل المشاهدة في جدول StoryView

            return View(stories); // سيستخدم ViewStories.cshtml الذي سننشئه
        }

        // صفحة إنشاء منشور جديد
        [HttpGet]
        public IActionResult Create()
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return RedirectToAction("Login", "Account");

            return View();
        }

        // حفظ المنشور في قاعدة البيانات
        [HttpPost]
        public async Task<IActionResult> Create(string content, IFormFile imageFile)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            string imagePath = null;

            if (imageFile != null && imageFile.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(fileStream);
                }

                imagePath = "/uploads/" + uniqueFileName;  // المسار الذي سيتم عرضه في الـ HTML
            }

            var post = new Post
            {
                UserId = userId.Value,
                Content = content,
                ImageUrl = imagePath
            };

            _db.Posts.Add(post);
            await _db.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Details(int id)
        {
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (currentUserId == null)
                return RedirectToAction("Login", "Account");

            var post = await _db.Posts
                .Include(p => p.User)
                .Include(p => p.OriginalPost)
                .Include(p => p.Likes)
                .Include(p => p.Comments)
                    .ThenInclude(c => c.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null) return NotFound();

            ViewBag.CurrentUserId = currentUserId.Value;

            return View(post);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleLike(int postId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Unauthorized();

            var existingLike = await _db.Likes
                .FirstOrDefaultAsync(l => l.UserId == userId && l.PostId == postId);

            var post = await _db.Posts.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == postId);
            if (post == null) return NotFound();

            bool isLiked = false;

            if (existingLike != null)
            {
                // إلغاء الإعجاب
                _db.Likes.Remove(existingLike);
                isLiked = false;
            }
            else
            {
                // إضافة إعجاب
                _db.Likes.Add(new Like
                {
                    UserId = userId.Value,
                    PostId = postId
                });
                isLiked = true;

                var user = await _db.Users.FindAsync(userId.Value);

                if (post.UserId != userId && user != null)
                {
                    // إرسال إشعار لصاحب البوست
                    _db.notifications.Add(new Notification
                    {
                        UserId = post.UserId,
                        Message = $"{user.Username} أعجب بمنشورك.",
                        PostId = postId,
                        Type = "Like"
                    });

                    // إشعار SignalR
                    await _hub.Clients.User(post.UserId.ToString())
                        .SendAsync("ReceiveNotification", $"{user.Username} أعجب بمنشورك.");
                }
            }

            await _db.SaveChangesAsync();

            // جلب العدد الجديد للإعجابات
            var newLikeCount = await _db.Likes.CountAsync(l => l.PostId == postId);

            // ✅ إرجاع JSON بدلاً من التوجيه
            return Json(new { success = true, isLiked = isLiked, newLikeCount = newLikeCount, postId = postId });
        }

        [HttpPost]
        public async Task<IActionResult> AddComment(int postId, string content, int? parentCommentId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(content)) return BadRequest(new { success = false, message = "التعليق فارغ." });

            var user = await _db.Users.FindAsync(userId.Value);
            var post = await _db.Posts.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == postId);

            if (post == null || user == null) return NotFound();

            var comment = new Comment
            {
                PostId = postId,
                UserId = userId.Value,
                Content = content,
                ParentCommentId = parentCommentId,
                CreatedAt = DateTime.Now // لضمان عرض الوقت الصحيح
            };

            _db.Comments.Add(comment);

            // إشعار صاحب البوست
            if (post.UserId != userId)
            {
                _db.notifications.Add(new Notification
                {
                    UserId = post.UserId,
                    Message = $"{user.Username} علق على منشورك.",
                    IsRead = false,
                    PostId = postId,
                    Type = "Comment"
                });

                // إشعار SignalR
                await _hub.Clients.User(post.UserId.ToString())
                    .SendAsync("ReceiveNotification", $"{user.Username} علق على منشورك.");
            }

            await _db.SaveChangesAsync();

            // ✅ إرجاع JSON بدلاً من التوجيه
            return Json(new
            {
                success = true,
                commentId = comment.Id,
                postId = postId,
                username = user.Username,
                content = comment.Content,
                newCommentCount = await _db.Comments.CountAsync(c => c.PostId == postId)
            });
        }

        [HttpPost]
        public async Task<IActionResult> Follow(int userId)
        {
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (currentUserId == null) return RedirectToAction("Login", "Account");

            // 1. التحقق من عدم متابعة النفس والتحقق من وجود المستخدم
            if (currentUserId == userId) return RedirectToAction("Users"); // التوجيه إلى Users بدلاً من Index

            // التأكد من أن المستخدم الذي تتم متابعته موجود فعلاً
            var followedUser = await _db.Users.FindAsync(userId);
            if (followedUser == null) return RedirectToAction("Users");

            // جلب بيانات المستخدم الحالي لإرسال الإشعار
            var currentUser = await _db.Users.FindAsync(currentUserId.Value);
            if (currentUser == null) return RedirectToAction("Users");

            // 2. التحقق من وجود العلاقة مسبقاً
            var existing = await _db.Followers
                .FirstOrDefaultAsync(f => f.FollowerId == currentUserId && f.FollowingId == userId);

            if (existing == null)
            {
                // 3. إضافة المتابعة
                _db.Followers.Add(new Follower
                {
                    FollowerId = currentUserId.Value,
                    FollowingId = userId
                });

                // 4. إضافة إشعار للمستخدم المتابع (UserId)
                _db.notifications.Add(new Notification
                {
                    UserId = userId,
                    Message = $"{currentUser.Username} بدأ في متابعتك.",
                    Type = "Follow"
                    // يمكنك إضافة PostId = null هنا أو تركه كما هو إذا كان قابلاً للقيمة الفارغة
                });

                // 5. حفظ كل التغييرات دفعة واحدة
                await _db.SaveChangesAsync();
            }

            // 6. التوجيه إلى الصفحة التي جاء منها المستخدم
            return RedirectToAction("Users"); // التوجيه إلى صفحة البحث عن المستخدمين
        }

        [HttpPost]
        public async Task<IActionResult> Unfollow(int userId)
        {
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (currentUserId == null) return RedirectToAction("Login", "Account");

            var existing = await _db.Followers
                .FirstOrDefaultAsync(f => f.FollowerId == currentUserId && f.FollowingId == userId);

            if (existing != null)
            {
                _db.Followers.Remove(existing);
                await _db.SaveChangesAsync();
            }

            // التوجيه إلى صفحة البحث عن المستخدمين
            return RedirectToAction("Users");
        }
        [HttpGet]
        public async Task<IActionResult> Users(string search)
        {
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (currentUserId == null) return RedirectToAction("Login", "Account");

            var query = _db.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                // يفضل استخدام ToLower أو ToUpper لضمان البحث غير الحساس لحالة الأحرف في بعض قواعد البيانات
                query = query.Where(u => u.Username.Contains(search));
            }

            var users = await query.ToListAsync();

            // جلب قائمة المتابعين الحاليين
            var followingIds = await _db.Followers
                .Where(f => f.FollowerId == currentUserId)
                .Select(f => f.FollowingId)
                .ToListAsync();

            ViewBag.CurrentUserId = currentUserId.Value;
            ViewBag.FollowingIds = followingIds;

            return View(users);
        }
        // أضف هذه الدالة إلى PostController
        // بديل لدالة Notifications القديمة، تجلب آخر 5 إشعارات غير مقروءة كـ JSON
        // في PostController.cs

        // في ملف المتحكم (PostController.cs) أو حيث توجد هذه الدالة

        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (currentUserId == null) return Unauthorized();

            // ⛔ التعديل هنا: إزالة n.Type != "Message" لجلب كل الإشعارات بما فيها الرسائل
            var notifications = await _db.notifications
                .Where(n => n.UserId == currentUserId) // تم حذف شرط n.Type != "Message"
                .OrderByDescending(n => n.CreatedAt)
                .Take(10)
                .ToListAsync();

            var notificationList = notifications.Select(n => new
            {
                id = n.Id,              // رقم الإشعار (مطلوب لعمل MarkAsRead)
                message = n.Message,
                isRead = n.IsRead,
                createdAt = n.CreatedAt.ToString("g"),
                postId = n.PostId,      // رقم المنشور (مطلوب للانتقال للصفحة)
                notificationType = n.Type // ✅ إضافة Type لتحديد ما إذا كانت رسالة
            }).ToList();

            // عداد الإشعارات غير المقروءة الكلي (يشمل الرسائل والتفاعلات الأخرى)
            var unreadCount = await _db.notifications
                .CountAsync(n => n.UserId == currentUserId && !n.IsRead);

            return Json(new { notifications = notificationList, unreadCount = unreadCount });
        }

        [HttpGet]
        public async Task<IActionResult> Notifications()
        {
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (currentUserId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // جلب جميع الإشعارات الخاصة بالمستخدم بترتيب زمني عكسي
            var notifications = await _db.notifications
                .Where(n => n.UserId == currentUserId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            // يجب أن تكون الدالة NotificationsAction في الكنترولر (PostController)
            // مُعرّفة بالشكل الصحيح في مساراتك إذا كنت تستخدم Routing مُخصص

            return View(notifications);
        }
        // دالة MarkAsRead عدلها لتعيد JSON بدلاً من Redirect
        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int notificationId)
        {
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (currentUserId == null) return Unauthorized();

            var notification = await _db.notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == currentUserId);

            if (notification != null && !notification.IsRead)
            {
                notification.IsRead = true;
                await _db.SaveChangesAsync();

                var newUnreadCount = await _db.notifications
                    .CountAsync(n => n.UserId == currentUserId && !n.IsRead);

                // إرجاع JSON بالعدد الجديد لتحديث الشريط العلوي
                return Json(new { success = true, newUnreadCount = newUnreadCount });
            }

            return Json(new { success = false, newUnreadCount = -1 });
        }

        // يمكنك حذف دالة Notifications القديمة بما أننا سنعتمد على Dropdown
        // public async Task<IActionResult> Notifications() { ... }

        // أضف هذه الدالة إلى PostController
        public async Task<IActionResult> Friends()
        {
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (currentUserId == null) return RedirectToAction("Login", "Account");

            // الخطوة 1: جلب قائمة IDs المستخدمين الذين يتابعهم المستخدم الحالي (Following)
            var followingIds = await _db.Followers
                .Where(f => f.FollowerId == currentUserId)
                .Select(f => f.FollowingId)
                .ToListAsync();

            // الخطوة 2: جلب قائمة IDs المستخدمين الذين يتابعون المستخدم الحالي (Followers)
            var followerIds = await _db.Followers
                .Where(f => f.FollowingId == currentUserId)
                .Select(f => f.FollowerId)
                .ToListAsync();

            // الخطوة 3: إيجاد التقاطع (Intersection) بين القائمتين للحصول على الأصدقاء المشتركين
            var mutualFriendIds = followingIds.Intersect(followerIds).ToList();

            // الخطوة 4: جلب بيانات المستخدمين الذين يمثلون الأصدقاء المشتركين
            var friends = await _db.Users
                .Where(u => mutualFriendIds.Contains(u.Id))
                .OrderBy(u => u.Username)
                .ToListAsync();

            ViewBag.CurrentUserId = currentUserId.Value;

            return View(friends);
        }
        // يمكنك دمج هذه القائمة في Index/PostController لإظهارها في الشريط العلوي
        private async Task<StoryUploadResult> SaveMediaFile(IFormFile mediaFile)
        {
            var extension = Path.GetExtension(mediaFile.FileName)?.ToLowerInvariant();
            string mediaType;
            string[] imageExtensions = { ".jpg", ".jpeg", ".png", ".gif" };
            string[] videoExtensions = { ".mp4", ".mov" };

            if (imageExtensions.Contains(extension))
                mediaType = "Image";
            else if (videoExtensions.Contains(extension))
                mediaType = "Video";
            else
                return null;

            var storiesFolder = Path.Combine(_webHostEnvironment.WebRootPath, "stories");
            if (!Directory.Exists(storiesFolder))
                Directory.CreateDirectory(storiesFolder);

            var uniqueFileName = Guid.NewGuid().ToString() + extension;
            var filePath = Path.Combine(storiesFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await mediaFile.CopyToAsync(stream);
            }

            return new StoryUploadResult
            {
                Url = "/stories/" + uniqueFileName,
                MediaType = mediaType
            };
        }

        // PostController.cs (أضف هذا في أي مكان داخل الكلاس PostController)

        // PostController.cs

        [HttpPost]
        public async Task<IActionResult> SharePost(int OriginalPostId, string? Content)
        {
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (currentUserId == null) return Unauthorized(); // ✅ 1. التحقق من المستخدم الحالي

            // **********************************************
            // ⚠️ أزل التعليق عن هذا Return أولاً للاختبار
            // **********************************************
            // return Ok(new { success = true, postId = OriginalPostId, message = "نجاح وهمي - تم التخطي بنجاح" });

            // 2. محاولة جلب المنشور الأصلي (هذه الخطوة غالباً تسبب الخطأ 500 إذا كان المنشور محذوفاً)
            var originalPost = await _db.Posts
                .Include(p => p.User) // ضروري لتفادي خطأ لاحق عند الإشعار
                .FirstOrDefaultAsync(p => p.Id == OriginalPostId);

            if (originalPost == null)
            {
                return NotFound(new { success = false, message = "المنشور الأصلي غير موجود." });
            }

            // 3. إنشاء المنشور الجديد (المشاركة)
            var sharedPost = new Post
            {
                UserId = currentUserId.Value,
                Content = Content ?? "",
                CreatedAt = DateTime.UtcNow,
                OriginalPostId = originalPost.Id,
                ImageUrl = null
            };

            _db.Posts.Add(sharedPost);

            // 4. جلب بيانات المستخدم الحالي
            var currentUser = await _db.Users.FindAsync(currentUserId.Value);

            // 5. منطق الإشعار (هذه المنطقة قد تسبب الـ Null Reference)
            if (originalPost.UserId != currentUserId.Value && currentUser != null)
            {
                var notificationMessage = $"{currentUser.Username} قام بمشاركة منشورك.";

                _db.notifications.Add(new Notification // تأكد أن لديك موديل Notification صالح
                {
                    UserId = originalPost.UserId,
                    Message = notificationMessage,
                    IsRead = false,
                    PostId = OriginalPostId,
                    Type = "Share",
                    CreatedAt = DateTime.UtcNow
                });

                // 6. إشعار SignalR
                // 🚨 تأكد أن الـ ID المستخدم في Clients.User هو string!
                await _hub.Clients.User(originalPost.UserId.ToString())
                    .SendAsync("ReceiveNotification", notificationMessage);
            }

            // 7. حفظ التغييرات
            await _db.SaveChangesAsync(); // 👈 قد يحدث الخطأ 500 هنا إذا كان هناك قيود في DB غير مُلبّاة

            return Ok(new { success = true, postId = sharedPost.Id });
        } // ℹ️ نماذج داخلية للمساعدة
        // =======================================================
        public class StoryUploadResult
        {
            public string Url { get; set; }
            public string MediaType { get; set; }
        }
    }



}
// في StoryController أو أي Controller تستخدمه
