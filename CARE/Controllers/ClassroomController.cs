using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CARE.Data;
using CARE.Services;
using CARE.ViewModels;

namespace CARE.Controllers
{
    public class ClassroomController : Controller
    {
        private readonly LiveDataStore  _store;
        private readonly AppDbContext   _db;
        private readonly ScoringService _scoring;
        private readonly BattleNotifier _notifier;

        public ClassroomController(LiveDataStore store, AppDbContext db,
            ScoringService scoring, BattleNotifier notifier)
        {
            _store    = store;
            _db       = db;
            _scoring  = scoring;
            _notifier = notifier;
        }

        [HttpGet("/Classroom/Detail/{name}")]
        public async Task<IActionResult> Detail(string name)
        {
            name = name.ToUpperInvariant();
            var live = _store.Get(name);
            var all  = _store.GetAllRanked();

            var history = await _db.Telemetry
                .Include(t => t.Classroom)
                .Where(t => t.Classroom.Name == name)
                .OrderByDescending(t => t.RecordedAt)
                .Take(30)
                .Select(t => new HistoryPoint(t.RecordedAt, t.TotalScore))
                .ToListAsync();

            var vm = new ClassDetailVm
            {
                ClassName    = name,
                Live         = live,
                History      = history,
                AvgScore     = all.Any() ? Math.Round(all.Average(c => c.TotalScore),  1) : 0,
                AvgTemp      = all.Any() ? Math.Round(all.Average(c => c.Temp),        1) : 0,
                AvgHumidity  = all.Any() ? Math.Round(all.Average(c => c.Humidity),    1) : 0,
                AvgEco2      = all.Any() ? Math.Round(all.Average(c => c.Eco2),        1) : 0,
                AvgDb        = all.Any() ? Math.Round(all.Average(c => c.Db),          1) : 0,
                AvgLux       = all.Any() ? Math.Round(all.Average(c => c.Lux),         1) : 0,
            };

            return View(vm);
        }
        private ClassroomLiveVm BuildVm(string name, ManualSensorVm f)
        {
            var score = _scoring.Calculate(f.Lux, f.Temp, f.Humidity, f.Eco2, f.Db);
            var rank  = ScoringService.GetRank(score.Total);
            var existing = _store.Get(name);
            return new ClassroomLiveVm
            {
                Id            = existing?.Id ?? 0,
                Name          = name,
                Lux           = f.Lux,  Temp     = f.Temp,
                Humidity      = f.Humidity, Eco2  = f.Eco2,  Db = f.Db,
                TotalScore    = score.Total,
                ScoreLux      = score.ScoreLux,    ScoreTemp     = score.ScoreTemp,
                ScoreHumidity = score.ScoreHumidity, ScoreEco2   = score.ScoreEco2,
                ScoreDb       = score.ScoreDb,
                RankLabel = rank.Label, RankEmoji = rank.Emoji, RankColor = rank.Color,
                Online = true, LastSeen = DateTime.UtcNow,
            };
        }
    }
}
