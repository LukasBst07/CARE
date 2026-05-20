using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using CARE.Data;
using CARE.Models;

namespace CARE.Services
{
    public class ChallengeFinalizerService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly LiveDataStore _store;
        private readonly ILogger<ChallengeFinalizerService> _log;

        public ChallengeFinalizerService(IServiceScopeFactory sf, LiveDataStore store,
            ILogger<ChallengeFinalizerService> log)
        {
            _scopeFactory = sf;
            _store = store;
            _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(1), ct);
                await FinalizeExpiredChallenges();
            }
        }

        private async Task FinalizeExpiredChallenges()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var toFinalize = await db.Challenges
                .Include(c => c.Results)
                .Where(c => c.EndsAt <= DateTime.UtcNow && !c.Results.Any())
                .ToListAsync();

            if (!toFinalize.Any()) return;

            var live = _store.GetAllRanked();
            if (!live.Any()) return;

            foreach (var challenge in toFinalize)
            {
                var ranked = challenge.Metric switch
                {
                    ChallengeMetric.BestAir => live.OrderByDescending(c => c.ScoreEco2).ToList(),
                    ChallengeMetric.Quietest => live.OrderByDescending(c => c.ScoreDb).ToList(),
                    ChallengeMetric.Brightest => live.OrderByDescending(c => c.ScoreLux).ToList(),
                    ChallengeMetric.BestTemp => live.OrderByDescending(c => c.ScoreTemp).ToList(),
                    ChallengeMetric.BestHumidity => live.OrderByDescending(c => c.ScoreHumidity).ToList(),
                    _ => live.OrderByDescending(c => c.TotalScore).ToList(),
                };

                for (int i = 0; i < ranked.Count; i++)
                {
                    db.ChallengeResults.Add(new ChallengeResult
                    {
                        ChallengeId = challenge.Id,
                        ClassroomName = ranked[i].Name,
                        Position = i + 1,
                        Score = ranked[i].TotalScore,
                        SnapshotAt = DateTime.UtcNow,
                    });
                }
                _log.LogInformation("[Finalizer] Challenge '{T}' finalized", challenge.Title);
            }
            await db.SaveChangesAsync();
        }
    }
}
