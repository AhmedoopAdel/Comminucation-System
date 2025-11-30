using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data.SqlTypes;
using WebApplication10.Models;

namespace WebApplication10.Controllers
{

    public class ReelsController : Controller
    {
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly AppDbContext _context; // افترض اسم الـ DbContext

        public ReelsController(IWebHostEnvironment hostEnvironment, AppDbContext context)
        {
            _hostEnvironment = hostEnvironment;
            _context = context;
        }

        // ----------------------------------------------------------------
        // 1. عمليات الرفع
        // ----------------------------------------------------------------

        public IActionResult Upload()
        {
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (currentUserId == null) return RedirectToAction("Login", "Account");
            return View();
        }

        [HttpPost]
        [DisableRequestSizeLimit]
        [RequestFormLimits(MultipartBodyLengthLimit = 209715200)]
        public async Task<IActionResult> Upload(IFormFile videoFile, string caption)
        {
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (currentUserId == null) return Unauthorized(new { success = false, message = "يرجى تسجيل الدخول أولاً." });

            if (videoFile == null || videoFile.Length == 0)
            {
                return BadRequest(new { success = false, message = "لم يتم اختيار ملف فيديو." });
            }

            try
            {
                // حفظ داخل wwwroot/reels
                var uploadDir = Path.Combine(_hostEnvironment.WebRootPath, "reels");
                if (!Directory.Exists(uploadDir))
                {
                    Directory.CreateDirectory(uploadDir);
                }

                var fileExtension = Path.GetExtension(videoFile.FileName);
                var fileName = Guid.NewGuid().ToString() + fileExtension;
                var filePath = Path.Combine(uploadDir, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await videoFile.CopyToAsync(stream);
                }

                var reel = new Reel
                {
                    UserId = (int)currentUserId,
                    VideoPath = $"/reels/{fileName}",
                    Caption = caption,
                    UploadDate = DateTime.UtcNow // إضافة تاريخ الرفع
                };
                _context.Reels.Add(reel);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, path = reel.VideoPath, message = "تم الرفع بنجاح." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "فشل في عملية الرفع.", error = ex.Message });
            }
        }

        // ----------------------------------------------------------------
        // 2. عمليات التفاعل (Like, Share, Comment)
        // ----------------------------------------------------------------

        [HttpPost]
        public async Task<IActionResult> ToggleLike(int reelId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Unauthorized(new { success = false, message = "Unauthorized" });

            var existingLike = await _context.ReelLikes
                .FirstOrDefaultAsync(l => l.UserId == userId && l.ReelId == reelId);

            bool isLiked;

            if (existingLike != null)
            {
                _context.ReelLikes.Remove(existingLike);
                isLiked = false;
            }
            else
            {
                _context.ReelLikes.Add(new ReelLike { ReelId = reelId, UserId = userId.Value, CreatedAt = DateTime.UtcNow });
                isLiked = true;
            }

            await _context.SaveChangesAsync();

            var totalLikes = await _context.ReelLikes.CountAsync(l => l.ReelId == reelId);

            return Json(new { success = true, isLiked, totalLikes });
        }

        /// <summary>
        /// ينشئ منشورا جديدا يمثل مشاركة للريل على صفحة المستخدم.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ShareReel(int reelId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Unauthorized(new { success = false, message = "Unauthorized" });

            var originalReel = await _context.Reels
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == reelId);

            if (originalReel == null)
            {
                return NotFound(new { success = false, message = "الريل غير موجود." });
            }

            // 1. إنشاء رابط الريل مع تمرير الـ ReelId كمعامل
            // سيفترض أن صفحة الريلز موجودة على مسار /Reels/Feed
            var reelUrl = Url.Action("Feed", "Reels", new { reelId = reelId }, protocol: HttpContext.Request.Scheme);

            // 2. إنشاء محتوى المنشور كـ HTML يتضمن الرابط التشعبي
            var linkText = $"شاهد الريل: \"{originalReel.Caption}\" من @{originalReel.User.Username}";

            // استخدام وسم <a> لإنشاء الرابط القابل للضغط
            var contentWithLink = $"<p>تمت مشاركة ريل:</p><p><a href=\"{reelUrl}\" target=\"_blank\">{linkText}</a></p>";

            var sharedPost = new Post
            {
                UserId = userId.Value,
                // 🔑 استخدام المحتوى الذي يحتوي على الرابط HTML
                Content = contentWithLink,
                CreatedAt = DateTime.UtcNow
            };

            _context.Posts.Add(sharedPost);

            // تحديث عداد المشاركات في جدول الريلز
            var shareEntry = new ReelShare
            {
                ReelId = reelId,
                UserId = userId.Value,
                CreatedAt = DateTime.UtcNow
            };

            _context.ReelShares.Add(shareEntry);
            await _context.SaveChangesAsync();

            var totalShares = await _context.ReelShares.CountAsync(s => s.ReelId == reelId);

            return Json(new { success = true, totalShares = totalShares, postId = sharedPost.Id });
        }
        [HttpPost]
        public async Task<IActionResult> AddComment(int reelId, [FromForm] string content)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Unauthorized(new { success = false, message = "Unauthorized" });

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return Unauthorized(new { success = false, message = "User not found." });

            var comment = new ReelComment
            {
                ReelId = reelId,
                UserId = userId.Value,
                Content = content,
                CreatedAt = DateTime.UtcNow
            };

            _context.ReelComments.Add(comment);
            await _context.SaveChangesAsync();

            var totalComments = await _context.ReelComments.CountAsync(c => c.ReelId == reelId);

            return Json(new
            {
                success = true,
                totalComments = totalComments,
                username = user.Username,
                content = content // 🔑 إضافة مسار الصورة
            });
        }

        /// <summary>
        /// يجلب قائمة التعليقات الخاصة بريل معين.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetComments(int reelId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var username = HttpContext.Session.GetString("Username");
            var comments = await _context.ReelComments
                .Where(c => c.ReelId == reelId)
                
                .OrderBy(c => c.CreatedAt)
                .Select(c => new
                {
                    content = c.Content,
                    username = username,
                })
                .AsNoTracking()
                .ToListAsync();

            return Json(comments);
        }

        // ----------------------------------------------------------------
        // 3. عرض الريلز (الصفحة الرئيسية)
        // ----------------------------------------------------------------

        public IActionResult Feed(int? reelId = null) // 🔑 استقبل الـ ID كمعامل اختياري
        {
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (currentUserId == null) return RedirectToAction("Login", "Account");

            int initialLoadCount = 10;

            // 1. تحديد ترتيب التحميل: الريلز المراد عرضه أولاً
            var query = _context.Reels.AsQueryable();

            if (reelId.HasValue && reelId.Value > 0)
            {
                // 🔑 إذا كان هناك reelId، نتحقق من وجوده ونستخدمه لترتيب الأسبقية
                var targetReel = _context.Reels.FirstOrDefault(r => r.Id == reelId.Value);

                if (targetReel != null)
                {
                    // 💡 الترتيب المطلوب: الريل المحدد أولاً، ثم جميع الريلز الأخرى بترتيب زمني
                    query = query
                        .OrderByDescending(r => r.Id == reelId.Value) // 🔑 هذا يجعل الريل المحدد رقم 1
                        .ThenByDescending(r => r.UploadDate);
                }
                else
                {
                    // إذا لم نجد الريل، نعود للترتيب الزمني الافتراضي
                    query = query.OrderByDescending(r => r.UploadDate);
                }
            }
            else
            {
                // الترتيب الافتراضي: حسب تاريخ التحميل الأحدث
                query = query.OrderByDescending(r => r.UploadDate);
            }

            // 2. تطبيق Skip/Take وتحويل البيانات
            var reels = query
                .Take(initialLoadCount) // تحميل أول دفعة
                .Select(r => new ReelViewModel
                {
                    Id = r.Id,
                    VideoPath = r.VideoPath,
                    Caption = r.Caption,

                    // ... (بقية خصائص الـ ReelViewModel) ...
                    LikesCount = _context.ReelLikes.Count(l => l.ReelId == r.Id),
                    CommentsCount = _context.ReelComments.Count(c => c.ReelId == r.Id),
                    SharesCount = _context.ReelShares.Count(s => s.ReelId == r.Id),
                    IsLikedByMe = _context.ReelLikes.Any(l => l.ReelId == r.Id && l.UserId == currentUserId),

                    Username = r.User.Username,
                    UserId = r.UserId.ToString()
                })
                .AsNoTracking()
                .ToList();

            // 3. تخزين متغيرات JavaScript (ViewBag)
            // 🔑 يجب أن نتأكد من أننا نستخدم التخطي الصحيح في حال وجود ReelId

            // إذا كان هناك reelId، نمرر ReelId إلى Viewbag لاستخدامه في JS للتمرير إليه مباشرة
            ViewBag.TargetReelId = reelId;

            ViewBag.CurrentSkip = initialLoadCount;
            ViewBag.PageSize = 10;

            // نحتاج إلى حساب إجمالي الريلز بدون الأخذ في الاعتبار الترتيب
            ViewBag.HasMoreReels = _context.Reels.Count() > initialLoadCount;

            return View(reels);
        }
        // 🔑 دالة جديدة لجلب المزيد من الريلز عبر AJAX
        [HttpGet]
        public IActionResult LoadMoreReels(int skip, int take)
        {
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (currentUserId == null) return Unauthorized(new { success = false, message = "Unauthorized" });

            var reels = _context.Reels
                .Include(r => r.User)
                .OrderByDescending(r => r.UploadDate)
                .Skip(skip) // تخطي الفيديوهات التي تم تحميلها بالفعل
                .Take(take) // جلب الدفعة التالية
                .Select(r => new ReelViewModel
                {
                    Id = r.Id,
                    VideoPath = r.VideoPath,
                    Caption = r.Caption,

                    // ... (بقية خصائص الـ ReelViewModel) ...
                    LikesCount = _context.ReelLikes.Count(l => l.ReelId == r.Id),
                    CommentsCount = _context.ReelComments.Count(c => c.ReelId == r.Id),
                    SharesCount = _context.ReelShares.Count(s => s.ReelId == r.Id),
                    IsLikedByMe = _context.ReelLikes.Any(l => l.ReelId == r.Id && l.UserId == currentUserId),

                    Username = r.User.Username,
                    UserId = r.UserId.ToString()
                })
                .AsNoTracking()
                .ToList();

            // 🔑 تحديد ما إذا كان هناك المزيد من الفيديوهات بعد هذه الدفعة
            bool hasMore = _context.Reels.Count() > (skip + take);

            return Json(new { reels = reels, hasMore = hasMore });
        }

    }
}