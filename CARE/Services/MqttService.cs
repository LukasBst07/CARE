using MQTTnet;
using MQTTnet.Client;
using System.Text;
using System.Text.Json;
using CARE.ViewModels;
using CARE.Data;
using CARE.Models;

namespace CARE.Services
{
    public class MqttService : BackgroundService
    {
        private readonly IConfiguration        _cfg;
        private readonly ILogger<MqttService>  _log;
        private readonly ScoringService        _scoring;
        private readonly LiveDataStore         _store;
        private readonly BattleNotifier        _notifier;
        private readonly IServiceScopeFactory  _scopeFactory;
        private IMqttClient? _client;

        public MqttService(IConfiguration cfg, ILogger<MqttService> log,
            ScoringService scoring, LiveDataStore store,
            BattleNotifier notifier, IServiceScopeFactory scopeFactory)
        {
            _cfg          = cfg;
            _log          = log;
            _scoring      = scoring;
            _store        = store;
            _notifier     = notifier;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            var host  = _cfg["Mqtt:Host"]     ?? "localhost";
            var port  = int.Parse(_cfg["Mqtt:Port"] ?? "1883");
            var user  = _cfg["Mqtt:Username"] ?? "";
            var pass  = _cfg["Mqtt:Password"] ?? "";
            var topic = _cfg["Mqtt:Topic"]    ?? "care/sensors/#";

            var factory = new MqttFactory();
            _client = factory.CreateMqttClient();

            var opts = new MqttClientOptionsBuilder()
                .WithTcpServer(host, port)
                .WithClientId($"care-mvc-{Guid.NewGuid():N}")
                .WithCleanSession();

            if (!string.IsNullOrEmpty(user))
                opts = opts.WithCredentials(user, pass);

            _client.ApplicationMessageReceivedAsync += OnMessage;
            _client.DisconnectedAsync += async _ =>
            {
                if (ct.IsCancellationRequested) return;
                _log.LogWarning("[MQTT] Disconnected – retry in 5s");
                await Task.Delay(5000, ct);
                await TryConnect(opts.Build(), topic, ct);
            };

            await TryConnect(opts.Build(), topic, ct);
            await Task.Delay(Timeout.Infinite, ct);

            if (_client.IsConnected)
                await _client.DisconnectAsync();
        }

        private async Task TryConnect(MqttClientOptions opts, string topic, CancellationToken ct)
        {
            try
            {
                await _client!.ConnectAsync(opts, ct);
                await _client.SubscribeAsync(topic, cancellationToken: ct);
                _log.LogInformation("[MQTT] Connected → {Topic}", topic);
            }
            catch (Exception ex)
            {
                _log.LogError("[MQTT] Connect failed: {Msg}", ex.Message);
            }
        }

        private async Task OnMessage(MqttApplicationMessageReceivedEventArgs args)
        {
            try
            {
                var parts     = args.ApplicationMessage.Topic.Split('/');
                var className = parts[^1].ToUpperInvariant();
                var json      = Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);

                var p = JsonSerializer.Deserialize<SensorPayload>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (p is null) return;

                var score    = _scoring.Calculate(p.Lux, p.Temp, p.Hum, p.Eco2, p.Db);
                var rank     = ScoringService.GetRank(score.Total);
                var existing = _store.Get(className);

                var vm = new ClassroomLiveVm
                {
                    Id            = existing?.Id ?? 0,
                    Name          = className,
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

                _ = PersistAsync(className, p, score);
                _log.LogInformation("[MQTT] {Class} → score {Score}", className, score.Total);
            }
            catch (Exception ex)
            {
                _log.LogError("[MQTT] Error: {Msg}", ex.Message);
            }
        }

        private async Task PersistAsync(string name, SensorPayload p, ScoreResult s)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var classroom = db.Classrooms.FirstOrDefault(c => c.Name == name)
                         ?? db.Classrooms.Add(new Classroom { Name = name, CreatedAt = DateTime.UtcNow }).Entity;
            await db.SaveChangesAsync();
            db.Telemetry.Add(new TelemetryReading
            {
                ClassroomId = classroom.Id, Lux = p.Lux, Temp = p.Temp,
                Humidity = p.Hum, Eco2 = p.Eco2, Db = p.Db,
                TotalScore = s.Total, ScoreLux = s.ScoreLux, ScoreTemp = s.ScoreTemp,
                ScoreHumidity = s.ScoreHumidity, ScoreEco2 = s.ScoreEco2, ScoreDb = s.ScoreDb,
                RecordedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        private record SensorPayload(double Lux, double Temp, double Hum, double Eco2, double Db);
    }
}
