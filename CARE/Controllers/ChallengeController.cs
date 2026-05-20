using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CARE.Data;
using CARE.ViewModels;

namespace CARE.Controllers
{
    public class ChallengeController : Controller
    {
        private readonly AppDbContext _db;

        public ChallengeController(AppDbContext db) => _db = db;

        [HttpGet("/Challenges")]
        public async Task<IActionResult> Index()
        {
            var all = await _db.Challenges
                .Include(c => c.Results)
                .OrderByDescending(c => c.StartsAt)
                .ToListAsync();

            var now = DateTime.UtcNow;
            var vm = new ChallengeListVm
            {
                Active = all.Where(c => c.IsActive).ToList(),
                Upcoming = all.Where(c => now < c.StartsAt).ToList(),
                Past = all.Where(c => c.IsOver && c.Results.Any())
                              .Select(c => new ChallengeHistoryVm
                              {
                                  Challenge = c,
                                  Results = c.Results.OrderBy(r => r.Position).ToList(),
                              }).ToList(),
            };
            return View(vm);
        }

        [HttpGet("/Challenges/{id}")]
        public async Task<IActionResult> Detail(int id)
        {
            var challenge = await _db.Challenges
                .Include(c => c.Results)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (challenge is null) return NotFound();

            var vm = new ChallengeHistoryVm
            {
                Challenge = challenge,
                Results = challenge.Results.OrderBy(r => r.Position).ToList(),
            };
            return View(vm);
        }

        [HttpGet("/api/challenges/active")]
        public async Task<IActionResult> ActiveJson()
        {
            var now = DateTime.UtcNow;
            var active = await _db.Challenges
                .Where(c => c.StartsAt <= now && c.EndsAt >= now)
                .Select(c => new {
                    c.Id,
                    c.Title,
                    c.Description,
                    Metric = c.Metric.ToString(),
                    c.EndsAt,
                })
                .ToListAsync();
            return Json(active);
        }

        [HttpGet("/Challenges/History")]
        public async Task<IActionResult> History()
        {
            var past = await _db.Challenges
                .Include(c => c.Results)
                .Where(c => c.EndsAt <= DateTime.UtcNow)
                .OrderByDescending(c => c.EndsAt)
                .ToListAsync();

            var vm = past.Select(c => new ChallengeHistoryVm
            {
                Challenge = c,
                Results = c.Results.OrderBy(r => r.Position).ToList(),
            }).ToList();

            return View(vm);
        }
    [HttpGet("/Challenges/HallOfFame")]
        public async Task<IActionResult> HallOfFame()
        {
            var results = await _db.ChallengeResults
                .Include(r => r.Challenge)
                .Where(r => r.Challenge.EndsAt <= DateTime.UtcNow)
                .ToListAsync();

            var hof = results
                .GroupBy(r => r.ClassroomName)
                .Select(g => new HallOfFameEntryVm
                {
                    ClassroomName = g.Key,
                    Wins = g.Count(r => r.Position == 1),
                    Podiums = g.Count(r => r.Position <= 3),
                    Participations = g.Count(),
                    AvgScore = Math.Round(g.Average(r => r.Score), 1),
                    BestScore = g.Max(r => r.Score),
                })
                .OrderByDescending(h => h.Wins)
                .ThenByDescending(h => h.AvgScore)
                .ToList();

            return View(hof);
        }
    }
}