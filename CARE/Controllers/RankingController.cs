using Microsoft.AspNetCore.Mvc;
using CARE.Services;
using CARE.ViewModels;

namespace CARE.Controllers
{
    public class RankingController : Controller
    {
        private readonly LiveDataStore _store;

        public RankingController(LiveDataStore store) => _store = store;

        public IActionResult Index()
        {
            var all = _store.GetAllRanked();

            var vm = new RankingPageVm
            {
                Overall      = all,
                BestAir      = all.OrderByDescending(c => c.ScoreEco2).ToList(),
                Quietest     = all.OrderByDescending(c => c.ScoreDb).ToList(),
                Brightest    = all.OrderByDescending(c => c.ScoreLux).ToList(),
                BestTemp     = all.OrderByDescending(c => c.ScoreTemp).ToList(),
                BestHumidity = all.OrderByDescending(c => c.ScoreHumidity).ToList(),
            };

            return View(vm);
        }
    }
}
