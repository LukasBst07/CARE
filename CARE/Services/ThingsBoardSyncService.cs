using CARE.Services;
using CARE.ViewModels;
using Npgsql;

namespace CARE.Services
{
    public class ThingsBoardSyncService : BackgroundService
    {
        private readonly IConfiguration _cfg;
        private readonly LiveDataStore _store;
        private readonly BattleNotifier _notifier;
        private readonly ILogger<ThingsBoardSyncService> _log;

        // Device-ID → Klassenname
        private readonly Dictionary<string, string> _deviceMap = new()
        {
            { "b5c42a50-0bee-11f1-a190-1d11a42d97d4", "4IT" },
        };

        public ThingsBoardSyncService(IConfiguration cfg, LiveDataStore store,
            BattleNotifier notifier, ILogger<ThingsBoardSyncService> log)
        {
            _cfg = cfg;
            _store = store;
            _notifier = notifier;
            _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            // Warte kurz bis ThingsBoard sicher läuft
            await Task.Delay(TimeSpan.FromSeconds(5), ct);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await SyncAll();
                }
                catch (Exception ex)
                {
                    _log.LogError("[TB Sync] Global error: {Msg}", ex.Message);
                }
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
            }
        }

        private async Task SyncAll()
        {
            // Verbindung zur ThingsBoard-DB (NICHT care_battles!)
            var connStr = "Host=localhost;Port=5432;Database=thingsboard;Username=thingsboard;Password=thingsboard";

            foreach (var (deviceId, className) in _deviceMap)
            {
                try
                {
                    await using var conn = new NpgsqlConnection(connStr);
                    await conn.OpenAsync();

                    // ══════════════════════════════════════════════════════
                    // EINE EINZIGE QUERY: Werte lesen + Scores berechnen
                    // Scoring komplett in SQL, kein C# ScoringService nötig
                    // ══════════════════════════════════════════════════════
                    await using var cmd = new NpgsqlCommand(@"
WITH raw AS (
    SELECT
        MAX(CASE WHEN t.key = 99  THEN COALESCE(t.dbl_v, t.long_v::double precision) END) AS lux,
        MAX(CASE WHEN t.key = 57  THEN COALESCE(t.dbl_v, t.long_v::double precision) END) AS temp,
        MAX(CASE WHEN t.key = 100 THEN COALESCE(t.dbl_v, t.long_v::double precision) END) AS hum,
        MAX(CASE WHEN t.key = 101 THEN COALESCE(t.dbl_v, t.long_v::double precision) END) AS eco2,
        MAX(CASE WHEN t.key = 102 THEN COALESCE(t.dbl_v, t.long_v::double precision) END) AS db,
        MAX(t.ts) AS last_ts
    FROM ts_kv_latest t
    WHERE t.entity_id = @id
      AND t.key IN (57, 99, 100, 101, 102)
),
scores AS (
    SELECT *,
        -- Lux Score (ideal 300-500, akzeptabel 200-750) | Gewicht 10%
        CASE
            WHEN lux BETWEEN 300 AND 500 THEN 100
            WHEN lux BETWEEN 200 AND 299.99 THEN LEAST(100, GREATEST(0, (lux - 200) / 100.0 * 100))
            WHEN lux BETWEEN 500.01 AND 750 THEN LEAST(100, GREATEST(0, 100 - (lux - 500) / 250.0 * 100))
            ELSE 0
        END AS score_lux,
        -- Temp Score (ideal 19-23, akzeptabel 17-26) | Gewicht 20%
        CASE
            WHEN temp BETWEEN 19 AND 23 THEN 100
            WHEN temp BETWEEN 17 AND 18.99 THEN LEAST(100, GREATEST(0, (temp - 17) / 2.0 * 100))
            WHEN temp BETWEEN 23.01 AND 26 THEN LEAST(100, GREATEST(0, 100 - (temp - 23) / 3.0 * 100))
            ELSE 0
        END AS score_temp,
        -- Humidity Score (ideal 40-60, akzeptabel 30-70) | Gewicht 15%
        CASE
            WHEN hum BETWEEN 40 AND 60 THEN 100
            WHEN hum BETWEEN 30 AND 39.99 THEN LEAST(100, GREATEST(0, (hum - 30) / 10.0 * 100))
            WHEN hum BETWEEN 60.01 AND 70 THEN LEAST(100, GREATEST(0, 100 - (hum - 60) / 10.0 * 100))
            ELSE 0
        END AS score_hum,
        -- CO2 Score (ideal <= 800, schlecht >= 1500) | Gewicht 30%
        CASE
            WHEN eco2 <= 800 THEN 100
            WHEN eco2 <= 1500 THEN LEAST(100, GREATEST(0, 100 - (eco2 - 800) / 700.0 * 100))
            ELSE 0
        END AS score_eco2,
        -- Noise Score (ideal <= 45, schlecht >= 70) | Gewicht 25%
        CASE
            WHEN db <= 45 THEN 100
            WHEN db <= 70 THEN LEAST(100, GREATEST(0, 100 - (db - 45) / 25.0 * 100))
            ELSE 0
        END AS score_db
    FROM raw
)
SELECT
    lux, temp, hum, eco2, db,
    last_ts,
    ROUND(score_lux)::int       AS score_lux,
    ROUND(score_temp)::int      AS score_temp,
    ROUND(score_hum)::int       AS score_hum,
    ROUND(score_eco2)::int      AS score_eco2,
    ROUND(score_db)::int        AS score_db,
    ROUND(
        score_lux  * 0.10 +
        score_temp * 0.20 +
        score_hum  * 0.15 +
        score_eco2 * 0.30 +
        score_db   * 0.25
    )::int AS total_score
FROM scores
WHERE lux IS NOT NULL
  AND temp IS NOT NULL
  AND hum IS NOT NULL
  AND eco2 IS NOT NULL
  AND db IS NOT NULL;", conn);

                    cmd.Parameters.AddWithValue("id", Guid.Parse(deviceId));

                    await using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        var lux = reader.GetDouble(0);
                        var temp = reader.GetDouble(1);
                        var hum = reader.GetDouble(2);
                        var eco2 = reader.GetDouble(3);
                        var db = reader.GetDouble(4);
                        var lastTs = reader.GetInt64(5);

                        var scoreLux = reader.GetInt32(6);
                        var scoreTemp = reader.GetInt32(7);
                        var scoreHum = reader.GetInt32(8);
                        var scoreEco2 = reader.GetInt32(9);
                        var scoreDb = reader.GetInt32(10);
                        var total = reader.GetInt32(11);

                        // Rank bestimmen (einzige Logik die in C# bleibt – 5 Zeilen)
                        var (rankLabel, rankEmoji, rankColor) = total switch
                        {
                            >= 88 => ("ELITE", "👑", "#ffd700"),
                            >= 72 => ("GREAT", "🔥", "#00ff88"),
                            >= 52 => ("DECENT", "⚡", "#00e5ff"),
                            >= 32 => ("MEH", "😐", "#ff8c00"),
                            _ => ("TOXIC", "💀", "#ff2d55"),
                        };

                        var vm = new ClassroomLiveVm
                        {
                            Name = className,
                            Lux = lux,
                            Temp = temp,
                            Humidity = hum,
                            Eco2 = eco2,
                            Db = db,
                            TotalScore = total,
                            ScoreLux = scoreLux,
                            ScoreTemp = scoreTemp,
                            ScoreHumidity = scoreHum,
                            ScoreEco2 = scoreEco2,
                            ScoreDb = scoreDb,
                            RankLabel = rankLabel,
                            RankEmoji = rankEmoji,
                            RankColor = rankColor,
                            Online = true,
                            LastSeen = DateTime.UtcNow,
                        };

                        _store.Upsert(vm);
                        await _notifier.PushBoth(vm, _store.GetAllRanked());

                        _log.LogInformation(
                            "[TB Sync] {Class} → lux={Lux} temp={Temp} hum={Hum} co2={Co2} db={Db} → score {Score} ({Rank})",
                            className, lux, temp, hum, eco2, db, total, rankLabel);
                    }
                    else
                    {
                        _log.LogWarning("[TB Sync] {Class} → keine Daten gefunden", className);
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