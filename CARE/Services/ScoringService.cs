namespace CARE.Services
{
    /// <summary>
    /// Scoring-Logik für Demo-Mode und MQTT-Fallback.
    /// ThingsBoardSyncService berechnet Scores direkt in SQL.
    /// Werte hier müssen mit der SQL-Query übereinstimmen!
    /// </summary>
    public class ScoringService
    {
        // ── Weights (must sum to 1.0) ──────────────────────────────
        private const double W_ECO2 = 0.30;
        private const double W_TEMP = 0.20;
        private const double W_HUM = 0.15;
        private const double W_DB = 0.25;
        private const double W_LUX = 0.10;

        public ScoreResult Calculate(double lux, double temp, double humidity, double eco2, double db)
        {
            var sLux = ScoreLux(lux);
            var sTemp = ScoreTemp(temp);
            var sHum = ScoreHumidity(humidity);
            var sEco2 = ScoreEco2(eco2);
            var sDb = ScoreDb(db);

            int total = (int)Math.Round(
                sLux * W_LUX +
                sTemp * W_TEMP +
                sHum * W_HUM +
                sEco2 * W_ECO2 +
                sDb * W_DB
            );

            return new ScoreResult
            {
                Total = total,
                ScoreLux = (int)sLux,
                ScoreTemp = (int)sTemp,
                ScoreHumidity = (int)sHum,
                ScoreEco2 = (int)sEco2,
                ScoreDb = (int)sDb,
            };
        }

        // ── Per-sensor scoring (synced with SQL query!) ───────────

        // Ideal: 300–500 lux, akzeptabel 200–750
        private static double ScoreLux(double v) =>
            v >= 300 && v <= 500 ? 100 :
            v >= 200 && v < 300 ? Lerp(0, 100, (v - 200) / 100) :
            v > 500 && v <= 750 ? Lerp(100, 0, (v - 500) / 250) : 0;

        // Ideal: 19–23 °C, akzeptabel 17–26
        private static double ScoreTemp(double v) =>
            v >= 19 && v <= 23 ? 100 :
            v >= 17 && v < 19 ? Lerp(0, 100, (v - 17) / 2) :
            v > 23 && v <= 26 ? Lerp(100, 0, (v - 23) / 3) : 0;

        // Ideal: 40–60 %, akzeptabel 30–70
        private static double ScoreHumidity(double v) =>
            v >= 40 && v <= 60 ? 100 :
            v >= 30 && v < 40 ? Lerp(0, 100, (v - 30) / 10) :
            v > 60 && v <= 70 ? Lerp(100, 0, (v - 60) / 10) : 0;

        // Ideal: ≤ 800 ppm; schlecht: ≥ 1500 ppm
        private static double ScoreEco2(double v) =>
            v <= 800 ? 100 :
            v <= 1500 ? Lerp(100, 0, (v - 800) / 700) : 0;

        // Ideal: ≤ 45 dB; schlecht: ≥ 70 dB
        private static double ScoreDb(double v) =>
            v <= 45 ? 100 :
            v <= 70 ? Lerp(100, 0, (v - 45) / 25) : 0;

        private static double Lerp(double a, double b, double t) =>
            Math.Clamp(a + (b - a) * t, 0, 100);

        // ── Rank tier ─────────────────────────────────────────────
        public static RankTier GetRank(int score) => score switch
        {
            >= 88 => new RankTier("ELITE", "👑", "#ffd700"),
            >= 72 => new RankTier("GREAT", "🔥", "#00ff88"),
            >= 52 => new RankTier("DECENT", "⚡", "#00e5ff"),
            >= 32 => new RankTier("MEH", "😐", "#ff8c00"),
            _ => new RankTier("TOXIC", "💀", "#ff2d55"),
        };
    }

    public record ScoreResult
    {
        public int Total { get; init; }
        public int ScoreLux { get; init; }
        public int ScoreTemp { get; init; }
        public int ScoreHumidity { get; init; }
        public int ScoreEco2 { get; init; }
        public int ScoreDb { get; init; }
    }

    public record RankTier(string Label, string Emoji, string Color);
}