// Failed webhook deliveries — list, retry one, throttled replay-all.
const TAKE = 25;

const banner       = document.getElementById("banner");
const progress     = document.getElementById("progress");
const rowsEl       = document.getElementById("rows");
const statusSel    = document.getElementById("status-filter");
const intervalIn   = document.getElementById("interval-ms");
const maxIn        = document.getElementById("max-count");
const refreshBtn   = document.getElementById("refresh");
const replayAllBtn = document.getElementById("replay-all");

const auth = () => window.PwAuth.authHeader();
const fail = (msg) => { banner.textContent = msg; banner.hidden = false; };

async function loadFailed() {
    banner.hidden = true;
    rowsEl.textContent = "Loading…";
    const status = statusSel.value;
    const url = `/admin/webhooks/failed?take=${TAKE}&skip=0&status=${encodeURIComponent(status)}`;
    try {
        const res = await fetch(url, { headers: auth() });
        if (res.status === 401) {
            await window.PwAuth.redirectToLogin();
            return;
        }
        if (!res.ok) {
            fail("Failed to list deliveries: HTTP " + res.status);
            rowsEl.innerHTML = "";
            return;
        }
        const body = await res.json();
        render(body.items || []);
    } catch (e) {
        fail("Failed to list deliveries: " + e.message);
    }
}

function render(items) {
    if (items.length === 0) {
        rowsEl.innerHTML = "<p class='empty'>No deliveries match.</p>";
        return;
    }
    const rows = items.map(r => `
        <tr>
            <td>${r.id}</td>
            <td>${escapeHtml(r.eventType)}</td>
            <td><code>${escapeHtml(r.correlationId)}</code></td>
            <td>${escapeHtml(r.status)}</td>
            <td>${r.retryCount ?? 0}</td>
            <td>${r.lastHttpStatusCode ?? "—"}</td>
            <td>${escapeHtml(r.failedAt ?? "")}</td>
            <td>${escapeHtml(r.retriedAt ?? "—")}</td>
            <td><button data-id="${r.id}" class="retry-one">Retry</button></td>
        </tr>
    `).join("");
    rowsEl.innerHTML = `
        <table>
            <thead><tr>
                <th>Id</th><th>EventType</th><th>CorrelationId</th><th>Status</th>
                <th>Retries</th><th>LastHTTP</th><th>FailedAt</th><th>RetriedAt</th><th>Actions</th>
            </tr></thead>
            <tbody>${rows}</tbody>
        </table>
    `;
    rowsEl.querySelectorAll(".retry-one").forEach(btn => {
        btn.addEventListener("click", () => retryOne(btn.dataset.id));
    });
}

async function retryOne(id) {
    try {
        const res = await fetch(`/admin/webhooks/${encodeURIComponent(id)}/retry`, {
            method:  "POST",
            headers: auth(),
        });
        if (res.status === 401) {
            await window.PwAuth.redirectToLogin();
            return;
        }
        const body = await res.json().catch(() => ({}));
        if (!res.ok) {
            fail(`Retry failed: HTTP ${res.status} ${body.detail || ""}`);
            return;
        }
        alert(`Retry result: ${body.status} (retries=${body.retry_count}).`);
    } catch (e) {
        fail("Retry failed: " + e.message);
    }
    loadFailed();
}

async function replayAll() {
    const interval = clampInt(intervalIn.value, 100, 60000, 1000);
    const max      = clampInt(maxIn.value, 1, 500, 500);

    const failedCount = await countFailed();
    if (failedCount === null) return;
    if (failedCount === 0) {
        alert("No Failed rows to replay.");
        return;
    }

    const attempt = Math.min(failedCount, max);
    const etaSec  = Math.round((attempt * interval) / 1000);
    if (!confirm(`Replay ${attempt} items at ~${interval}ms apart? ETA ~${etaSec} seconds.`)) return;

    replayAllBtn.disabled = true;
    progress.hidden = false;
    progress.textContent = `Replaying ${attempt} items… ETA ~${etaSec}s.`;
    try {
        const url = `/admin/webhooks/replay-all?confirm=true&intervalMs=${interval}&max=${max}`;
        const res = await fetch(url, { method: "POST", headers: auth() });
        if (res.status === 401) {
            await window.PwAuth.redirectToLogin();
            return;
        }
        const body = await res.json().catch(() => ({}));
        if (!res.ok) {
            fail(`Replay-all failed: HTTP ${res.status} ${body.detail || ""}`);
            return;
        }
        const more = body.attempted === max ? " — more remaining, click again." : "";
        progress.textContent =
            `Done: attempted=${body.attempted}, delivered=${body.delivered}, failed=${body.failed}, ${body.durationMs}ms.${more}`;
    } catch (e) {
        fail("Replay-all failed: " + e.message);
    } finally {
        replayAllBtn.disabled = false;
        loadFailed();
    }
}

async function countFailed() {
    try {
        const res = await fetch(`/admin/webhooks/failed?status=Failed&take=100&skip=0`, { headers: auth() });
        if (res.status === 401) {
            await window.PwAuth.redirectToLogin();
            return null;
        }
        if (!res.ok) return 0;
        const body = await res.json();
        return (body.items ?? []).length;
    } catch {
        return 0;
    }
}

function clampInt(value, lo, hi, fallback) {
    const n = parseInt(value, 10);
    if (Number.isNaN(n)) return fallback;
    if (n < lo) return lo;
    if (n > hi) return hi;
    return n;
}

function escapeHtml(s) {
    return String(s ?? "").replace(/[&<>"']/g, c => ({
        "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
    })[c]);
}

statusSel.addEventListener("change", loadFailed);
refreshBtn.addEventListener("click", loadFailed);
replayAllBtn.addEventListener("click", replayAll);

(async () => {
    await window.PwAuth.requireLogin();
    loadFailed();
})();
