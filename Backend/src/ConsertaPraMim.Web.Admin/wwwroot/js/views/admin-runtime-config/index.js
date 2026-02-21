(function () {
    const config = window.cpmAdminRuntimeConfig || {};
    const layoutConfig = window.adminLayoutConfig || {};
    const sectionsUrl = config.sectionsUrl || "";
    const saveUrl = config.saveUrl || "";
    const restartApiUrl = config.restartApiUrl || "";
    const loginUrl = String(config.loginUrl || layoutConfig.loginUrl || "/Account/Login");
    const apiBaseUrl = String(layoutConfig.apiBaseUrl || "").trim();
    const apiHealthUrl = resolveApiHealthUrl(config.apiHealthUrl, apiBaseUrl);

    const sectionsList = document.getElementById("runtimeConfigSectionsList");
    const errorAlert = document.getElementById("runtimeConfigAlertError");
    const successAlert = document.getElementById("runtimeConfigAlertSuccess");
    const reloadButton = document.getElementById("btnReloadRuntimeConfigSections");
    const restartOverlay = document.getElementById("runtimeRestartOverlay");
    const restartOverlayMessage = document.getElementById("runtimeRestartOverlayMessage");
    const restartOverlayStatus = document.getElementById("runtimeRestartOverlayStatus");

    if (!sectionsList || !errorAlert || !successAlert || !reloadButton || !sectionsUrl || !saveUrl) {
        return;
    }

    function showError(message) {
        errorAlert.textContent = message || "Falha ao processar configuracao runtime.";
        errorAlert.classList.remove("d-none");
    }

    function clearError() {
        errorAlert.textContent = "";
        errorAlert.classList.add("d-none");
    }

    function showSuccess(message) {
        successAlert.textContent = message || "Operacao concluida.";
        successAlert.classList.remove("d-none");
        window.setTimeout(function () {
            successAlert.classList.add("d-none");
        }, 2600);
    }

    function clearSuccess() {
        successAlert.textContent = "";
        successAlert.classList.add("d-none");
    }

    function resolveApiHealthUrl(explicitHealthUrl, baseUrl) {
        const explicit = String(explicitHealthUrl || "").trim();
        if (explicit) {
            return explicit;
        }

        const normalizedBase = String(baseUrl || "").trim().replace(/\/+$/, "");
        return normalizedBase ? `${normalizedBase}/health` : "";
    }

    function delay(ms) {
        return new Promise(function (resolve) {
            window.setTimeout(resolve, ms);
        });
    }

    function setRestartOverlayStatus(message) {
        if (restartOverlayStatus) {
            restartOverlayStatus.textContent = message || "";
        }
    }

    function showRestartOverlay(message, statusMessage) {
        if (!restartOverlay) {
            return;
        }

        if (restartOverlayMessage && message) {
            restartOverlayMessage.textContent = message;
        }

        setRestartOverlayStatus(statusMessage || "Verificando disponibilidade da API...");
        restartOverlay.classList.add("is-visible");
        restartOverlay.setAttribute("aria-hidden", "false");
        document.body.classList.add("overflow-hidden");
    }

    function hideRestartOverlay() {
        if (!restartOverlay) {
            return;
        }

        restartOverlay.classList.remove("is-visible");
        restartOverlay.setAttribute("aria-hidden", "true");
        document.body.classList.remove("overflow-hidden");
    }

    function redirectToLogin() {
        const returnUrl = `${window.location.pathname || "/"}${window.location.search || ""}${window.location.hash || ""}`;
        const separator = loginUrl.includes("?") ? "&" : "?";
        window.location.assign(`${loginUrl}${separator}returnUrl=${encodeURIComponent(returnUrl)}`);
    }

    async function probeApiHealth() {
        if (!apiHealthUrl) {
            return false;
        }

        const probeUrl = `${apiHealthUrl}${apiHealthUrl.includes("?") ? "&" : "?"}_ts=${Date.now()}`;
        const controller = typeof AbortController === "function" ? new AbortController() : null;
        const requestOptions = {
            method: "GET",
            cache: "no-store",
            mode: "cors",
            credentials: "omit"
        };

        if (controller) {
            requestOptions.signal = controller.signal;
        }

        const timeoutHandle = window.setTimeout(function () {
            if (controller) {
                controller.abort();
            }
        }, 2200);

        try {
            const response = await fetch(probeUrl, requestOptions);
            return response.ok;
        } catch {
            return false;
        } finally {
            window.clearTimeout(timeoutHandle);
        }
    }

    async function waitForApiRestartAndRedirect() {
        if (!restartOverlay || !apiHealthUrl) {
            redirectToLogin();
            return;
        }

        const startedAt = Date.now();
        const waitForOfflineMs = 25000;
        const maxWaitMs = 150000;
        const intervalMs = 1500;
        let hasSeenOffline = false;
        let stableOnlineChecks = 0;

        while (Date.now() - startedAt < maxWaitMs) {
            const elapsedMs = Date.now() - startedAt;
            const isOnline = await probeApiHealth();

            if (!isOnline) {
                hasSeenOffline = true;
                stableOnlineChecks = 0;
                setRestartOverlayStatus("API offline. Reiniciando servico...");
                await delay(intervalMs);
                continue;
            }

            if (hasSeenOffline || elapsedMs > waitForOfflineMs) {
                stableOnlineChecks += 1;
                setRestartOverlayStatus("API online. Validando estabilidade...");

                if (stableOnlineChecks >= 2) {
                    setRestartOverlayStatus("API restabelecida. Redirecionando para login...");
                    await delay(700);
                    redirectToLogin();
                    return;
                }
            } else {
                setRestartOverlayStatus("Solicitacao enviada. Aguardando API iniciar o reinicio...");
            }

            await delay(intervalMs);
        }

        hideRestartOverlay();
        showError("A API nao voltou dentro do tempo esperado. Recarregue a pagina e faca login novamente.");
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
            credentials: "same-origin",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify(body || {})
        });
        const payload = await response.json();
        if (!response.ok || !payload.success) {
            throw new Error(payload.errorMessage || "Falha na chamada da API.");
        }

        return payload.data;
    }

    async function askRestartConfirmation(sectionPath) {
        const message = `A secao ${sectionPath} requer reinicio da API para aplicar as mudancas. Deseja reiniciar agora?`;

        if (window.Swal && typeof window.Swal.fire === "function") {
            const result = await window.Swal.fire({
                icon: "question",
                title: "Reiniciar API agora?",
                text: message,
                showCancelButton: true,
                confirmButtonText: "Sim, reiniciar",
                cancelButtonText: "Agora nao",
                reverseButtons: true
            });

            return Boolean(result && result.isConfirmed);
        }

        return window.confirm(message);
    }

    async function requestApiRestart(sectionPath) {
        if (!restartApiUrl) {
            showError("URL de reinicio da API nao configurada.");
            return;
        }

        const confirmed = await askRestartConfirmation(sectionPath);
        if (!confirmed) {
            return;
        }

        await apiPost(restartApiUrl, {});
        showRestartOverlay(
            "A API esta sendo reiniciada para aplicar as alteracoes de runtime.",
            "Iniciando monitoramento do restart..."
        );
        await delay(600);
        await waitForApiRestartAndRedirect();
    }

    function parseBooleanLike(value) {
        if (typeof value === "boolean") {
            return value;
        }

        if (typeof value === "number") {
            return value !== 0;
        }

        if (typeof value === "string") {
            const normalized = value.trim().toLowerCase();
            if (!normalized) {
                return false;
            }

            if (normalized === "true" || normalized === "1") {
                return true;
            }

            if (normalized === "false" || normalized === "0") {
                return false;
            }
        }

        return false;
    }

    function shouldRequireSeedResetSecurityCode(sectionPath, jsonPayload) {
        if (String(sectionPath || "").trim().toLowerCase() !== "seed") {
            return false;
        }

        try {
            const parsed = JSON.parse(jsonPayload);
            if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) {
                return false;
            }

            return parseBooleanLike(parsed.Reset);
        } catch {
            return false;
        }
    }

    async function askSeedResetSecurityCode() {
        if (window.Swal && typeof window.Swal.fire === "function") {
            const result = await window.Swal.fire({
                icon: "warning",
                title: "Confirmacao de seguranca",
                text: "Para habilitar Seed.Reset=true informe o codigo de seguranca.",
                input: "password",
                inputPlaceholder: "Codigo de seguranca",
                inputAttributes: {
                    autocapitalize: "off",
                    autocomplete: "off",
                    autocorrect: "off"
                },
                showCancelButton: true,
                confirmButtonText: "Confirmar e salvar",
                cancelButtonText: "Cancelar",
                reverseButtons: true,
                inputValidator: function (value) {
                    if (!String(value || "").trim()) {
                        return "Informe o codigo de seguranca.";
                    }
                    return null;
                }
            });

            if (!result || !result.isConfirmed) {
                return null;
            }

            return String(result.value || "").trim();
        }

        const raw = window.prompt("Informe o codigo de seguranca para habilitar Seed.Reset=true:");
        return String(raw || "").trim() || null;
    }

    function prettyJsonText(rawJson) {
        if (!rawJson) {
            return "{}";
        }

        try {
            return JSON.stringify(JSON.parse(rawJson), null, 2);
        } catch {
            return String(rawJson);
        }
    }

    function formatDateTime(value) {
        if (!value) {
            return "-";
        }

        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return "-";
        }

        return date.toLocaleString("pt-BR");
    }

    function escapeHtml(value) {
        return String(value ?? "")
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll("\"", "&quot;")
            .replaceAll("'", "&#39;");
    }

    function autoResizeTextarea(textarea) {
        if (!textarea) {
            return;
        }

        textarea.style.height = "auto";
        textarea.style.height = `${Math.max(textarea.scrollHeight, 120)}px`;
    }

    function autoResizeAllTextareas() {
        sectionsList.querySelectorAll(".runtime-config-section-editor").forEach(function (textarea) {
            autoResizeTextarea(textarea);
        });
    }

    function renderSections(items) {
        sectionsList.innerHTML = "";

        if (!items || items.length === 0) {
            const empty = document.createElement("div");
            empty.className = "text-muted small";
            empty.textContent = "Nenhuma secao configuravel encontrada.";
            sectionsList.appendChild(empty);
            return;
        }

        items.forEach(function (section) {
            const card = document.createElement("div");
            card.className = "border rounded p-3 bg-light-subtle";

            const sectionPath = String(section.sectionPath || "");
            const editorId = `runtime-config-editor-${sectionPath.replace(/[^a-zA-Z0-9_-]/g, "-").toLowerCase()}`;
            const prettyJson = prettyJsonText(section.jsonValue);
            const updatedAtLabel = formatDateTime(section.updatedAtUtc);

            card.innerHTML = `
                <div class="d-flex justify-content-between align-items-start flex-wrap gap-2 mb-2">
                    <div>
                        <div class="fw-semibold">${escapeHtml(section.displayName || sectionPath)}</div>
                        <div class="small text-muted"><code>${escapeHtml(sectionPath)}</code></div>
                    </div>
                    <div class="d-flex align-items-center gap-2">
                        ${section.requiresRestart ? '<span class="badge text-bg-warning">Requer restart</span>' : '<span class="badge text-bg-success">Hot reload</span>'}
                        <button type="button" class="btn btn-sm btn-primary runtime-config-save-btn">Salvar</button>
                    </div>
                </div>
                <div class="small text-muted mb-2">${escapeHtml(section.description || "")}</div>
                <textarea id="${editorId}" class="form-control runtime-config-section-editor" spellcheck="false">${escapeHtml(prettyJson)}</textarea>
                <div class="runtime-config-section-updated mt-2">Atualizado em: ${escapeHtml(updatedAtLabel)}</div>
            `;

            const saveButton = card.querySelector(".runtime-config-save-btn");
            const editor = card.querySelector("textarea");
            editor.addEventListener("input", function () {
                autoResizeTextarea(editor);
            });

            saveButton.addEventListener("click", async function () {
                const originalLabel = saveButton.innerHTML;
                const payload = editor.value || "{}";
                let securityCode = null;

                try {
                    JSON.parse(payload);
                } catch {
                    showError(`JSON invalido na secao ${sectionPath}.`);
                    return;
                }

                if (shouldRequireSeedResetSecurityCode(sectionPath, payload)) {
                    securityCode = await askSeedResetSecurityCode();
                    if (!securityCode) {
                        return;
                    }
                }

                clearError();
                clearSuccess();
                saveButton.disabled = true;
                saveButton.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i>Salvando';
                try {
                    const updatedSection = await apiPost(saveUrl, {
                        sectionPath: sectionPath,
                        jsonValue: payload,
                        securityCode: securityCode
                    });
                    await loadSections();
                    showSuccess(`Secao ${sectionPath} salva com sucesso.`);
                    const requiresRestart = Boolean(
                        (updatedSection && updatedSection.requiresRestart) || section.requiresRestart
                    );
                    if (requiresRestart) {
                        await requestApiRestart(sectionPath);
                    }
                } catch (err) {
                    showError(err.message || `Falha ao salvar secao ${sectionPath}.`);
                } finally {
                    saveButton.disabled = false;
                    saveButton.innerHTML = originalLabel;
                }
            });

            sectionsList.appendChild(card);
            window.requestAnimationFrame(function () {
                autoResizeTextarea(editor);
            });
        });

        window.requestAnimationFrame(function () {
            autoResizeAllTextareas();
        });
    }

    async function loadSections() {
        clearError();
        const data = await apiGet(sectionsUrl);
        const items = Array.isArray(data.items) ? data.items : [];
        renderSections(items);
    }

    reloadButton.addEventListener("click", function () {
        loadSections().catch(function (err) {
            showError(err.message || "Falha ao recarregar secoes runtime.");
        });
    });

    window.addEventListener("resize", function () {
        autoResizeAllTextareas();
    });

    loadSections().catch(function (err) {
        showError(err.message || "Falha ao carregar secoes runtime.");
    });
})();
