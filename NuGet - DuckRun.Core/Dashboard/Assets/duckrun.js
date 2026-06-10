(() => {
  const apiBase = new URL("api/", document.baseURI).pathname;
  const state = {
    jobs: [],
    selectedJob: null,
    runs: [],
    selectedRunId: null,
    runDetail: null,
    console: [],
    view: "overview",
    window: 1440,
    runsTake: 25,
    overview: null,
    polling: null,
    consolePolling: null,
    overviewPolling: null,
  };

  const $jobCount = document.getElementById("job-count");
  const $updatedAt = document.getElementById("updated-at");
  const $jobList = document.getElementById("job-list");
  const $detail = document.getElementById("detail");
  const $overview = document.getElementById("overview");
  let $kpiRow, $bars, $chartSub;
  const jobItemTpl = document.getElementById("job-list-item-tpl");
  const detailTpl = document.getElementById("detail-tpl");

  async function api(path, init) {
    const res = await fetch(apiBase + path, {
      headers: { "accept": "application/json" },
      ...init,
    });
    if (!res.ok) {
      const body = await res.text();
      throw new Error(`${res.status} ${res.statusText}: ${body}`);
    }
    if (res.status === 204) return null;
    return res.json();
  }

  function fmtCron(c) { return c || "—"; }

  function fmtTime(iso) {
    if (!iso) return "—";
    const d = new Date(iso);
    return d.toLocaleString(undefined, { dateStyle: "short", timeStyle: "medium" });
  }

  function fmtDuration(ms) {
    if (ms == null) return "—";
    if (ms < 1000) return `${Math.round(ms)} ms`;
    if (ms < 60_000) return `${(ms / 1000).toFixed(2)} s`;
    const m = Math.floor(ms / 60_000);
    const s = ((ms % 60_000) / 1000).toFixed(0);
    return `${m}m ${s}s`;
  }

  function stateClass(s) {
    return (s || "").toLowerCase();
  }

  // --- routing: overview page vs job detail page ---
  function currentRoute() {
    const h = location.hash.replace(/^#\/?/, "");
    if (h.startsWith("jobs/")) return { view: "job", job: decodeURIComponent(h.slice(5)) };
    return { view: "overview", job: null };
  }

  function navHome() { location.hash = "#/"; }
  function navJob(name) { location.hash = `#/jobs/${encodeURIComponent(name)}`; }

  function applyRoute() {
    const r = currentRoute();
    state.view = r.view;
    state.selectedJob = r.job;
    if (r.view !== "job") { state.selectedRunId = null; state.runDetail = null; state.console = []; }
    $overview.hidden = r.view !== "overview";
    $detail.hidden = r.view !== "job";
    renderNav();
    renderJobList();
    if (r.view === "overview") {
      fetchOverview();
    } else {
      state.runs = [];
      renderDetail();
      refreshRuns();
    }
  }

  function renderNav() {
    document.querySelectorAll(".side-nav .nav-item").forEach(el => el.classList.toggle("active", state.view === "overview"));
  }

  // --- overview / analytics ---
  function buildOverviewShell() {
    $overview.innerHTML = `
      <div class="overview-head">
        <span class="overview-title">Runtime overview</span>
        <div class="window-select">
          <button type="button" data-window="60">1h</button>
          <button type="button" data-window="1440" class="active">24h</button>
          <button type="button" data-window="10080">7d</button>
        </div>
      </div>
      <div class="kpi-row" id="kpi-row"></div>
      <div class="panel chart-card">
        <header class="panel-head"><span>Runs over time</span><span class="dim" id="chart-sub"></span></header>
        <div class="bars" id="bars"></div>
        <div class="chart-axis" id="chart-axis"></div>
      </div>`;
    $kpiRow = document.getElementById("kpi-row");
    $bars = document.getElementById("bars");
    $chartSub = document.getElementById("chart-sub");
    $overview.querySelectorAll("[data-window]").forEach(btn => {
      btn.addEventListener("click", () => {
        state.window = parseInt(btn.dataset.window, 10);
        $overview.querySelectorAll("[data-window]").forEach(b => b.classList.toggle("active", b === btn));
        fetchOverview();
      });
    });
  }

  async function fetchOverview() {
    try {
      state.overview = await api(`overview?window=${state.window}&buckets=${bucketsFor(state.window)}`);
      renderOverview();
    } catch (err) {
      $updatedAt.textContent = `error: ${err.message}`;
    }
  }

  function renderOverview() {
    const o = state.overview;
    if (!o || !$kpiRow) return;
    const t = o.totals;
    const srText = t.successRate == null ? "—" : `${t.successRate}%`;
    const exPct = t.finished ? Math.round((t.exceptions / t.finished) * 100) : 0;
    $kpiRow.innerHTML =
      ringTile(t.successRate ?? 0, "var(--olive)", srText, "success rate", `${t.succeeded}/${t.finished} finished`) +
      ringTile(exPct, "var(--brick)", String(t.exceptions), "exceptions", `${t.failed} failed · ${t.timedOut} timed out`) +
      statTile(t.total, "runs in window", false, o.truncated ? "capped at 20k" : "") +
      statTile(t.running, "running now", t.running > 0, "");
    renderBars(o.buckets, o.windowMinutes);
  }

  function ringTile(pct, color, center, label, sub) {
    const r = 26, c = 2 * Math.PI * r, dash = clampPct(pct) / 100 * c;
    return `<div class="kpi kpi-ring">
      <svg class="ring" viewBox="0 0 64 64">
        <circle class="ring-track" cx="32" cy="32" r="${r}"></circle>
        <circle class="ring-fill" cx="32" cy="32" r="${r}" stroke="${color}" stroke-dasharray="${dash.toFixed(1)} ${(c - dash).toFixed(1)}" transform="rotate(-90 32 32)"></circle>
        <text class="ring-text" x="32" y="37">${center}</text>
      </svg>
      <div class="kpi-label">${label}</div>
      <div class="kpi-sub">${sub}</div>
    </div>`;
  }

  function statTile(value, label, accent, sub) {
    return `<div class="kpi kpi-stat">
      <div class="kpi-num${accent ? " accent" : ""}">${value}</div>
      <div class="kpi-label">${label}</div>
      <div class="kpi-sub">${sub || ""}</div>
    </div>`;
  }

  function renderBars(buckets, windowMinutes) {
    if (!$bars) return;
    const totals = buckets.map(b => b.succeeded + b.failed + b.other);
    const max = Math.max(1, ...totals);
    const sum = totals.reduce((a, c) => a + c, 0);
    const seg = (n, cls) => n > 0 ? `<span class="seg ${cls}" style="flex:${n}"></span>` : "";
    $bars.innerHTML = buckets.map((b, i) => {
      const tot = totals[i];
      const h = tot === 0 ? 0 : Math.max(4, (tot / max) * 100);
      const when = fmtBucket(b.start, windowMinutes);
      return `<div class="bar" title="${when} · ${tot} run${tot === 1 ? "" : "s"} (${b.succeeded}✓ ${b.failed}✕)">
        <div class="bar-stack" style="height:${h}%">${seg(b.failed, "f")}${seg(b.other, "o")}${seg(b.succeeded, "s")}</div>
      </div>`;
    }).join("");
    if ($chartSub) $chartSub.textContent = `${windowLabel(windowMinutes)} · ${sum} run${sum === 1 ? "" : "s"}`;
    const axis = document.getElementById("chart-axis");
    if (axis && buckets.length) axis.innerHTML = `<span>${fmtBucket(buckets[0].start, windowMinutes)}</span><span>now</span>`;
  }

  function clampPct(p) { return Math.max(0, Math.min(100, p || 0)); }
  function bucketsFor(min) { return min <= 60 ? 12 : min <= 1440 ? 24 : 28; }
  function windowLabel(min) { return min <= 60 ? "last hour" : min <= 1440 ? "last 24 hours" : "last 7 days"; }
  function fmtBucket(ms, windowMinutes) {
    const d = new Date(ms);
    if (windowMinutes <= 1440) return d.toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" });
    return `${d.toLocaleDateString(undefined, { month: "short", day: "numeric" })} ${d.toLocaleTimeString(undefined, { hour: "2-digit" })}`;
  }

  function renderJobList() {
    $jobList.innerHTML = "";
    if (!state.jobs.length) {
      const li = document.createElement("li");
      li.className = "job-item";
      li.innerHTML = `<div style="padding:14px 24px;color:var(--ink-dim);font-size:12px">No jobs registered.</div>`;
      $jobList.appendChild(li);
      return;
    }
    for (const j of state.jobs) {
      const node = jobItemTpl.content.cloneNode(true);
      const btn = node.querySelector(".job-button");
      node.querySelector(".job-name").textContent = j.name;
      node.querySelector(".job-cron").textContent = fmtCron(j.cron);
      const dot = node.querySelector(".job-state-dot");
      const lastState = j.lastRunState || (j.runningCount > 0 ? "running" : "");
      const dotClass = stateClass(lastState);
      if (dotClass) dot.classList.add(dotClass);   // a job with no runs has no state class — classList.add("") throws
      if (state.selectedJob && state.selectedJob === j.name) btn.classList.add("active");
      btn.addEventListener("click", () => selectJob(j.name));
      $jobList.appendChild(node);
    }
  }

  function renderDetail() {
    $detail.innerHTML = "";
    const back = document.createElement("button");
    back.type = "button";
    back.className = "detail-back";
    back.textContent = "← Overview";
    back.addEventListener("click", navHome);
    $detail.appendChild(back);

    const job = state.jobs.find(j => j.name === state.selectedJob);
    if (!job) {
      const note = document.createElement("div");
      note.className = "empty";
      note.innerHTML = `<p>${state.jobs.length ? "Job not found." : "Loading…"}</p>`;
      $detail.appendChild(note);
      return;
    }

    const node = detailTpl.content.cloneNode(true);
    node.querySelector(".job-title").textContent = job.name;
    node.querySelector(".job-cron-val").textContent = fmtCron(job.cron);
    node.querySelector(".job-next-val").textContent = fmtTime(job.nextRunUtc);
    node.querySelector(".job-conc-val").textContent =
      job.maxConcurrency == null ? "unbounded" : String(job.maxConcurrency);
    node.querySelector(".job-decl-val").textContent =
      `${job.declaringType ?? "?"}.${job.method ?? "?"}`;

    const triggerBtn = node.querySelector('[data-action="trigger"]');
    if (!job.allowManualTrigger) {
      triggerBtn.disabled = true;
      triggerBtn.textContent = "Manual trigger disabled";
    }
    triggerBtn.addEventListener("click", () => triggerJob(job.name));

    const takeSel = node.querySelector("[data-runs-take]");
    if (takeSel) {
      takeSel.value = String(state.runsTake);
      takeSel.addEventListener("change", e => { state.runsTake = parseInt(e.target.value, 10) || 25; refreshRuns(); });
    }
    const closeBtn = node.querySelector("[data-close-console]");
    if (closeBtn) closeBtn.addEventListener("click", closeConsole);

    const tbody = node.querySelector(".runs-body");
    tbody.innerHTML = "";
    if (!state.runs.length) {
      tbody.innerHTML = `<tr><td colspan="6" class="dim">No runs yet.</td></tr>`;
    } else {
      for (const r of state.runs) {
        const tr = document.createElement("tr");
        tr.className = "selectable";
        if (state.selectedRunId === r.id) tr.classList.add("selected");
        tr.innerHTML = `
          <td>${fmtTime(r.startedAt || r.createdAt)}</td>
          <td><span class="state-pill ${stateClass(r.state)}">${r.state}</span></td>
          <td>${r.triggerSource}</td>
          <td>${fmtDuration(r.durationMs)}</td>
          <td class="run-id">${r.id.slice(0, 8)}…</td>
          <td>${r.state === "Running" ? '<button class="btn btn-cancel" data-cancel="' + r.id + '">Cancel</button>' : ""}</td>
        `;
        tr.addEventListener("click", e => {
          if (e.target.closest("button[data-cancel]")) return;
          selectRun(r.id);
        });
        const cancelBtn = tr.querySelector("button[data-cancel]");
        if (cancelBtn) cancelBtn.addEventListener("click", () => cancelRun(r.id));
        tbody.appendChild(tr);
      }
    }

    $detail.appendChild(node);
    renderRunDetail();
  }

  function renderRunDetail() {
    const $runPanel = document.getElementById("run-panel");
    const $console = document.getElementById("console");
    if (!$runPanel || !$console) return;

    if (!state.selectedRunId) {
      $runPanel.hidden = true;
      return;
    }
    $runPanel.hidden = false;
    $runPanel.querySelector(".run-id-label").textContent = state.selectedRunId;

    $console.innerHTML = "";
    if (!state.console.length) {
      $console.innerHTML = `<span class="dim">No console output for this run.</span>`;
    } else {
      const html = state.console.map(e => {
        const cls = `line-${e.level.toLowerCase()}`;
        const t = new Date(e.timestamp).toISOString().substring(11, 23);
        return `<div class="${cls}"><span class="ts">${t}</span><span class="lvl">${e.level.toUpperCase()}</span>${escapeHtml(e.message)}</div>`;
      }).join("");
      $console.innerHTML = html;
      $console.scrollTop = $console.scrollHeight;
    }

    const $errBlock = $runPanel.querySelector(".error-block");
    if (state.runDetail?.errorMessage) {
      $errBlock.hidden = false;
      const stack = state.runDetail.errorStackTrace || state.runDetail.errorMessage;
      $errBlock.querySelector(".error-text").textContent = stack;
    } else {
      $errBlock.hidden = true;
    }
  }

  function escapeHtml(s) {
    return String(s ?? "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;");
  }

  async function refreshJobs() {
    try {
      const jobs = await api("jobs");
      const lastByJob = new Map();
      for (const j of jobs) {
        try {
          const runs = await api(`jobs/${encodeURIComponent(j.name)}/runs?take=1`);
          if (runs.length) lastByJob.set(j.name, runs[0]);
        } catch { /* ignore per-job errors */ }
      }
      for (const j of jobs) {
        const r = lastByJob.get(j.name);
        j.lastRunState = r ? r.state : null;
        j.runningCount = r && r.state === "Running" ? 1 : 0;
      }
      state.jobs = jobs;
      $jobCount.textContent = `${jobs.length} job${jobs.length === 1 ? "" : "s"}`;
      $updatedAt.textContent = `updated ${new Date().toLocaleTimeString()}`;
      renderJobList();
      if (state.view === "job" && state.selectedJob) await refreshRuns();
    } catch (err) {
      $updatedAt.textContent = `error: ${err.message}`;
    }
  }

  async function refreshRuns() {
    if (!state.selectedJob) return;
    try {
      const runs = await api(`jobs/${encodeURIComponent(state.selectedJob)}/runs?take=${state.runsTake}`);
      state.runs = runs;
      renderDetail();
    } catch (err) {
      $updatedAt.textContent = `error: ${err.message}`;
    }
  }

  async function refreshConsole() {
    if (!state.selectedRunId) return;
    try {
      const [detail, console] = await Promise.all([
        api(`runs/${state.selectedRunId}`),
        api(`runs/${state.selectedRunId}/console`),
      ]);
      state.runDetail = detail;
      state.console = console;
      renderRunDetail();
    } catch (err) {
      $updatedAt.textContent = `error: ${err.message}`;
    }
  }

  function selectJob(name) {
    navJob(name);   // hashchange -> applyRoute switches to the job page
  }

  function selectRun(id) {
    state.selectedRunId = id;
    refreshConsole();
  }

  function closeConsole() {
    state.selectedRunId = null;
    state.runDetail = null;
    state.console = [];
    renderDetail();
  }

  async function triggerJob(name) {
    try {
      await api(`jobs/${encodeURIComponent(name)}/trigger`, { method: "POST" });
      await refreshRuns();
    } catch (err) {
      alert(`Trigger failed: ${err.message}`);
    }
  }

  async function cancelRun(id) {
    try {
      await api(`runs/${id}/cancel`, { method: "POST" });
      await refreshRuns();
    } catch (err) {
      alert(`Cancel failed: ${err.message}`);
    }
  }

  buildOverviewShell();
  document.querySelectorAll("[data-nav-home]").forEach(el => el.addEventListener("click", navHome));
  window.addEventListener("hashchange", applyRoute);
  applyRoute();
  refreshJobs();
  state.polling = setInterval(refreshJobs, 3000);
  state.overviewPolling = setInterval(() => { if (state.view === "overview") fetchOverview(); }, 5000);
  state.consolePolling = setInterval(() => {
    if (state.view === "job" && state.selectedRunId) refreshConsole();
  }, 1500);
})();
