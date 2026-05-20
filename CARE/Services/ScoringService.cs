namespace CARE.Services
{
    /// <summary>
    /// Pure scoring logic — easy to tune, no DB dependency.
    /// Each sensor returns 0–100. Weighted total = 0–100.
    /// </summary>
    public class ScoringService
    {
        // ── Weights (must sum to 1.0) ──────────────────────────────
        private const double W_ECO2 = 0.30;
        private const double W_TEMP = 0.20;
        private const double W_HUM  = 0.20;
        private const double W_DB   = 0.20;
        private const double W_LUX  = 0.10;

        public ScoreResult Calculate(double lux, double temp, double humidity, double eco2, double db)
        {
            var sLux  = ScoreLux(lux);
            var sTemp = ScoreTemp(temp);
            var sHum  = ScoreHumidity(humidity);
            var sEco2 = ScoreEco2(eco2);
            var sDb   = ScoreDb(db);

            int total = (int)Math.Round(
                sLux  * W_LUX  +
                sTemp * W_TEMP +
                sHum  * W_HUM  +
                sEco2 * W_ECO2 +
                sDb   * W_DB
            );

            return new ScoreResult
            {
                Total       = total,
                ScoreLux    = (int)sLux,
                ScoreTemp   = (int)sTemp,
                ScoreHumidity = (int)sHum,
                ScoreEco2   = (int)sEco2,
                ScoreDb     = (int)sDb,
            };
        }

        // ── Per-sensor scoring ─────────────────────────────────────

        // Ideal: 300–750 lux
        private static double ScoreLux(double v) =>
            v >= 300 && v <= 750 ? 100 :
            v >= 150 && v < 300  ? Lerp(0, 100, (v - 150) / 150) :
            v > 750 && v <= 2000 ? Lerp(100, 0, (v - 750) / 1250) : 0;

        // Ideal: 20–22 °C
        private static double ScoreTemp(double v) =>
            v >= 20 && v <= 22 ? 100 :
            v >= 16 && v < 20  ? Lerp(0, 100, (v - 16) / 4) :
            v > 22 && v <= 28  ? Lerp(100, 0, (v - 22) / 6) : 0;

        // Ideal: 40–60 %
        private static double ScoreHumidity(double v) =>
            v >= 40 && v <= 60 ? 100 :
            v >= 20 && v < 40  ? Lerp(0, 100, (v - 20) / 20) :
            v > 60 && v <= 80  ? Lerp(100, 0, (v - 60) / 20) : 0;

        // Ideal: ≤ 700 ppm; bad: ≥ 2500 ppm
        private static double ScoreEco2(double v) =>
            v <= 700  ? 100 :
            v <= 2500 ? Lerp(100, 0, (v - 700) / 1800) : 0;

        // Ideal: ≤ 40 dB; bad: ≥ 90 dB
        private static double ScoreDb(double v) =>
            v <= 40 ? 100 :
            v <= 90 ? Lerp(100, 0, (v - 40) / 50) : 0;

        private static double Lerp(double a, double b, double t) =>
            Math.Clamp(a + (b - a) * t, 0, 100);

        // ── Rank tier ─────────────────────────────────────────────
        public static RankTier GetRank(int score) => score switch
        {
            >= 88 => new RankTier("ELITE",  "👑", "#ffd700"),
            >= 72 => new RankTier("GREAT",  "🔥", "#00ff88"),
            >= 52 => new RankTier("DECENT", "⚡", "#00e5ff"),
            >= 32 => new RankTier("MEH",    "😐", "#ff8c00"),
            _     => new RankTier("TOXIC",  "💀", "#ff2d55"),
        };
    }

    public record ScoreResult
    {
        public int Total         { get; init; }
        public int ScoreLux      { get; init; }
        public int ScoreTemp     { get; init; }
        public int ScoreHumidity { get; init; }
        public int ScoreEco2     { get; init; }
        public int ScoreDb       { get; init; }
    }

    public record RankTier(string Label, string Emoji, string Color);
}
