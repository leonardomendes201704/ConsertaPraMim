(function () {
    const config = window.adminChecklistTemplatesConfig || {};
    const createUrl = config.createUrl || "";
    const updateUrl = config.updateUrl || "";
    const updateStatusUrl = config.updateStatusUrl || "";

    if (!createUrl || !updateUrl || !updateStatusUrl) {
        return;
    }

    const feedback = document.getElementById("checklist-feedback");
    const templateModalElement = document.getElementById("templateModal");
    const statusModalElement = document.getElementById("statusTemplateModal");
    const templateModal = templateModalElement ? new bootstrap.Modal(templateModalElement) : null;
    const statusModal = statusModalElement ? new bootstrap.Modal(statusModalElement) : null;

    const modalTitle = document.getElementById("template-modal-title");
    const templateIdInput = document.getElementById("template-id");
    const categoryInput = document.getElementById("template-category");
    const nameInput = document.getElementById("template-name");
    const descriptionInput = document.getElementById("template-description");
    const itemsContainer = document.getElementById("template-items");
    const addItemButton = document.getElementById("add-item-btn");
    const saveTemplateButton = document.getElementById("save-template-btn");
    const openCreateButton = document.getElementById("open-create-modal-btn");
    const confirmStatusButton = document.getElementById("confirm-status-btn");
    const statusMessage = document.getElementById("status-message");
    const statusReason = document.getElementById("status-reason");

    if (!feedback || !templateModal || !statusModal || !modalTitle || !templateIdInput || !categoryInput || !nameInput || !descriptionInput || !itemsContainer || !addItemButton || !saveTemplateButton || !openCreateButton || !confirmStatusButton || !statusMessage || !statusReason) {
        return;
    }

    let pendingStatus = null;

    function showFeedback(type, message) {
        feedback.className = `alert alert-${type} mb-3`;
        feedback.textContent = message;
        feedback.classList.remove("d-none");
    }

    function clearFeedback() {
        feedback.className = "d-none alert mb-3";
        feedback.textContent = "";
    }

    function createItemRow(item) {
        const wrapper = document.createElement("div");
        wrapper.className = "checklist-item-row";
        wrapper.innerHTML = `
            <input type="hidden" class="js-item-id" value="${item.id || ""}" />
            <div class="row g-2 align-items-end">
                <div class="col-lg-1">
                    <label class="form-label small fw-semibold mb-1">Ordem</label>
                    <input type="number" class="form-control form-control-sm js-item-sort" value="${Number(item.sortOrder || 10)}" min="0" max="10000">
                </div>
                <div class="col-lg-4">
                    <label class="form-label small fw-semibold mb-1">Titulo</label>
                    <input type="text" class="form-control form-control-sm js-item-title" maxlength="180" value="${item.title || ""}">
                </div>
                <div class="col-lg-4">
                    <label class="form-label small fw-semibold mb-1">Texto de apoio</label>
                    <input type="text" class="form-control form-control-sm js-item-help" maxlength="500" value="${item.helpText || ""}">
                </div>
                <div class="col-lg-3 text-end">
                    <button type="button" class="btn btn-sm btn-outline-danger js-remove-item"><i class="fas fa-trash"></i> Remover</button>
                </div>
                <div class="col-lg-12">
                    <div class="d-flex flex-wrap gap-3 pt-1">
                        <div class="form-check">
                            <input class="form-check-input js-item-required" type="checkbox" ${item.isRequired ? "checked" : ""}>
                            <label class="form-check-label small">Obrigatorio</label>
                        </div>
                        <div class="form-check">
                            <input class="form-check-input js-item-evidence" type="checkbox" ${item.requiresEvidence ? "checked" : ""}>
                            <label class="form-check-label small">Exigir evidencia</label>
                        </div>
                        <div class="form-check">
                            <input class="form-check-input js-item-note" type="checkbox" ${item.allowNote ? "checked" : ""}>
                            <label class="form-check-label small">Permitir observacao</label>
                        </div>
                        <div class="form-check">
                            <input class="form-check-input js-item-active" type="checkbox" ${item.isActive !== false ? "checked" : ""}>
                            <label class="form-check-label small">Ativo</label>
                        </div>
                    </div>
                </div>
            </div>`;

        wrapper.querySelector(".js-remove-item")?.addEventListener("click", function () {
            wrapper.remove();
        });
        return wrapper;
    }

    function resetTemplateModal() {
        templateIdInput.value = "";
        categoryInput.value = "";
        categoryInput.disabled = false;
        nameInput.value = "";
        descriptionInput.value = "";
        itemsContainer.innerHTML = "";
        itemsContainer.appendChild(createItemRow({
            id: null,
            title: "",
            helpText: "",
            sortOrder: 10,
            isRequired: true,
            requiresEvidence: false,
            allowNote: true,
            isActive: true
        }));
    }

    function collectItems() {
        return Array.from(itemsContainer.querySelectorAll(".checklist-item-row")).map((row, index) => ({
            id: row.querySelector(".js-item-id")?.value || null,
            title: row.querySelector(".js-item-title")?.value || "",
            helpText: row.querySelector(".js-item-help")?.value || null,
            isRequired: row.querySelector(".js-item-required")?.checked === true,
            requiresEvidence: row.querySelector(".js-item-evidence")?.checked === true,
            allowNote: row.querySelector(".js-item-note")?.checked === true,
            isActive: row.querySelector(".js-item-active")?.checked === true,
            sortOrder: Number(row.querySelector(".js-item-sort")?.value || ((index + 1) * 10))
        }));
    }

    async function postJson(url, payload) {
        const response = await fetch(url, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "X-Requested-With": "XMLHttpRequest"
            },
            body: JSON.stringify(payload)
        });

        const body = await response.json().catch(() => null);
        if (!response.ok || !body?.success) {
            throw new Error(body?.errorMessage || `Falha na operacao (${response.status}).`);
        }

        return body;
    }

    openCreateButton.addEventListener("click", function () {
        clearFeedback();
        modalTitle.textContent = "Novo template de checklist";
        resetTemplateModal();
        templateModal.show();
    });

    addItemButton.addEventListener("click", function () {
        const nextSort = (itemsContainer.querySelectorAll(".checklist-item-row").length + 1) * 10;
        itemsContainer.appendChild(createItemRow({
            id: null,
            title: "",
            helpText: "",
            sortOrder: nextSort,
            isRequired: true,
            requiresEvidence: false,
            allowNote: true,
            isActive: true
        }));
    });

    document.addEventListener("click", function (event) {
        const editButton = event.target.closest(".js-edit-btn");
        if (editButton) {
            clearFeedback();
            const scriptId = editButton.getAttribute("data-template-script-id");
            if (!scriptId) return;

            const payloadRaw = document.getElementById(scriptId)?.textContent || "";
            if (!payloadRaw) return;

            const template = JSON.parse(payloadRaw);
            modalTitle.textContent = "Editar template de checklist";
            templateIdInput.value = template.id;
            categoryInput.value = template.categoryDefinitionId;
            categoryInput.disabled = true;
            nameInput.value = template.name || "";
            descriptionInput.value = template.description || "";

            itemsContainer.innerHTML = "";
            const items = Array.isArray(template.items) ? template.items : [];
            if (items.length === 0) {
                itemsContainer.appendChild(createItemRow({
                    id: null,
                    title: "",
                    helpText: "",
                    sortOrder: 10,
                    isRequired: true,
                    requiresEvidence: false,
                    allowNote: true,
                    isActive: true
                }));
            } else {
                items
                    .sort((a, b) => Number(a.sortOrder || 0) - Number(b.sortOrder || 0))
                    .forEach(item => itemsContainer.appendChild(createItemRow(item)));
            }

            templateModal.show();
            return;
        }

        const statusButton = event.target.closest(".js-toggle-status-btn");
        if (!statusButton) return;

        clearFeedback();
        const row = statusButton.closest("tr");
        const isActive = String(row?.getAttribute("data-template-active") || "false").toLowerCase() === "true";
        const targetIsActive = !isActive;
        const templateName = row?.getAttribute("data-template-name") || "Template";
        const templateId = row?.getAttribute("data-template-id") || "";
        if (!templateId) return;

        pendingStatus = {
            templateId: templateId,
            isActive: targetIsActive
        };

        const action = targetIsActive ? "ativar" : "inativar";
        statusMessage.textContent = `Confirma ${action} o template "${templateName}"?`;
        statusReason.value = "";
        statusModal.show();
    });

    saveTemplateButton.addEventListener("click", async function () {
        saveTemplateButton.disabled = true;
        clearFeedback();

        try {
            const payload = {
                templateId: templateIdInput.value || null,
                categoryDefinitionId: categoryInput.value,
                name: nameInput.value,
                description: descriptionInput.value,
                items: collectItems()
            };

            if (!payload.categoryDefinitionId) {
                throw new Error("Selecione a categoria do template.");
            }

            if (!payload.name || !payload.name.trim()) {
                throw new Error("Informe o nome do template.");
            }

            if (!Array.isArray(payload.items) || payload.items.length === 0) {
                throw new Error("Informe ao menos um item para o checklist.");
            }

            const url = payload.templateId ? updateUrl : createUrl;
            await postJson(url, payload);
            window.location.reload();
        } catch (error) {
            showFeedback("danger", error.message || "Falha ao salvar template.");
        } finally {
            saveTemplateButton.disabled = false;
        }
    });

    confirmStatusButton.addEventListener("click", async function () {
        if (!pendingStatus) {
            return;
        }

        confirmStatusButton.disabled = true;
        clearFeedback();
        try {
            await postJson(updateStatusUrl, {
                templateId: pendingStatus.templateId,
                isActive: pendingStatus.isActive,
                reason: statusReason.value
            });
            window.location.reload();
        } catch (error) {
            showFeedback("danger", error.message || "Falha ao alterar status do template.");
        } finally {
            confirmStatusButton.disabled = false;
            pendingStatus = null;
        }
    });
})();
