const VOID_STRANDED = "VoidStranded";

const banner    = document.getElementById("banner");
const detailEl  = document.getElementById("detail");
const retryBtn  = document.getElementById("retry-void");
const retryHint = document.getElementById("retry-hint");

const params = new URLSearchParams(window.location.search);
const id     = params.get("id");

const auth = () => window.PwAuth.authHeader();
const fail = (msg) => { banner.textContent = msg; banner.hidden = false; };

async function load() {
    banner.hidden = true;
    detailEl.textContent = "Loading…";
    try {
        const res = await fetch(`/admin/sagas/${encodeURIComponent(id)}`, { headers: auth() });
        if (res.status === 401) {
            await window.PwAuth.redirectToLogin();
            return;
        }
        if (res.status === 404) {
            fail("Saga not found.");
            detailEl.innerHTML = "";
            return;
        }
        if (!res.ok) {
            fail("Failed to load saga: HTTP " + res.status);
            detailEl.innerHTML = "";
            return;
        }
        const saga = await res.json();
        render(saga);
    } catch (e) {
        fail("Failed to load saga: " + e.message);
    }
}

function render(s) {
    const rows = [
        ["CorrelationId",     `<code>${escapeHtml(s.correlationId)}</code>`],
        ["State",              stateBadge(s.currentState)],
        ["Type",               escapeHtml(s.transactionType)],
        ["Amount",            `${escapeHtml(String(s.amount))} ${escapeHtml(s.asset)}`],
        ["Debit account",      escapeHtml(s.debitAccountId ?? "—")],
        ["Credit account",     escapeHtml(s.creditAccountId)],
        ["Void attempts",      escapeHtml(String(s.voidAttempts))],
        ["Hold expiry token",  escapeHtml(s.holdExpiryTokenId ?? "—")],
        ["Failure reason",     escapeHtml(s.failureReason ?? "—")],
        ["Created",            escapeHtml(s.createdAt)],
        ["Updated",            escapeHtml(s.updatedAt)],
    ];
    detailEl.innerHTML = `<dl class="detail">${rows.map(([k, v]) => `<dt>${k}</dt><dd>${v}</dd>`).join("")}</dl>`;

    if (s.currentState === VOID_STRANDED) {
        retryBtn.disabled = false;
        retryHint.textContent = "";
    } else {
        retryBtn.disabled = true;
        retryHint.textContent = `Only enabled when state is ${VOID_STRANDED}.`;
    }
}

async function retryVoid() {
    if (!confirm("Re-publish VoidRequested for this saga?")) return;
    retryBtn.disabled = true;
    try {
        const res = await fetch(`/admin/transactions/${encodeURIComponent(id)}/retry-void`, {
            method:  "POST",
            headers: auth(),
        });
        if (res.status === 401) {
            await window.PwAuth.redirectToLogin();
            return;
        }
        if (res.status === 202 || res.ok) {
            alert("Retry-void accepted. Refreshing…");
            load();
        } else {
            fail("Retry-void failed: HTTP " + res.status);
        }
    } catch (e) {
        fail("Retry-void failed: " + e.message);
    }
}

function escapeHtml(s) {
    return String(s ?? "").replace(/[&<>"']/g, c => ({
        "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
    })[c]);
}

const STATE_TONE = {
    Submitted:    "info",
    Processing:   "info",
    Held:         "warn",
    Completed:    "ok",
    Failed:       "err",
    VoidStranded: "err",
};

function stateBadge(state) {
    const tone = STATE_TONE[state] || "muted";
    return `<span class="badge ${tone}">${escapeHtml(state)}</span>`;
}

retryBtn.addEventListener("click", retryVoid);

(async () => {
    await window.PwAuth.requireLogin();
    if (!id) {
        fail("Missing ?id= parameter.");
        return;
    }
    load();
})();
