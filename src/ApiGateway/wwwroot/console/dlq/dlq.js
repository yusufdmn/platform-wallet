// DLQ queue list. PKCE-protected; expects an access token in localStorage at
// `pw.access_token` (same scheme as the rest of the console — to be wired up
// when the PKCE flow lands).
const banner   = document.getElementById("banner");
const queuesEl = document.getElementById("queues");

const PEEK_LIMIT = 50;

const token  = () => localStorage.getItem("pw.access_token") || "";
const auth   = () => ({ Authorization: "Bearer " + token() });
const fail   = (msg) => { banner.textContent = msg; banner.hidden = false; };

async function loadQueues() {
    try {
        const res = await fetch("/admin/dlq/", { headers: auth() });
        if (!res.ok) {
            fail("Failed to list DLQ queues: HTTP " + res.status);
            queuesEl.innerHTML = "";
            return;
        }
        const queues = await res.json();
        render(queues);
    } catch (e) {
        fail("Failed to list DLQ queues: " + e.message);
        queuesEl.innerHTML = "";
    }
}

function render(queues) {
    if (!queues || queues.length === 0) {
        queuesEl.innerHTML = "<p class='empty'>No _error queues found.</p>";
        return;
    }
    const rows = queues.map(q => `
        <tr>
            <td><code>${escapeHtml(q.name)}</code></td>
            <td>${q.messages}</td>
            <td>
                <a href="inspect.html?queue=${encodeURIComponent(q.name)}">Inspect</a>
                <button data-queue="${escapeHtml(q.name)}" class="replay-all">Replay all</button>
            </td>
        </tr>
    `).join("");
    queuesEl.innerHTML = `
        <table>
            <thead><tr><th>Queue</th><th>Messages</th><th>Actions</th></tr></thead>
            <tbody>${rows}</tbody>
        </table>
    `;
    queuesEl.querySelectorAll(".replay-all").forEach(btn => {
        btn.addEventListener("click", () => replayAll(btn.dataset.queue));
    });
}

async function replayAll(queue) {
    if (!confirm(`Replay every message in ${queue}?`)) return;
    try {
        const res = await fetch(`/admin/dlq/${encodeURIComponent(queue)}/replay-all?confirm=true`, {
            method: "POST",
            headers: auth(),
        });
        const body = await res.json().catch(() => ({}));
        if (res.ok) {
            alert(`Replayed ${body.replayed ?? 0} messages.`);
        } else {
            fail(`Replay-all failed: ${body.error || res.status}`);
        }
    } catch (e) {
        fail("Replay-all failed: " + e.message);
    }
    loadQueues();
}

function escapeHtml(s) {
    return String(s).replace(/[&<>"']/g, c => ({
        "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
    })[c]);
}

loadQueues();

// Expose for inspect.js
window.__pwDlq = { PEEK_LIMIT, auth, fail, escapeHtml };
