(function () {
    const config = window.cpmAdminUsersIndex || {};
    initStatusUpdateFlow(config.updateStatusUrl || "");
    initCreateAdminFlow(config.createAdminUrl || "");

    function initStatusUpdateFlow(updateStatusUrl) {
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
    }

    function initCreateAdminFlow(createAdminUrl) {
        const createModalElement = document.getElementById("createAdminUserModal");
        const createForm = document.getElementById("create-admin-user-form");
        const submitButton = document.getElementById("create-admin-user-submit-btn");
        const formFeedback = document.getElementById("create-admin-feedback");
        const overlay = document.getElementById("createAdminOverlay");
        const overlayIcon = document.getElementById("createAdminOverlayIcon");
        const overlayTitle = document.getElementById("createAdminOverlayTitle");
        const overlayMessage = document.getElementById("createAdminOverlayMessage");
        const overlayCloseButton = document.getElementById("createAdminOverlayCloseBtn");

        if (!createAdminUrl ||
            !createModalElement ||
            !createForm ||
            !submitButton ||
            !formFeedback ||
            !overlay ||
            !overlayIcon ||
            !overlayTitle ||
            !overlayMessage ||
            !overlayCloseButton ||
            !window.bootstrap) {
            return;
        }

        function showFormFeedback(type, message) {
            formFeedback.className = `alert alert-${type} mb-3`;
            formFeedback.textContent = message;
            formFeedback.classList.remove("d-none");
        }

        function clearFormFeedback() {
            formFeedback.className = "d-none alert mb-3";
            formFeedback.textContent = "";
        }

        function getOverlayIconHtml(state) {
            if (state === "saving") {
                return "<span class=\"spinner-border text-primary\" role=\"status\" aria-hidden=\"true\"></span>";
            }

            if (state === "success") {
                return "<span class=\"text-success\"><i class=\"fas fa-circle-check fa-2x\"></i></span>";
            }

            return "<span class=\"text-danger\"><i class=\"fas fa-circle-exclamation fa-2x\"></i></span>";
        }

        function showOverlay(state, title, message, showCloseButton) {
            overlayIcon.innerHTML = getOverlayIconHtml(state);
            overlayTitle.textContent = title;
            overlayMessage.textContent = message;
            overlayCloseButton.classList.toggle("d-none", !showCloseButton);
            overlay.classList.add("is-visible");
            overlay.setAttribute("aria-hidden", "false");
        }

        function hideOverlay() {
            overlay.classList.remove("is-visible");
            overlay.setAttribute("aria-hidden", "true");
            overlayCloseButton.classList.add("d-none");
        }

        function getPayloadFromForm() {
            return {
                name: (createForm.elements.name?.value || "").trim(),
                email: (createForm.elements.email?.value || "").trim(),
                phone: (createForm.elements.phone?.value || "").trim(),
                password: createForm.elements.password?.value || "",
                confirmPassword: createForm.elements.confirmPassword?.value || ""
            };
        }

        function validatePayload(payload) {
            if (!payload.name) {
                return "Nome e obrigatorio.";
            }

            if (payload.name.length < 3 || payload.name.length > 100) {
                return "Nome deve ter entre 3 e 100 caracteres.";
            }

            if (!payload.email) {
                return "Email e obrigatorio.";
            }

            const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
            if (!emailRegex.test(payload.email)) {
                return "Email invalido.";
            }

            const normalizedPhone = payload.phone.replace(/\D/g, "");
            if (!normalizedPhone || (normalizedPhone.length !== 10 && normalizedPhone.length !== 11)) {
                return "Telefone deve conter 10 ou 11 digitos.";
            }

            if (!payload.password) {
                return "Senha e obrigatoria.";
            }

            if (payload.password.length < 8) {
                return "Senha deve ter no minimo 8 caracteres.";
            }

            if (payload.password !== payload.confirmPassword) {
                return "Confirmacao de senha nao confere.";
            }

            return null;
        }

        async function submitCreateAdmin() {
            clearFormFeedback();
            const payload = getPayloadFromForm();
            const validationMessage = validatePayload(payload);
            if (validationMessage) {
                showFormFeedback("danger", validationMessage);
                return;
            }

            submitButton.disabled = true;
            showOverlay("saving", "Salvando", "Criando novo usuario admin...", false);

            try {
                const response = await fetch(createAdminUrl, {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json",
                        "X-Requested-With": "XMLHttpRequest"
                    },
                    body: JSON.stringify({
                        name: payload.name,
                        email: payload.email,
                        phone: payload.phone,
                        password: payload.password
                    })
                });

                const responsePayload = await response.json().catch(() => null);
                if (!response.ok || !responsePayload?.success) {
                    const fallback = `Falha ao criar usuario admin (${response.status}).`;
                    const message = responsePayload?.errorMessage || fallback;
                    showOverlay("error", "Erro ao salvar", message, true);
                    return;
                }

                showOverlay("success", "Salvo com sucesso", responsePayload?.message || "Usuario admin criado com sucesso.", false);
                window.setTimeout(function () {
                    window.location.reload();
                }, 900);
            } catch (error) {
                console.error(error);
                showOverlay("error", "Erro ao salvar", "Nao foi possivel criar o usuario admin.", true);
            } finally {
                submitButton.disabled = false;
            }
        }

        overlayCloseButton.addEventListener("click", function () {
            hideOverlay();
        });

        createForm.addEventListener("submit", function (event) {
            event.preventDefault();
            submitCreateAdmin();
        });

        createModalElement.addEventListener("hidden.bs.modal", function () {
            createForm.reset();
            clearFormFeedback();
            hideOverlay();
            submitButton.disabled = false;
        });
    }
})();
