using AI_Interviwer.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AI_Interviwer.Controllers
{
    public class HomeController : Controller
    {
        private readonly AIDbContext _context;

        public HomeController(AIDbContext context)
        {
            _context = context;
        }
        public async Task<IActionResult> Index()
        {

            return View();
        }
        // 1. Dashboard View (Real Logged-in User ke Mutabiq Dynamic Data)
        public async Task<IActionResult> Dashboard()
        {
            int? currentUserId = HttpContext.Session.GetInt32("UserId");

            if (currentUserId == null)
            {
                return RedirectToAction("Signin", "Account");
            }

            ViewBag.UserName = HttpContext.Session.GetString("UserName") ?? "Developer";

            var userSessions = await _context.InterviewSessions
                                             .Where(s => s.UserId == currentUserId.Value)
                                             .OrderByDescending(s => s.CreatedAt)
                                             .ToListAsync();

            return View(userSessions);
        }

        // 2. Start Interview Session (Jab user form submit karega)
        [HttpPost]
        public async Task<IActionResult> StartSession(string role, string level, int totalLength, string interviewType)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Signin", "Account");
            }

            InterviewSession newSession = new InterviewSession
            {
                UserId = userId.Value,
                Role = role,
                Level = level,
                TotalLength = totalLength,
                InterviewType = interviewType,
                Status = "In-Progress",
                CreatedAt = DateTime.Now
            };

            _context.InterviewSessions.Add(newSession);
            await _context.SaveChangesAsync();

            HttpContext.Session.SetInt32("CurrentSessionId", newSession.SessionId);

            return RedirectToAction("StartInterview", "Interview");
        }

        // 🔥 3. Single Session Delete Action (Fixed Nullable int Conversion)
        [HttpPost]
        public async Task<IActionResult> DeleteSession(int id)
        {
            int? currentUserId = HttpContext.Session.GetInt32("UserId");
            if (currentUserId == null)
            {
                return RedirectToAction("Signin", "Account");
            }

            // 1. Check karein ke session isi user ka hai
            var session = await _context.InterviewSessions
                                        .FirstOrDefaultAsync(s => s.SessionId == id && s.UserId == currentUserId.Value);

            if (session != null)
            {
                // 2. Child questions delete karein (.Value laga kar comparison fix kiya)
                var relatedQuestions = _context.SessionQuestions.Where(q => q.SessionId.HasValue && q.SessionId.Value == id);

                if (relatedQuestions.Any())
                {
                    _context.SessionQuestions.RemoveRange(relatedQuestions);
                }

                // 3. Main session delete karein
                _context.InterviewSessions.Remove(session);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Dashboard");
        }

        // 🔥 4. Clear All History Action (Fixed Nullable int Conversion)
        [HttpPost]
        public async Task<IActionResult> ClearAllHistory()
        {
            int? currentUserId = HttpContext.Session.GetInt32("UserId");
            if (currentUserId == null)
            {
                return RedirectToAction("Signin", "Account");
            }

            // 1. User ke saare sessions ki IDs nikalien
            var userSessionIds = await _context.InterviewSessions
                                               .Where(s => s.UserId == currentUserId.Value)
                                               .Select(s => s.SessionId)
                                               .ToListAsync();

            if (userSessionIds.Any())
            {
                // 2. Un saare sessions ke questions ko match kar ke delete karein
                var allRelatedQuestions = _context.SessionQuestions
                                                  .Where(q => q.SessionId.HasValue && userSessionIds.Contains(q.SessionId.Value));

                if (allRelatedQuestions.Any())
                {
                    _context.SessionQuestions.RemoveRange(allRelatedQuestions);
                }

                // 3. Ab user ke saare sessions uradien
                var userSessions = _context.InterviewSessions.Where(s => s.UserId == currentUserId.Value);
                _context.InterviewSessions.RemoveRange(userSessions);

                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Dashboard");
        }
    }
}