(function () {
    const config = window.cpmAdminCorsSettings || {};
    const configUrl = config.configUrl || "";
    const saveUrl = config.saveUrl || "";

    const errorAlert = document.getElementById("corsConfigAlertError");
    const successAlert = document.getElementById("corsConfigAlertSuccess");
    const input = document.getElementById("corsAllowedOriginsInput");
    const updatedAtLabel = document.getElementById("corsUpdatedAtLabel");
    const countLabel = document.getElementById("corsOriginCountLabel");
    const saveButton = document.getElementById("btnSaveCorsConfig");
    const reloadButton = document.getElementById("btnReloadCorsConfig");

    if (!errorAlert || !successAlert || !input || !updatedAtLabel || !countLabel || !saveButton || !reloadButton || !configUrl || !saveUrl) {
        return;
    }

    function showError(message) {
        errorAlert.textContent = message || "Falha ao processar configuracao de CORS.";
        errorAlert.classList.remove("d-none");
    }

    function clearError() {
        errorAlert.textContent = "";
        errorAlert.classList.add("d-none");
    }

    function showSuccess(message) {
        successAlert.textContent = message;
        successAlert.classList.remove("d-none");
        window.setTimeout(function () {
            successAlert.classList.add("d-none");
        }, 2600);
    }

    function parseOrigins() {
        const lines = String(input.value || "")
            .split(/\r?\n/g)
            .map(function (x) { return x.trim(); })
            .filter(function (x) { return x.length > 0; });

        const dedup = [];
        const seen = new Set();
        lines.forEach(function (origin) {
            const key = origin.toLowerCase();
            if (!seen.has(key)) {
                seen.add(key);
                dedup.push(origin);
            }
        });

        return dedup;
    }

    function updateCountLabel() {
        const total = parseOrigins().length;
        countLabel.textContent = total + (total === 1 ? " origin" : " origins");
    }

    function formatDate(value) {
        if (!value) {
            return "-";
        }

        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return "-";
        }

        return date.toLocaleString("pt-BR");
    }

    async function apiGet(url) {
        const response = await fetch(url, {
            method: "GET",
            credentials: "same-origin"
        });
        const payload = await response.json();
        if (!response.ok || !payload.success) {
            throw new Error(payload.errorMessage || "Falha na chamada da API.");
        }
        return payload.data;
    }

    async function apiPost(url, body) {
        const response = await fetch(url, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            credentials: "same-origin",
            body: JSON.stringify(body || {})
        });

        const payload = await response.json();
        if (!response.ok || !payload.success) {
            throw new Error(payload.errorMessage || "Falha na chamada da API.");
        }

        return payload.data;
    }

    async function loadConfig() {
        clearError();
        const data = await apiGet(configUrl);
        const origins = Array.isArray(data.allowedOrigins) ? data.allowedOrigins : [];
        input.value = origins.join("\n");
        updatedAtLabel.textContent = "Atualizado em: " + formatDate(data.updatedAtUtc);
        updateCountLabel();
    }

    async function saveConfig() {
        clearError();
        saveButton.disabled = true;
        reloadButton.disabled = true;
        try {
            const allowedOrigins = parseOrigins();
            const data = await apiPost(saveUrl, { allowedOrigins: allowedOrigins });
            const persistedOrigins = Array.isArray(data.allowedOrigins) ? data.allowedOrigins : [];
            input.value = persistedOrigins.join("\n");
            updatedAtLabel.textContent = "Atualizado em: " + formatDate(data.updatedAtUtc);
            updateCountLabel();
            showSuccess("Configuracao de CORS salva com sucesso.");
        } finally {
            saveButton.disabled = false;
            reloadButton.disabled = false;
        }
    }

    input.addEventListener("input", updateCountLabel);
    reloadButton.addEventListener("click", function () {
        loadConfig().catch(function (err) {
            showError(err.message || "Falha ao recarregar configuracao de CORS.");
        });
    });
    saveButton.addEventListener("click", function () {
        saveConfig().catch(function (err) {
            showError(err.message || "Falha ao salvar configuracao de CORS.");
        });
    });

    loadConfig().catch(function (err) {
        showError(err.message || "Falha ao carregar configuracao de CORS.");
    });
})();
