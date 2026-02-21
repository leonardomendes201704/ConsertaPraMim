(function () {
    const config = window.cpmAdminRuntimeConfig || {};
    const sectionsUrl = config.sectionsUrl || "";
    const saveUrl = config.saveUrl || "";
    const restartApiUrl = config.restartApiUrl || "";

    const sectionsList = document.getElementById("runtimeConfigSectionsList");
    const errorAlert = document.getElementById("runtimeConfigAlertError");
    const successAlert = document.getElementById("runtimeConfigAlertSuccess");
    const reloadButton = document.getElementById("btnReloadRuntimeConfigSections");

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

        const restartMessage = "Reinicio da API solicitado. Aguarde alguns segundos para a aplicacao voltar.";
        if (window.Swal && typeof window.Swal.fire === "function") {
            await window.Swal.fire({
                icon: "success",
                title: "Reinicio solicitado",
                text: restartMessage,
                timer: 2500,
                showConfirmButton: false
            });
        }

        showSuccess(restartMessage);
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

                try {
                    JSON.parse(payload);
                } catch {
                    showError(`JSON invalido na secao ${sectionPath}.`);
                    return;
                }

                clearError();
                clearSuccess();
                saveButton.disabled = true;
                saveButton.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i>Salvando';
                try {
                    const updatedSection = await apiPost(saveUrl, {
                        sectionPath: sectionPath,
                        jsonValue: payload
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
