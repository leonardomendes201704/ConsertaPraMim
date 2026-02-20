(function () {
    const config = window.cpmAdminUsersIndex || {};
    const updateStatusUrl = config.updateStatusUrl || "";

    const feedbackBox = document.getElementById("users-feedback");
    const modalElement = document.getElementById("statusConfirmModal");
    const confirmMessage = document.getElementById("status-confirm-message");
    const reasonInput = document.getElementById("status-change-reason");
    const confirmButton = document.getElementById("confirm-status-change-btn");

    if (!updateStatusUrl || !feedbackBox || !modalElement || !confirmMessage || !reasonInput || !confirmButton || !window.bootstrap) {
        return;
    }

    const modal = new bootstrap.Modal(modalElement);
    let pendingAction = null;

    function showFeedback(type, message) {
        feedbackBox.className = `mb-3 alert alert-${type}`;
        feedbackBox.textContent = message;
        feedbackBox.classList.remove("d-none");
    }

    function clearFeedback() {
        feedbackBox.classList.add("d-none");
        feedbackBox.textContent = "";
    }

    function parseBoolean(value) {
        return String(value).toLowerCase() === "true";
    }

    function getStatusBadgeClass(isActive) {
        return isActive
            ? "badge status-badge bg-success-subtle text-success-emphasis js-user-status-badge"
            : "badge status-badge bg-danger-subtle text-danger-emphasis js-user-status-badge";
    }

    function getActionButtonClass(isActive) {
        return isActive
            ? "btn btn-outline-danger btn-sm js-toggle-status-btn"
            : "btn btn-outline-success btn-sm js-toggle-status-btn";
    }

    function getActionButtonLabel(isActive) {
        return isActive
            ? "<i class=\"fas fa-user-slash\"></i> Desativar"
            : "<i class=\"fas fa-user-check\"></i> Ativar";
    }

    function updateRow(userId, isActive) {
        const row = document.getElementById(`user-row-${userId.replaceAll("-", "").toLowerCase()}`);
        if (!row) {
            return;
        }

        row.dataset.isActive = String(isActive);

        const statusBadge = row.querySelector(".js-user-status-badge");
        if (statusBadge) {
            statusBadge.className = getStatusBadgeClass(isActive);
            statusBadge.textContent = isActive ? "Ativo" : "Inativo";
        }

        const actionButton = row.querySelector(".js-toggle-status-btn");
        if (actionButton) {
            actionButton.className = getActionButtonClass(isActive);
            actionButton.dataset.targetActive = String(!isActive).toLowerCase();
            actionButton.innerHTML = getActionButtonLabel(isActive);
        }
    }

    async function executeStatusChange(action) {
        confirmButton.disabled = true;
        clearFeedback();

        try {
            const response = await fetch(updateStatusUrl, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "X-Requested-With": "XMLHttpRequest"
                },
                body: JSON.stringify({
                    userId: action.userId,
                    isActive: action.targetActive,
                    reason: reasonInput.value?.trim() || null
                })
            });

            const payload = await response.json().catch(() => null);
            if (!response.ok || !payload?.success) {
                const fallback = `Falha ao atualizar status (${response.status}).`;
                const message = payload?.errorMessage || fallback;
                showFeedback("danger", message);
                return;
            }

            updateRow(action.userId, action.targetActive);
            modal.hide();
            reasonInput.value = "";
            showFeedback("success", payload?.message || "Status atualizado com sucesso.");
        } catch (error) {
            console.error(error);
            showFeedback("danger", "Nao foi possivel atualizar o status do usuario.");
        } finally {
            confirmButton.disabled = false;
            pendingAction = null;
        }
    }

    document.addEventListener("click", function (event) {
        const button = event.target.closest(".js-toggle-status-btn");
        if (!button) {
            return;
        }

        const userId = button.dataset.userId;
        const userName = button.dataset.userName || "Usuario";
        const targetActive = parseBoolean(button.dataset.targetActive);
        if (!userId) {
            return;
        }

        pendingAction = {
            userId,
            targetActive
        };

        const actionText = targetActive ? "ativar" : "desativar";
        confirmMessage.textContent = `Confirma ${actionText} o usuario "${userName}"?`;
        reasonInput.value = "";
        modal.show();
    });

    confirmButton.addEventListener("click", function () {
        if (!pendingAction) {
            return;
        }
        executeStatusChange(pendingAction);
    });
})();
