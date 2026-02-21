(function () {
    const filterForm = document.getElementById("adminSupportFilterForm");
    const submitButton = document.getElementById("adminSupportFilterSubmit");
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

    function isSupportPath(path) {
        return path.indexOf("/adminsupporttickets") >= 0;
    }

    function scheduleReload() {
        if (reloadTimer) {
            return;
        }

        reloadTimer = window.setTimeout(function () {
            window.location.reload();
        }, 220);
    }

    if (filterForm && submitButton) {
        const pageInput = filterForm.querySelector('input[name="page"]');
        const controlsThatResetPage = filterForm.querySelectorAll("select, input[type='text'], input[type='number']");

        controlsThatResetPage.forEach(function (control) {
            control.addEventListener("change", function () {
                if (pageInput) {
                    pageInput.value = "1";
                }
            });
        });

        filterForm.addEventListener("submit", function () {
            submitButton.disabled = true;
            submitButton.innerHTML = '<span class="spinner-border spinner-border-sm me-1" role="status"></span>Aplicando...';
        });
    }

    window.addEventListener("cpm:notification", function (event) {
        const detail = event && event.detail ? event.detail : {};
        const path = toSafePath(detail.actionUrl || "");
        if (isSupportPath(path)) {
            scheduleReload();
        }
    });
})();
