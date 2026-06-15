using AI_Interviwer.Models; // Aap ka exact namespace
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AI_Interviwer.Controllers
{
    public class AccountController : Controller
    {
        private readonly AIDbContext _context; // Aap ka DbContext name yahan aayega

        // Constructor jo database ko inject karega
        public AccountController(AIDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Signup()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Signup(User model, string confirmPassword)
        {
            // Simple check agar fields khali hain
            if (string.IsNullOrEmpty(model.Name) || string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(model.PasswordHash))
            {
                ViewBag.Error = "Must be fill all feilds!";
                return View(model);
            }

            // Check agar password match nahi hua
            if (model.PasswordHash != confirmPassword)
            {
                ViewBag.Error = "Your password does'nt match!";
                return View(model);
            }

            // Check agar email pehle se save hai database mein
            var userExists = await _context.Users.AnyAsync(u => u.Email == model.Email);
            if (userExists)
            {
                ViewBag.Error = "The Email is alread been created!";
                return View(model);
            }

            // Data save karne ki tayaari
            model.CreatedAt = DateTime.Now;

            _context.Users.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Your are register! now you can login.";
            return RedirectToAction("Signin");
        }

        [HttpGet]
        public IActionResult Signin()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Signin(string email, string password)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Please Give Email and Password both!";
                return View();
            }

            // Database se check kiya (Abhi simple match kr rhy hain, hashing baad my dkhnge)
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email && u.PasswordHash == password);

            if (user == null)
            {
                ViewBag.Error = "The Email and Password is wrong!";
                return View();
            }

            // Session mein user ka data rakh diya
            HttpContext.Session.SetInt32("UserId", user.UserId);
            HttpContext.Session.SetString("UserName", user.Name);

            // Login ke baad Dashboard pr bhej diya
            return RedirectToAction("Dashboard", "Home");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Signin");
        }
    }
}