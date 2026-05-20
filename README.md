# ⚡ CARE – Classroom Battle System
### ASP.NET Core MVC · SignalR · MQTT · EF Core · SQLite

Live ESP32 classroom competition platform. Classes battle based on real sensor data.

---

## Stack

| Layer     | Technology                              |
|-----------|-----------------------------------------|
| Web App   | ASP.NET Core 8 MVC + Razor Views        |
| Realtime  | SignalR (WebSocket push to browser)     |
| IoT Feed  | MQTT via MQTTnet 4 background service   |
| Database  | Entity Framework Core + SQLite          |
| Design    | Custom CSS (Orbitron + Rajdhani fonts)  |

---

## Folder Structure

```
CARE/
├── Controllers/
│   ├── HomeController.cs       ← Battle page + /api/inject
│   ├── RankingController.cs    ← Leaderboard page
│   └── ClassroomController.cs  ← Class detail page
├── Models/
│   └── Classroom.cs            ← EF Core entities
├── ViewModels/
│   └── ClassroomVm.cs          ← ViewModels + DTOs
├── Views/
│   ├── Home/Index.cshtml       ← Live battle page
│   ├── Ranking/Index.cshtml    ← Ranking page
│   ├── Classroom/Detail.cshtml ← Class detail
│   └── Shared/
│       ├── _Layout.cshtml
│       ├── _BattleCard.cshtml  ← Reusable card partial
│       └── _LeaderRow.cshtml   ← Reusable row partial
├── Services/
│   ├── MqttService.cs          ← Background MQTT subscriber
│   ├── ScoringService.cs       ← Score calculation engine
│   ├── LiveDataStore.cs        ← In-memory hot cache
│   └── DemoDataSeeder.cs       ← Demo data on startup
├── Hubs/
│   └── BattleHub.cs            ← SignalR hub
├── Data/
│   └── AppDbContext.cs         ← EF Core context + seeding
├── wwwroot/
│   ├── css/care.css            ← Full custom stylesheet
│   └── js/care.js              ← SignalR client + DOM updates
└── appsettings.json
```

---

## Quick Start

### Prerequisites
- .NET 8 SDK → https://dotnet.microsoft.com/download
- MQTT Broker (Mosquitto)

### 1. Start MQTT Broker
```bash
# Docker
docker run -d -p 1883:1883 eclipse-mosquitto

# Or install Mosquitto and run:
mosquitto -v
```

### 2. Configure `appsettings.json`
```json
"Mqtt": {
  "Host": "localhost",
  "Port": "1883",
  "Topic": "care/sensors/#"
}
```

### 3. Run the App
```bash
cd CARE
dotnet run
```
Open **https://localhost:5001**

The app auto-migrates the SQLite DB and seeds demo data on first start.

---

## MQTT Integration

**Topic format:**
```
care/sensors/{CLASSNAME}
```

**Payload (JSON):**
```json
{ "lux": 320, "temp": 22.4, "hum": 48, "eco2": 620, "db": 54 }
```

**Data flow:**
```
ESP32 → MQTT Broker → MqttService (BackgroundService)
  → ScoringService.Calculate()
  → LiveDataStore.Upsert()
  → SignalR BattleHub → Browser (instant update)
  → AppDbContext → care.db (persisted)
```

---

## SignalR Events

| Event              | Payload            | When                         |
|--------------------|--------------------|------------------------------|
| `RankingUpdate`    | `ClassroomLiveVm[]`| Every new MQTT message       |
| `ClassroomUpdate`  | `ClassroomLiveVm`  | Every new MQTT message       |

Browser subscribes in each view's `@section Scripts` block.

---

## Scoring System

Each sensor → 0–100 points, weighted total:

| Sensor      | Weight | Ideal Range   |
|-------------|--------|---------------|
| CO₂ (eco2)  | 30 %   | 400–700 ppm   |
| Temperature | 20 %   | 20–22 °C      |
| Humidity    | 20 %   | 40–60 %       |
| Noise (db)  | 20 %   | ≤ 40 dB       |
| Light (lux) | 10 %   | 300–750 lux   |

Tune in `Services/ScoringService.cs`.

**Rank tiers:** 👑 ELITE (≥88) · 🔥 GREAT (≥72) · ⚡ DECENT (≥52) · 😐 MEH (≥32) · 💀 TOXIC (<32)

---

## Test Without ESP32

```bash
curl -X POST https://localhost:5001/api/inject/3AHEL \
  -H "Content-Type: application/json" \
  -k -d '{"lux":450,"temp":21.2,"hum":49,"eco2":630,"db":41}'
```

Or use Mosquitto CLI:
```bash
mosquitto_pub -t care/sensors/3AHEL \
  -m '{"lux":450,"temp":21.2,"hum":49,"eco2":630,"db":41}'
```

---

## ESP32 Wiring

See the included `ESP32_ClassFight.ino` from the previous version.
Update the topic to `care/sensors/{CLASSNAME}`.
