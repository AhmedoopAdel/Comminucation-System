using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebApplication10.Models;

namespace WebApplication10.Controllers
{
    
    public class AccountController : Controller
    {
        private readonly AppDbContext _db;
        public AccountController(AppDbContext db) => _db = db;

        [HttpGet]
        
        public IActionResult Register() => View();

        [HttpPost]
       
        public async Task<IActionResult> Register(string username, string email, string password)
        {
            if (await _db.Users.AnyAsync(u => u.Email == email || u.Username == username))
            {
                ViewBag.Error = "Username or Email already exists";
                return View();
            }

            var user = new User
            {
                Username = username,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password) // Ensure the correct namespace is used
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return RedirectToAction("Login");
        }
        [HttpGet]
        
        public IActionResult Login() => View();

        [HttpPost]
        
        public async Task<IActionResult> Login(string email, string password)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                ViewBag.Error = "Invalid email or password";
                return View();
            }

            // تسجيل الدخول باستخدام Session
            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("Username", user.Username);

            return RedirectToAction("Index", "Post"); // للـFeed بعد تسجيل الدخول
        }

        public async Task<IActionResult> Profile(int? id)
        {
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (currentUserId == null) return RedirectToAction("Login", "Account");

            // لو معرفش id يبقى ده ملفه الشخصي
            int userId = id ?? currentUserId.Value;

            var user = await _db.Users
                .Include(u => u.Posts)
                    .ThenInclude(p => p.Likes)
                .Include(u => u.Posts)
                    .ThenInclude(p => p.Comments)
                        .ThenInclude(c => c.User)
                .Include(u => u.Posts)
                    .ThenInclude(p => p.OriginalPost)
                        .ThenInclude(op => op.User)
                .FirstOrDefaultAsync(u => u.Id == userId);


            if (user == null) return NotFound();

            // عدد المتابعين والمتابعين
            var followersCount = await _db.Followers.CountAsync(f => f.FollowingId == userId);
            var followingCount = await _db.Followers.CountAsync(f => f.FollowerId == userId);

            // هل المستخدم الحالي بيتابع ده؟
            bool isFollowing = await _db.Followers.AnyAsync(f => f.FollowerId == currentUserId && f.FollowingId == userId);

            ViewBag.FollowersCount = followersCount;
            ViewBag.FollowingCount = followingCount;
            ViewBag.IsFollowing = isFollowing;
            ViewBag.CurrentUserId = currentUserId.Value;

            return View(user);
        }
        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (currentUserId == null) return RedirectToAction("Login", "Account");

            var user = await _db.Users.FindAsync(currentUserId.Value);
            if (user == null) return NotFound();

            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> EditProfile(string fullName, string bio, string profilePicUrl)
        {
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (currentUserId == null) return RedirectToAction("Login", "Account");

            var user = await _db.Users.FindAsync(currentUserId.Value);
            if (user == null) return NotFound();

            user.FullName = fullName;
            user.Bio = bio;
            user.ProfilePicUrl = profilePicUrl;

            await _db.SaveChangesAsync();
            return RedirectToAction("Profile");
        }
        // أضف هذه الدالة إلى AccountController
        [HttpPost]
        [ValidateAntiForgeryToken] // يفضل استخدام هذه الحماية
        public IActionResult Logout()
        {
            HttpContext.Session.Clear(); // مسح كل بيانات الجلسة
            return RedirectToAction("Login", "Account");
        }
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [AllowAnonymous]
        public IActionResult GoogleLogin()
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = Url.Action("GoogleCallback")
            };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [AllowAnonymous]
        public async Task<IActionResult> GoogleCallback()
        {
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!result.Succeeded)
                return RedirectToAction("Login");

            var email = result.Principal.FindFirst(ClaimTypes.Email)?.Value;
            var name = result.Principal.FindFirst(ClaimTypes.Name)?.Value;

            // لو المستخدم جديد، اعمله تسجيل تلقائي
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                user = new User
                {
                    Email = email,
                    Username = name,
                    PasswordHash = "" // تسجيل بدون باسورد
                };
                _db.Users.Add(user);
                await _db.SaveChangesAsync();
            }

            // سيشن وتسجيل الدخول
            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("Username", user.Username);

            return RedirectToAction("Index", "Post");
        }

    }


}
