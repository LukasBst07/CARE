using System.ComponentModel.DataAnnotations;

namespace CARE.Models
{
    public class Classroom
    {
        public int Id { get; set; }

        [Required, MaxLength(20)]
        public string Name { get; set; } = "";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<TelemetryReading> Readings { get; set; } = new List<TelemetryReading>();
    }

    public class TelemetryReading
    {
        public int Id { get; set; }

        public int ClassroomId { get; set; }
        public Classroom Classroom { get; set; } = null!;

        public double Lux      { get; set; }
        public double Temp     { get; set; }
        public double Humidity { get; set; }
        public double Eco2     { get; set; }
        public double Db       { get; set; }

        public int TotalScore     { get; set; }
        public int ScoreLux       { get; set; }
        public int ScoreTemp      { get; set; }
        public int ScoreHumidity  { get; set; }
        public int ScoreEco2      { get; set; }
        public int ScoreDb        { get; set; }

        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    }
    public enum ChallengeMetric
    {
        OverallScore,
        BestAir,
        Quietest,
        Brightest,
        BestTemp,
        BestHumidity,
    }

    public class Challenge
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public ChallengeMetric Metric { get; set; } = ChallengeMetric.OverallScore;
        public DateTime StartsAt { get; set; }
        public DateTime EndsAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive => DateTime.UtcNow >= StartsAt && DateTime.UtcNow <= EndsAt;
        public bool IsOver => DateTime.UtcNow > EndsAt;

        public ICollection<ChallengeResult> Results { get; set; } = new List<ChallengeResult>();
    }

    public class ChallengeResult
    {
        public int Id { get; set; }
        public int ChallengeId { get; set; }
        public Challenge Challenge { get; set; } = null!;
        public string ClassroomName { get; set; } = "";
        public int Position { get; set; }
        public double MetricValue { get; set; }
        public int Score { get; set; }
        public DateTime SnapshotAt { get; set; } = DateTime.UtcNow;
    }

    // Identity User (Admin)
    public class AppUser : Microsoft.AspNetCore.Identity.IdentityUser { }
}
