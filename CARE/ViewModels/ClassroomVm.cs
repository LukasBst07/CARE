using CARE.Models;

namespace CARE.ViewModels
{
    public record ClassroomLiveVm
    {
        public int    Id         { get; init; }
        public string Name       { get; init; } = "";
        public int    Position   { get; init; }

        public double Lux      { get; init; }
        public double Temp     { get; init; }
        public double Humidity { get; init; }
        public double Eco2     { get; init; }
        public double Db       { get; init; }

        public int TotalScore     { get; init; }
        public int ScoreLux       { get; init; }
        public int ScoreTemp      { get; init; }
        public int ScoreHumidity  { get; init; }
        public int ScoreEco2      { get; init; }
        public int ScoreDb        { get; init; }

        public string RankLabel { get; init; } = "MEH";
        public string RankEmoji { get; init; } = "😐";
        public string RankColor { get; init; } = "#ff8c00";

        public bool     Online   { get; init; }
        public DateTime LastSeen { get; init; }

        public string LastSeenAgo =>
            (DateTime.UtcNow - LastSeen) switch
            {
                var t when t.TotalSeconds < 60 => $"{(int)t.TotalSeconds}s ago",
                var t when t.TotalMinutes < 60 => $"{(int)t.TotalMinutes}m ago",
                _                              => "long ago",
            };
    }

    public class RankingPageVm
    {
        public IReadOnlyList<ClassroomLiveVm> Overall      { get; init; } = [];
        public IReadOnlyList<ClassroomLiveVm> BestAir      { get; init; } = [];
        public IReadOnlyList<ClassroomLiveVm> Quietest     { get; init; } = [];
        public IReadOnlyList<ClassroomLiveVm> Brightest    { get; init; } = [];
        public IReadOnlyList<ClassroomLiveVm> BestTemp     { get; init; } = [];
        public IReadOnlyList<ClassroomLiveVm> BestHumidity { get; init; } = [];
    }

    public class ClassDetailVm
    {
        public ClassroomLiveVm? Live      { get; init; }
        public string           ClassName { get; init; } = "";
        public double AvgScore    { get; init; }
        public double AvgTemp     { get; init; }
        public double AvgHumidity { get; init; }
        public double AvgEco2     { get; init; }
        public double AvgDb       { get; init; }
        public double AvgLux      { get; init; }
        public List<HistoryPoint> History { get; init; } = [];
    }

    public record HistoryPoint(DateTime Ts, int Score);

    public class ManualSensorVm
    {
        public string Name     { get; set; } = "";
        public double Lux      { get; set; } = 400;
        public double Temp     { get; set; } = 21;
        public double Humidity { get; set; } = 50;
        public double Eco2     { get; set; } = 700;
        public double Db       { get; set; } = 45;
    }
    public class ChallengeFormVm
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public ChallengeMetric Metric { get; set; } = ChallengeMetric.OverallScore;
        public DateTime StartsAt { get; set; } = DateTime.Today;
        public DateTime EndsAt { get; set; } = DateTime.Today.AddDays(7);
    }

    public class ChallengeListVm
    {
        public List<CARE.Models.Challenge> Active { get; init; } = [];
        public List<CARE.Models.Challenge> Upcoming { get; init; } = [];
        public List<ChallengeHistoryVm> Past { get; init; } = [];
    }

    public class ChallengeHistoryVm
    {
        public CARE.Models.Challenge Challenge { get; init; } = null!;
        public List<CARE.Models.ChallengeResult> Results { get; init; } = [];
        public CARE.Models.ChallengeResult? Winner => Results.FirstOrDefault(r => r.Position == 1);
    }
    public class HallOfFameEntryVm
    {
        public string ClassroomName { get; init; } = "";
        public int Wins { get; init; }
        public int Podiums { get; init; }
        public int Participations { get; init; }
        public double AvgScore { get; init; }
        public int BestScore { get; init; }
    }
}
