// Sagas list page. PKCE-protected — uses window.PwAuth from ../auth.js.
const TAKE = 25;

const banner     = document.getElementById("banner");
const rowsEl     = document.getElementById("rows");
const stateSel   = document.getElementById("state-filter");
const refreshBtn = document.getElementById("refresh");
const prevBtn    = document.getElementById("prev");
const nextBtn    = document.getElementById("next");
const pageInfo   = document.getElementById("page-info");

let skip = 0;

const auth = () => window.PwAuth.authHeader();
const fail = (msg) => { banner.textContent = msg; banner.hidden = false; };

async function load() {
    banner.hidden = true;
    rowsEl.textContent = "Loading…";
    const state = stateSel.value || "";
    const url   = `/admin/sagas/?take=${TAKE}&skip=${skip}` + (state ? `&state=${encodeURIComponent(state)}` : "");
    try {
        const res = await fetch(url, { headers: auth() });
        if (res.status === 401) {
            await window.PwAuth.redirectToLogin();
            return;
        }
        if (!res.ok) {
            fail("Failed to list sagas: HTTP " + res.status);
            rowsEl.innerHTML = "";
            return;
        }
        const body = await res.json();
        render(body.items || []);
        pageInfo.textContent = `Showing ${skip + 1}–${skip + (body.items?.length ?? 0)}`;
        prevBtn.disabled = skip === 0;
        nextBtn.disabled = (body.items?.length ?? 0) < TAKE;
    } catch (e) {
        fail("Failed to list sagas: " + e.message);
    }
}

function render(items) {
    if (items.length === 0) {
        rowsEl.innerHTML = "<p class='empty'>No sagas match.</p>";
        return;
    }
    const rows = items.map(s => `
        <tr>
            <td><code>${escapeHtml(s.correlationId)}</code></td>
            <td>${escapeHtml(s.currentState)}</td>
            <td>${escapeHtml(s.transactionType)}</td>
            <td>${escapeHtml(String(s.amount))} ${escapeHtml(s.asset)}</td>
            <td>${escapeHtml(s.createdAt)}</td>
            <td>${escapeHtml(s.updatedAt)}</td>
            <td><a href="detail.html?id=${encodeURIComponent(s.correlationId)}">View</a></td>
        </tr>
    `).join("");
    rowsEl.innerHTML = `
        <table>
            <thead><tr>
                <th>CorrelationId</th><th>State</th><th>Type</th><th>Amount</th>
                <th>Created</th><th>Updated</th><th>Actions</th>
            </tr></thead>
            <tbody>${rows}</tbody>
        </table>
    `;
}

function escapeHtml(s) {
    return String(s ?? "").replace(/[&<>"']/g, c => ({
        "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
    })[c]);
}

stateSel.addEventListener("change", () => { skip = 0; load(); });
refreshBtn.addEventListener("click", load);
prevBtn.addEventListener("click", () => { skip = Math.max(0, skip - TAKE); load(); });
nextBtn.addEventListener("click", () => { skip += TAKE; load(); });

(async () => {
    await window.PwAuth.requireLogin();
    load();
})();
