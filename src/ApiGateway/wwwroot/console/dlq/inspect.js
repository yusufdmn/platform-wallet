const PEEK_LIMIT = 50;

const banner    = document.getElementById("banner");
const queueName = document.getElementById("queue-name");
const messages  = document.getElementById("messages");

const params = new URLSearchParams(window.location.search);
const queue  = params.get("queue");

const auth = () => window.PwAuth.authHeader();
const fail = (msg) => { banner.textContent = msg; banner.hidden = false; };

(async () => {
    await window.PwAuth.requireLogin();
    if (!queue) {
        fail("Missing ?queue= parameter.");
        return;
    }
    queueName.textContent = queue;
    loadMessages();
})();

async function loadMessages() {
    try {
        const res = await fetch(`/admin/dlq/${encodeURIComponent(queue)}?take=${PEEK_LIMIT}`, {
            headers: auth(),
        });
        if (res.status === 401) {
            await window.PwAuth.redirectToLogin();
            return;
        }
        if (!res.ok) {
            fail("Failed to peek messages: HTTP " + res.status);
            messages.innerHTML = "";
            return;
        }
        const list = await res.json();
        render(list);
    } catch (e) {
        fail("Failed to peek messages: " + e.message);
    }
}

function render(list) {
    if (!list || list.length === 0) {
        messages.innerHTML = "<p class='empty'>Queue is empty.</p>";
        return;
    }
    const rows = list.map((m, i) => `
        <div class="row">
            <div><strong>#${i + 1}</strong> — encoding: <code>${escapeHtml(m.payload_encoding)}</code></div>
            <pre>${escapeHtml(truncate(m.payload, 1000))}</pre>
            <button class="replay-one">Replay one</button>
        </div>
    `).join("");
    messages.innerHTML = rows;
    messages.querySelectorAll(".replay-one").forEach(btn => {
        btn.addEventListener("click", replayOne);
    });
}

async function replayOne() {
    try {
        const res = await fetch(`/admin/dlq/${encodeURIComponent(queue)}/replay-one`, {
            method: "POST",
            headers: auth(),
        });
        if (res.status === 401) {
            await window.PwAuth.redirectToLogin();
            return;
        }
        if (res.status === 204) {
            alert("Queue was already empty.");
        } else if (res.ok) {
            alert("Replayed 1 message.");
        } else {
            const body = await res.json().catch(() => ({}));
            fail(`Replay-one failed: ${body.detail || res.status}`);
        }
    } catch (e) {
        fail("Replay-one failed: " + e.message);
    }
    loadMessages();
}

function truncate(s, n) {
    s = String(s);
    return s.length > n ? s.slice(0, n) + "…" : s;
}

function escapeHtml(s) {
    return String(s).replace(/[&<>"']/g, c => ({
        "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
    })[c]);
}
