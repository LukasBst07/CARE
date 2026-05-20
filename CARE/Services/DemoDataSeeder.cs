using CARE.ViewModels;

namespace CARE.Services
{
    /// <summary>
    /// Seeds demo data only when DEMO_MODE=true in appsettings.
    /// In production: leave empty, ESP32 fills it via MQTT.
    /// </summary>
    public class DemoDataSeeder
    {
        private readonly LiveDataStore  _store;
        private readonly ScoringService _scoring;
        private readonly IConfiguration _cfg;

        public DemoDataSeeder(LiveDataStore store, ScoringService scoring, IConfiguration cfg)
        {
            _store   = store;
            _scoring = scoring;
            _cfg     = cfg;
        }

        public void Seed()
        {
            if (_cfg["DemoMode"] != "true") return;

            var demos = new[]
            {
                ("3AHEL", 480.0, 21.2, 47.0, 650.0, 42.0),
                ("3BHEL", 210.0, 24.5, 65.0, 1420.0, 72.0),
                ("4AHEL", 530.0, 20.8, 51.0, 590.0, 38.0),
                ("4BHEL", 90.0,  27.1, 78.0, 1900.0, 81.0),
            };

            foreach (var (name, lux, temp, hum, eco2, db) in demos)
            {
                var score = _scoring.Calculate(lux, temp, hum, eco2, db);
                var rank  = ScoringService.GetRank(score.Total);
                _store.Upsert(new ClassroomLiveVm
                {
                    Name          = name, Lux = lux, Temp = temp,
                    Humidity      = hum,  Eco2 = eco2, Db = db,
                    TotalScore    = score.Total,    ScoreLux      = score.ScoreLux,
                    ScoreTemp     = score.ScoreTemp, ScoreHumidity = score.ScoreHumidity,
                    ScoreEco2     = score.ScoreEco2, ScoreDb       = score.ScoreDb,
                    RankLabel     = rank.Label, RankEmoji = rank.Emoji, RankColor = rank.Color,
                    Online        = true, LastSeen = DateTime.UtcNow,
                });
            }
        }
    }
}
