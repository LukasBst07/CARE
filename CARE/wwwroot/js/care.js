/* ═══════════════════════════════════════════════════════
   CARE – Client JS
   · Connects to SignalR /hubs/battle
   · Exposes careSignalR for view-level scripts
   · Updates DOM on ranking/classroom events
   ═══════════════════════════════════════════════════════ */

(function () {
  'use strict';

  // ── SignalR connection ──────────────────────────────────
  const connection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/battle')
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Warning)
    .build();

  // Public event bus
  window.careSignalR = connection;

  const statusDot   = document.querySelector('.status-dot');
  const statusLabel = document.querySelector('.status-label');

  function setStatus(live) {
    if (!statusDot) return;
    statusDot.className  = 'status-dot ' + (live ? 'live' : 'offline');
    statusLabel.textContent = live ? 'LIVE' : 'RECONNECTING';
  }

  connection.onreconnecting(() => setStatus(false));
  connection.onreconnected(() => setStatus(true));
  connection.onclose(() => setStatus(false));

  connection.start()
    .then(() => setStatus(true))
    .catch(e => { console.error('[SignalR] Connect failed:', e); setStatus(false); });

  // ── Score helpers ───────────────────────────────────────
  function scoreColor(s) {
    return s >= 80 ? '#00ff88' : s >= 60 ? '#00e5ff' : s >= 40 ? '#ff8c00' : '#ff2d55';
  }
  function medal(pos) {
    return pos === 1 ? '🥇' : pos === 2 ? '🥈' : pos === 3 ? '🥉' : `#${pos}`;
  }
  function flashEl(el) {
    el.classList.remove('val-flash');
    void el.offsetWidth; // reflow
    el.classList.add('val-flash');
  }

  // ── Battle grid update ──────────────────────────────────
  window.updateBattleGrid = function (classrooms) {
    classrooms.forEach(cls => {
      const card = document.querySelector(`.battle-card[data-name="${cls.name}"]`);
      if (!card) return;

      const sc = scoreColor(cls.totalScore);

      // Score ring
      const ring = card.querySelector('.card-score-ring');
      if (ring) ring.style.background = `conic-gradient(${sc} ${cls.totalScore}%, #1e1e35 0%)`;
      const num = card.querySelector('.card-score-num');
      if (num) { num.textContent = cls.totalScore; num.style.color = sc; flashEl(num); }

      // Score bar
      const bar = card.querySelector('.score-bar');
      if (bar) bar.style.width = cls.totalScore + '%';

      // Rank badge
      const rankBadge = card.querySelector('.badge-rank');
      if (rankBadge) {
        rankBadge.textContent = `${cls.rankEmoji} ${cls.rankLabel}`;
        rankBadge.style.color = cls.rankColor;
        rankBadge.style.borderColor = cls.rankColor + '44';
      }

      // Online badge
      const onlineBadge = card.querySelector('.badge-online');
      if (onlineBadge) {
        onlineBadge.textContent = cls.online ? '● LIVE' : '○ OFFLINE';
        onlineBadge.className   = 'badge-online font-mono ' + (cls.online ? 'online' : 'offline');
      }

      // Sensor cells
      const cells = card.querySelectorAll('.sensor-cell');
      const sensors = [
        { val: cls.eco2?.toFixed(0),     score: cls.scoreEco2 },
        { val: cls.lux?.toFixed(0),      score: cls.scoreLux  },
        { val: cls.temp?.toFixed(1),     score: cls.scoreTemp },
        { val: cls.humidity?.toFixed(0), score: cls.scoreHumidity },
        { val: cls.db?.toFixed(0),       score: cls.scoreDb },
      ];
      cells.forEach((cell, i) => {
        const s = sensors[i];
        if (!s) return;
        const c = scoreColor(s.score);
        const valEl = cell.querySelector('.sensor-val');
        if (valEl) { valEl.textContent = s.val; valEl.style.color = c; flashEl(valEl); }
        const fillEl = cell.querySelector('.sensor-bar-fill');
        if (fillEl) { fillEl.style.width = s.score + '%'; fillEl.style.background = c; }
        cell.style.setProperty('--score-color', c);
        cell.style.borderBottomColor = c;
      });

      // Medal
      const medalEl = card.querySelector('.card-medal');
      if (medalEl) medalEl.textContent = medal(cls.position);
    });
  };

  // ── Leaderboard update ──────────────────────────────────
  window.updateLeaderboard = function (classrooms) {
    const board = document.getElementById('overallBoard');
    if (!board) return;

    board.innerHTML = '';
    classrooms.forEach((cls, i) => {
      const sc = scoreColor(cls.totalScore);
      const m  = medal(i + 1);
      const row = document.createElement('a');
      row.href = `/Classroom/Detail/${cls.name}`;
      row.className = 'leader-row' + (i === 0 ? ' leader-row-first' : '');
      if (i === 0) {
        row.style.background    = `${cls.rankColor}0a`;
        row.style.borderColor   = `${cls.rankColor}33`;
      }
      row.innerHTML = `
        <div class="lr-rank font-display">${m}</div>
        <div class="lr-name-group">
          <span class="lr-name font-display">${cls.name}</span>
          <span class="lr-status font-mono ${cls.online ? 'text-green' : 'text-red'}">${cls.online ? '● LIVE' : '○ OFFLINE'}</span>
        </div>
        <div class="lr-bar-wrap">
          <div class="lr-bar" style="width:${cls.totalScore}%;background:linear-gradient(90deg,${sc}88,${sc})">
            <span class="lr-bar-label font-mono">${cls.totalScore} pts</span>
          </div>
        </div>
        <div class="lr-sensors font-mono">
          <span>🌫 ${cls.eco2?.toFixed(0) ?? '–'}</span>
          <span>🌡 ${cls.temp?.toFixed(1) ?? '–'}</span>
          <span>💧 ${cls.humidity?.toFixed(0) ?? '–'}%</span>
          <span>🔊 ${cls.db?.toFixed(0) ?? '–'}</span>
          <span>💡 ${cls.lux?.toFixed(0) ?? '–'}</span>
        </div>
        <div class="lr-score-wrap">
          <span class="lr-score font-display" style="color:${sc}">${cls.totalScore}</span>
          <span class="font-mono text-dim">/ 100</span>
        </div>`;
      board.appendChild(row);
    });
  };

  // ── Winner banner update ────────────────────────────────
  window.updateWinnerBanner = function (classrooms) {
    const leader = classrooms.find(c => c.online) || classrooms[0];
    if (!leader) return;

    const set = (id, val) => { const el = document.getElementById(id); if (el) el.textContent = val; };
    set('wName',  leader.name);
    set('wScore', leader.totalScore);
    set('wRank',  `${leader.rankEmoji} ${leader.rankLabel}`);
    set('wEco2',  leader.eco2?.toFixed(0));
    set('wTemp',  leader.temp?.toFixed(1));
    set('wHum',   leader.humidity?.toFixed(0));
    set('wDb',    leader.db?.toFixed(0));
    set('wLux',   leader.lux?.toFixed(0));

    const banner = document.getElementById('winnerBanner');
    if (banner) {
      banner.style.setProperty('--rank-color', leader.rankColor);
    }
  };

}());
