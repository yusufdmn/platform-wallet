// Platform Wallet — Ops Console entry point. PKCE login, then renders the tile grid.
document.addEventListener("DOMContentLoaded", async () => {
    try {
        await window.PwAuth.requireLogin();
    } catch (e) {
        const banner = document.getElementById("banner");
        if (banner) {
            banner.textContent = "Config missing — " + e.message;
            banner.hidden = false;
        }
    }
});
