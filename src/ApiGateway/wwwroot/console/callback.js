(async () => {
    try {
        const returnTo = await window.PwAuth.handleCallback();
        window.location.replace(returnTo);
    } catch (e) {
        document.getElementById("status").textContent = "Sign-in failed.";
        const err = document.getElementById("error");
        err.textContent = e.message;
        err.style.display = "block";
    }
})();
