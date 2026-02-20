(function () {
    const config = window.cpmAdminServiceCategories || {};
    const createUrl = config.createUrl || "";
    const updateUrl = config.updateUrl || "";
    const updateStatusUrl = config.updateStatusUrl || "";

    const feedback = document.getElementById("categories-feedback");
    const createModalElement = document.getElementById("createCategoryModal");
    const editModalElement = document.getElementById("editCategoryModal");
    const statusModalElement = document.getElementById("statusCategoryModal");
    const openCreateButton = document.getElementById("open-create-modal-btn");
    const createButton = document.getElementById("create-category-btn");
    const saveButton = document.getElementById("save-category-btn");
    const confirmStatusButton = document.getElementById("confirm-status-btn");

    if (!createUrl || !updateUrl || !updateStatusUrl || !feedback || !createModalElement || !editModalElement || !statusModalElement || !window.bootstrap) {
        return;
    }

    const createModal = new bootstrap.Modal(createModalElement);
    const editModal = new bootstrap.Modal(editModalElement);
    const statusModal = new bootstrap.Modal(statusModalElement);
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

    async function postJson(url, payload) {
        const response = await fetch(url, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "X-Requested-With": "XMLHttpRequest"
            },
            body: JSON.stringify(payload)
        });

        const body = await response.json().catch(function () { return null; });
        if (!response.ok || !body?.success) {
            throw new Error(body?.errorMessage || `Falha na operacao (${response.status}).`);
        }

        return body;
    }

    openCreateButton?.addEventListener("click", function () {
        clearFeedback();
        document.getElementById("create-name").value = "";
        document.getElementById("create-slug").value = "";
        document.getElementById("create-icon").value = "build_circle";
        document.getElementById("create-legacy").value = "Electrical";
        createModal.show();
    });

    createButton?.addEventListener("click", async function () {
        createButton.disabled = true;
        clearFeedback();
        try {
            await postJson(createUrl, {
                name: document.getElementById("create-name").value,
                slug: document.getElementById("create-slug").value,
                legacyCategory: document.getElementById("create-legacy").value,
                icon: document.getElementById("create-icon").value
            });
            window.location.reload();
        } catch (error) {
            showFeedback("danger", error.message || "Falha ao criar categoria.");
        } finally {
            createButton.disabled = false;
        }
    });

    document.addEventListener("click", function (event) {
        const editButton = event.target.closest(".js-edit-btn");
        if (editButton) {
            clearFeedback();
            const row = editButton.closest("tr");
            document.getElementById("edit-id").value = row.dataset.categoryId;
            document.getElementById("edit-name").value = row.dataset.categoryName || "";
            document.getElementById("edit-slug").value = row.dataset.categorySlug || "";
            document.getElementById("edit-icon").value = row.dataset.categoryIcon || "build_circle";
            document.getElementById("edit-legacy").value = row.dataset.categoryLegacy || "Electrical";
            editModal.show();
            return;
        }

        const statusButton = event.target.closest(".js-toggle-status-btn");
        if (!statusButton) {
            return;
        }

        clearFeedback();
        const row = statusButton.closest("tr");
        const isActive = String(row.dataset.categoryActive || "false").toLowerCase() === "true";
        const targetIsActive = !isActive;
        const categoryName = row.dataset.categoryName || "Categoria";

        pendingStatus = {
            categoryId: row.dataset.categoryId,
            isActive: targetIsActive
        };

        const action = targetIsActive ? "ativar" : "inativar";
        document.getElementById("status-message").textContent = `Confirma ${action} a categoria "${categoryName}"?`;
        document.getElementById("status-reason").value = "";
        statusModal.show();
    });

    saveButton?.addEventListener("click", async function () {
        saveButton.disabled = true;
        clearFeedback();
        try {
            await postJson(updateUrl, {
                categoryId: document.getElementById("edit-id").value,
                name: document.getElementById("edit-name").value,
                slug: document.getElementById("edit-slug").value,
                legacyCategory: document.getElementById("edit-legacy").value,
                icon: document.getElementById("edit-icon").value
            });
            window.location.reload();
        } catch (error) {
            showFeedback("danger", error.message || "Falha ao atualizar categoria.");
        } finally {
            saveButton.disabled = false;
        }
    });

    confirmStatusButton?.addEventListener("click", async function () {
        if (!pendingStatus) {
            return;
        }

        confirmStatusButton.disabled = true;
        clearFeedback();
        try {
            await postJson(updateStatusUrl, {
                categoryId: pendingStatus.categoryId,
                isActive: pendingStatus.isActive,
                reason: document.getElementById("status-reason").value
            });
            window.location.reload();
        } catch (error) {
            showFeedback("danger", error.message || "Falha ao alterar status da categoria.");
        } finally {
            confirmStatusButton.disabled = false;
            pendingStatus = null;
        }
    });
})();
