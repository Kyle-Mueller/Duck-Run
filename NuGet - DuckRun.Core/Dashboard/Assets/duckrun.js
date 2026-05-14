(() => {
  const apiBase = new URL("api/", document.baseURI).pathname;
  const state = {
    jobs: [],
    selectedJob: null,
    runs: [],
    selectedRunId: null,
    runDetail: null,
    console: [],
    polling: null,
    consolePolling: null,
  };

  const $jobCount = document.getElementById("job-count");
  const $updatedAt = document.getElementById("updated-at");
  const $jobList = document.getElementById("job-list");
  const $detail = document.getElementById("detail");
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
      dot.classList.add(stateClass(lastState));
      if (state.selectedJob && state.selectedJob === j.name) btn.classList.add("active");
      btn.addEventListener("click", () => selectJob(j.name));
      $jobList.appendChild(node);
    }
  }

  function renderDetail() {
    if (!state.selectedJob) {
      $detail.innerHTML = `<div class="empty"><p>Select a job to see runs and console output.</p></div>`;
      return;
    }
    const job = state.jobs.find(j => j.name === state.selectedJob);
    if (!job) {
      $detail.innerHTML = `<div class="empty"><p>Job not found.</p></div>`;
      return;
    }

    $detail.innerHTML = "";
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
      if (state.selectedJob) await refreshRuns();
      else renderDetail();
    } catch (err) {
      $updatedAt.textContent = `error: ${err.message}`;
    }
  }

  async function refreshRuns() {
    if (!state.selectedJob) return;
    try {
      const runs = await api(`jobs/${encodeURIComponent(state.selectedJob)}/runs?take=50`);
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
    state.selectedJob = name;
    state.selectedRunId = null;
    state.runDetail = null;
    state.console = [];
    refreshRuns();
    renderJobList();
  }

  function selectRun(id) {
    state.selectedRunId = id;
    refreshConsole();
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

  refreshJobs();
  state.polling = setInterval(refreshJobs, 3000);
  state.consolePolling = setInterval(() => {
    if (state.selectedRunId) refreshConsole();
  }, 1500);
})();
