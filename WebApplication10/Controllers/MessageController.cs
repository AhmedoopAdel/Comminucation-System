using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WebApplication10.Models;

namespace WebApplication10.Controllers
{
    

        public class MessageController : Controller
        {
        private readonly AppDbContext _db;
        private readonly IHubContext<ChatHub> _chatHub;
        private readonly IHubContext<NotificationHub> _notificationHub; // ⭐ الجديد: حقن Notification Hub

        public MessageController(AppDbContext db, IHubContext<ChatHub> chatHub, IHubContext<NotificationHub> notificationHub)
        {
            _db = db;
            _chatHub = chatHub;
            _notificationHub = notificationHub; // ⭐ الجديد: حقن Notification Hub
        }

        // ----------------------------------------------------
        // دالة Chat: لتمييز الرسائل كمقروءة عند فتح المحادثة
        // ----------------------------------------------------
        public async Task<IActionResult> Chat(int userId)
        {
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (currentUserId == null) return RedirectToAction("Login", "Account");

            // ⭐ الجديد: تمييز الإشعارات المتعلقة بهذه المحادثة كمقروءة
            var unreadNotifications = await _db.notifications
                .Where(n => n.UserId == currentUserId &&
                            n.Type == "Message" &&
                            !n.IsRead)
                .ToListAsync();

            if (unreadNotifications.Any())
            {
                foreach (var n in unreadNotifications)
                {
                    // افتراض أنك لا تحتاج لمعرفة من المرسل في الـ Notification Model لتمييز القراءة
                    // لكن لو أردت، يجب إضافة حقل SenderId للـ Notification Model

                    // تمييز كل الإشعارات "الرسائل" الحالية كمقروءة
                    n.IsRead = true;
                }
                await _db.SaveChangesAsync();

                // ⭐ إرسال تحديث فوري لعداد الإشعارات في شريط التنقل للمستخدم الحالي
                var newUnreadCount = await _db.notifications.CountAsync(n => n.UserId == currentUserId && !n.IsRead);
                await _notificationHub.Clients.User(currentUserId.ToString())
                    .SendAsync("UpdateUnreadCount", newUnreadCount);
            }
            // ----------------------------------------------------

            var messages = await _db.Messages
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .Where(m => (m.SenderId == currentUserId && m.ReceiverId == userId) ||
                            (m.SenderId == userId && m.ReceiverId == currentUserId))
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();

            ViewBag.ReceiverId = userId;
            return View(messages);
        }

        // ----------------------------------------------------
        // دالة SendMessage: لإنشاء إشعار فوري
        // ----------------------------------------------------
        [HttpPost]
        public async Task<IActionResult> SendMessage(int receiverId, string content, IFormFile? file)
        {
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            var senderUsername = HttpContext.Session.GetString("Username");
            if (currentUserId == null) return RedirectToAction("Login", "Account");

            if (string.IsNullOrWhiteSpace(content) && file == null)
                return BadRequest("Message or file required");

            var receiver = await _db.Users.FindAsync(receiverId);
            if (receiver == null)
                return NotFound("Receiver not found");

            string? fileUrl = null;
            if (file != null && file.Length > 0)
            {
                var fileName = Path.GetFileName(file.FileName);
                var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads", fileName);

                using (var stream = new FileStream(path, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                fileUrl = "/uploads/" + fileName;
            }

            var message = new Message
            {
                SenderId = currentUserId.Value,
                ReceiverId = receiverId,
                Content = content,
                FileUrl = fileUrl
            };

            _db.Messages.Add(message);
            await _db.SaveChangesAsync();

            // ----------------------------------------------------
            // ⭐ الكود الجديد: إنشاء الإشعار
            // ----------------------------------------------------
            var notificationMessage = string.IsNullOrWhiteSpace(content) ?
                               $"أرسل لك رسالة {senderUsername} ملفاً." :
                               $"أرسل لك رسالة {senderUsername}: {content}";

            var notification = new Notification
            {
                UserId = receiverId,
                Message = notificationMessage,
                IsRead = false,
                Type = "Message",
                CreatedAt = DateTime.Now // التأكد من حفظ وقت الإنشاء
            };

            _db.notifications.Add(notification);
            await _db.SaveChangesAsync();

            // 1. تحديد الرابط الذي سيتم التوجيه إليه (Chat Page)
            var linkDestination = $"/Message/Chat?userId={currentUserId.Value}";

            // 2. إرسال كائن الإشعار كاملاً عبر SignalR
            var notificationData = new
            {
                id = notification.Id,
                message = notification.Message,
                isRead = notification.IsRead,
                createdAt = notification.CreatedAt.ToString("M/dd/yyyy h:mm tt"), // تهيئة التاريخ للعرض
                type = notification.Type,
                linkDestination = linkDestination // أضفنا الرابط المباشر
            };

            await _notificationHub.Clients.User(receiverId.ToString())
                .SendAsync("ReceiveNotification", notificationData); // نرسل الكائن كاملاً
                                                                     // ----------------------------------------------------

            // ... (الكود الخاص بـ ChatHub) ...

            return Json(new
            {
                content = content,
                fileUrl = fileUrl,
                senderUsername = senderUsername
            });
        }
        public async Task<IActionResult> ChatList()
        {
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (currentUserId == null) return RedirectToAction("Login", "Account");

            // 1. تحديد جميع IDs المستخدمين الآخرين الذين تبادل معهم المستخدم الحالي رسائل
            var contactIds = await _db.Messages
                .Where(m => m.SenderId == currentUserId || m.ReceiverId == currentUserId)
                .Select(m => m.SenderId == currentUserId ? m.ReceiverId : m.SenderId)
                .Distinct()
                .ToListAsync();

            // 2. إنشاء قائمة لتخزين تفاصيل المحادثات
            var conversations = new List<ConversationViewModel>();

            // 3. جلب آخر رسالة لكل جهة اتصال
            foreach (var contactId in contactIds)
            {
                var otherUser = await _db.Users.FindAsync(contactId);

                if (otherUser != null)
                {
                    var lastMessage = await _db.Messages
                        .Where(m => (m.SenderId == currentUserId && m.ReceiverId == contactId) ||
                                    (m.SenderId == contactId && m.ReceiverId == currentUserId))
                        .OrderByDescending(m => m.CreatedAt)
                        .FirstOrDefaultAsync();

                    if (lastMessage != null)
                    {
                        conversations.Add(new ConversationViewModel
                        {
                            OtherUserId = otherUser.Id,
                            OtherUsername = otherUser.Username,
                            LastMessageContent = lastMessage.Content,
                            LastMessageTime = lastMessage.CreatedAt
                        });
                    }
                }
            }

            var sortedConversations = conversations.OrderByDescending(c => c.LastMessageTime).ToList();

            return View(sortedConversations);
        }


    }
}

