(function () {
    const form = document.getElementById("supportFiltersForm");
    const submit = document.getElementById("supportFilterSubmit");
    let reloadTimer = null;

    function toSafePath(value) {
        const raw = String(value || "").trim();
        if (!raw) {
            return "";
        }

        try {
            const parsed = new URL(raw, window.location.origin);
            if ((parsed.protocol !== "http:" && parsed.protocol !== "https:") || parsed.origin !== window.location.origin) {
                return "";
            }

            return `${parsed.pathname}${parsed.search}${parsed.hash}`.toLowerCase();
        } catch {
            return "";
        }
    }

    function scheduleReload() {
        if (reloadTimer) {
            return;
        }

        reloadTimer = window.setTimeout(function () {
            window.location.reload();
        }, 220);
    }

    if (form && submit) {
        form.addEventListener("submit", function () {
            submit.disabled = true;
            submit.innerHTML = '<span class="spinner-border spinner-border-sm me-2" role="status"></span>Carregando...';
        });
    }

    window.addEventListener("cpm:notification", function (event) {
        const detail = event && event.detail ? event.detail : {};
        const path = toSafePath(detail.actionUrl || "");
        if (path.indexOf("/supporttickets") >= 0) {
            scheduleReload();
        }
    });
})();
