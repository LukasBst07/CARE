using CARE.Services;
using CARE.ViewModels;
using Npgsql;

namespace CARE.Services
{
    public class ThingsBoardSyncService : BackgroundService
    {
        private readonly IConfiguration _cfg;
        private readonly LiveDataStore _store;
        private readonly ScoringService _scoring;
        private readonly ILogger<ThingsBoardSyncService> _log;

        private readonly Dictionary<string, string> _deviceMap = new()
        {
            { "b5c42a50-0bee-11f1-a190-1d11a42d97d4", "2LM" },
        };

        private readonly Dictionary<string, int> _keyIds = new()
        {
            { "lux",  99  },
            { "temp", 57  },
            { "hum",  100 },
            { "eco2", 101 },
            { "db",   102 },
        };

        public ThingsBoardSyncService(IConfiguration cfg, LiveDataStore store,
            ScoringService scoring, ILogger<ThingsBoardSyncService> log)
        {
            _cfg = cfg;
            _store = store;
            _scoring = scoring;
            _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await SyncFromThingsBoard();
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
            }
        }

        private async Task SyncFromThingsBoard()
        {
            var connStr = "Host=localhost;Port=5432;Database=thingsboard;Username=thingsboard;Password=thingsboard";

            foreach (var (deviceId, className) in _deviceMap)
            {
                try
                {
                    await using var conn = new NpgsqlConnection(connStr);
                    await conn.OpenAsync();

                    await using var cmd = new NpgsqlCommand(@"
                SELECT k.key, 
                       COALESCE(t.dbl_v, t.long_v::double precision) as value
                FROM (
                    SELECT key, MAX(ts) as max_ts
                    FROM ts_kv
                    WHERE entity_id = @id
                      AND key = ANY(@keys)
                    GROUP BY key
                ) latest
                JOIN ts_kv t ON t.entity_id = @id 
                             AND t.key = latest.key 
                             AND t.ts = latest.max_ts
                JOIN key_dictionary k ON k.key_id = t.key", conn);

                    cmd.Parameters.AddWithValue("id", Guid.Parse(deviceId));
                    cmd.Parameters.AddWithValue("keys", new int[] { 57, 99, 100, 101, 102 });

                    var sensors = new Dictionary<string, double>();
                    var keyMap = new Dictionary<int, string>
            {
                { 57,  "temp" },
                { 99,  "lux"  },
                { 100, "hum"  },
                { 101, "eco2" },
                { 102, "db"   },
            };

                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var keyName = reader.GetString(0);
                        if (!reader.IsDBNull(1))
                            sensors[keyName] = reader.GetDouble(1);
                    }

                    if (sensors.Count == 5)
                    {
                        var score = _scoring.Calculate(
                            sensors["lux"], sensors["temp"], sensors["hum"],
                            sensors["eco2"], sensors["db"]);
                        var rank = ScoringService.GetRank(score.Total);

                        _store.Upsert(new ClassroomLiveVm
                        {
                            Name = className,
                            Lux = sensors["lux"],
                            Temp = sensors["temp"],
                            Humidity = sensors["hum"],
                            Eco2 = sensors["eco2"],
                            Db = sensors["db"],
                            TotalScore = score.Total,
                            ScoreLux = score.ScoreLux,
                            ScoreTemp = score.ScoreTemp,
                            ScoreHumidity = score.ScoreHumidity,
                            ScoreEco2 = score.ScoreEco2,
                            ScoreDb = score.ScoreDb,
                            RankLabel = rank.Label,
                            RankEmoji = rank.Emoji,
                            RankColor = rank.Color,
                            Online = true,
                            LastSeen = DateTime.UtcNow,
                        });

                        _log.LogInformation("[TB Sync] {Class} → score {Score}", className, score.Total);
                    }
                    else
                    {
                        _log.LogWarning("[TB Sync] {Class} → nur {Count}/5 Werte", className, sensors.Count);
                    }
                }
                catch (Exception ex)
                {
                    _log.LogError("[TB Sync] {Class} Fehler: {Msg}", className, ex.Message);
                }
            }
        }
    }
}