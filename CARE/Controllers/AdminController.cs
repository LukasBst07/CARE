using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CARE.Data;
using CARE.Models;
using CARE.ViewModels;

namespace CARE.Controllers
{
    public class AdminController : Controller
    {
        private readonly UserManager<AppUser> _userMgr;
        private readonly SignInManager<AppUser> _signInMgr;
        private readonly AppDbContext _db;

        public AdminController(UserManager<AppUser> userMgr,
            SignInManager<AppUser> signInMgr, AppDbContext db)
        {
            _userMgr = userMgr;
            _signInMgr = signInMgr;
            _db = db;
        }

        [HttpGet("/Admin/Login")]
        public IActionResult Login() => View();

        [HttpPost("/Admin/Login")]
        public async Task<IActionResult> Login(string email, string password)
        {
            var result = await _signInMgr.PasswordSignInAsync(
                email, password, isPersistent: false, lockoutOnFailure: false);
            if (result.Succeeded)
                return RedirectToAction("Dashboard");
            ViewBag.Error = "Ungültige Zugangsdaten";
            return View();
        }

        [HttpPost("/Admin/Logout")]
        public async Task<IActionResult> Logout()
        {
            await _signInMgr.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("/Admin/Dashboard")]
        public async Task<IActionResult> Dashboard()
        {
            var challenges = await _db.Challenges
                .OrderByDescending(c => c.StartsAt)
                .Take(20).ToListAsync();
            return View(challenges);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("/Admin/Challenge/Create")]
        public IActionResult CreateChallenge()
            => View(new ChallengeFormVm
            {
                StartsAt = DateTime.Today,
                EndsAt = DateTime.Today.AddDays(7)
            });

        [Authorize(Roles = "Admin")]
        [HttpPost("/Admin/Challenge/Create")]
        public async Task<IActionResult> CreateChallenge(ChallengeFormVm form)
        {
            _db.Challenges.Add(new Challenge
            {
                Title = form.Title,
                Description = form.Description ?? "",
                Metric = form.Metric,
                StartsAt = form.StartsAt.ToUniversalTime(),
                EndsAt = form.EndsAt.ToUniversalTime(),
                CreatedAt = DateTime.UtcNow,
            });
            await _db.SaveChangesAsync();
            return RedirectToAction("Dashboard");
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("/Admin/Challenge/Edit/{id}")]
        public async Task<IActionResult> EditChallenge(int id)
        {
            var c = await _db.Challenges.FindAsync(id);
            if (c is null) return NotFound();
            return View("CreateChallenge", new ChallengeFormVm
            {
                Id = c.Id,
                Title = c.Title,
                Description = c.Description ?? "",
                Metric = c.Metric,
                StartsAt = c.StartsAt.ToLocalTime(),
                EndsAt = c.EndsAt.ToLocalTime(),
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("/Admin/Challenge/Edit/{id}")]
        public async Task<IActionResult> EditChallenge(int id, ChallengeFormVm form)
        {
            var c = await _db.Challenges.FindAsync(id);
            if (c is null) return NotFound();
            c.Title = form.Title;
            c.Description = form.Description ?? "";
            c.Metric = form.Metric;
            c.StartsAt = form.StartsAt.ToUniversalTime();
            c.EndsAt = form.EndsAt.ToUniversalTime();
            await _db.SaveChangesAsync();
            return RedirectToAction("Dashboard");
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("/Admin/Challenge/Delete/{id}")]
        public async Task<IActionResult> DeleteChallenge(int id)
        {
            var c = await _db.Challenges.FindAsync(id);
            if (c is not null) { _db.Challenges.Remove(c); await _db.SaveChangesAsync(); }
            return RedirectToAction("Dashboard");
        }
    }
}
