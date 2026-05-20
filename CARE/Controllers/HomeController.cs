using Microsoft.AspNetCore.Mvc;
using CARE.Services;
using CARE.ViewModels;

namespace CARE.Controllers
{
    public class HomeController : Controller
    {
        private readonly LiveDataStore  _store;
        private readonly ScoringService _scoring;
        private readonly BattleNotifier _notifier;

        public HomeController(LiveDataStore store, ScoringService scoring, BattleNotifier notifier)
        {
            _store    = store;
            _scoring  = scoring;
            _notifier = notifier;
        }

        public IActionResult Index()
        {
            return View(_store.GetAllRanked());
        }

        [HttpPost("/api/inject/{name}")]
        public async Task<IActionResult> Inject(string name, [FromBody] InjectPayload p)
        {
            var score = _scoring.Calculate(p.Lux, p.Temp, p.Hum, p.Eco2, p.Db);
            var rank  = ScoringService.GetRank(score.Total);

            var vm = new ClassroomLiveVm
            {
                Name          = name.ToUpperInvariant(),
                Lux           = p.Lux,  Temp     = p.Temp,
                Humidity      = p.Hum,  Eco2     = p.Eco2,  Db = p.Db,
                TotalScore    = score.Total,
                ScoreLux      = score.ScoreLux,    ScoreTemp     = score.ScoreTemp,
                ScoreHumidity = score.ScoreHumidity, ScoreEco2   = score.ScoreEco2,
                ScoreDb       = score.ScoreDb,
                RankLabel = rank.Label, RankEmoji = rank.Emoji, RankColor = rank.Color,
                Online = true, LastSeen = DateTime.UtcNow,
            };

            _store.Upsert(vm);
            await _notifier.PushBoth(vm, _store.GetAllRanked());
            return Ok(new { ok = true, score = score.Total });
        }

        public record InjectPayload(double Lux, double Temp, double Hum, double Eco2, double Db);
    }
}
