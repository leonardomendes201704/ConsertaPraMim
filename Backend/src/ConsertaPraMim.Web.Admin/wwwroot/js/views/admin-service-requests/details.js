(function () {
    const config = window.adminServiceRequestDetailsConfig || {};
    const requestId = config.requestId || "";
    const updateStatusUrl = config.updateStatusUrl || "";
    const sendNotificationUrl = config.sendNotificationUrl || "";

    const feedback = document.getElementById("request-feedback");
    const evidencePhaseFilter = document.getElementById("evidence-phase-filter");
    const evidenceProviderFilter = document.getElementById("evidence-provider-filter");
    const evidenceVisibleCount = document.getElementById("evidence-visible-count");
    const evidenceEmptyFilter = document.getElementById("evidence-empty-filter");
    const evidenceItems = Array.from(document.querySelectorAll(".evidence-item"));
    const updateStatusButton = document.getElementById("update-status-btn");
    const sendNotificationButton = document.getElementById("send-notification-btn");

    if (!feedback || !requestId || !updateStatusUrl || !sendNotificationUrl || !updateStatusButton || !sendNotificationButton) {
        return;
    }

    function showFeedback(type, message) {
        feedback.className = `alert alert-${type} mb-3`;
        feedback.textContent = message;
        feedback.classList.remove("d-none");
    }

    function hideFeedback() {
        feedback.classList.add("d-none");
        feedback.textContent = "";
    }

    async function updateStatus() {
        const status = document.getElementById("request-status-select").value;
        const reason = document.getElementById("status-reason").value?.trim() || null;

        if (!confirm(`Confirma alterar o status do pedido para "${status}"?`)) {
            return;
        }

        hideFeedback();
        try {
            const response = await fetch(updateStatusUrl, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "X-Requested-With": "XMLHttpRequest"
                },
                body: JSON.stringify({ requestId: requestId, status: status, reason: reason })
            });

            const payload = await response.json().catch(() => null);
            if (!response.ok || !payload?.success) {
                showFeedback("danger", payload?.errorMessage || `Falha ao atualizar status (${response.status}).`);
                return;
            }

            const statusBadge = document.getElementById("request-status-badge");
            if (statusBadge) {
                statusBadge.textContent = status;
            }
            showFeedback("success", payload?.message || "Status atualizado com sucesso.");
        } catch (error) {
            console.error(error);
            showFeedback("danger", "Erro inesperado ao atualizar status.");
        }
    }

    function parseRecipient(value) {
        if (!value) {
            return {};
        }

        if (value.startsWith("id:")) {
            return { recipientUserId: value.substring(3), recipientEmail: null };
        }

        if (value.startsWith("email:")) {
            return { recipientUserId: null, recipientEmail: value.substring(6) };
        }

        return {};
    }

    async function sendNotification() {
        const targetValue = document.getElementById("notification-target").value;
        const subject = document.getElementById("notification-subject").value?.trim();
        const message = document.getElementById("notification-message").value?.trim();
        const reason = document.getElementById("notification-reason").value?.trim() || null;
        const recipient = parseRecipient(targetValue);

        if (!subject || !message) {
            showFeedback("danger", "Assunto e mensagem sao obrigatorios.");
            return;
        }

        if (!confirm("Confirma o envio da notificacao manual?")) {
            return;
        }

        hideFeedback();
        try {
            const response = await fetch(sendNotificationUrl, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "X-Requested-With": "XMLHttpRequest"
                },
                body: JSON.stringify({
                    recipientUserId: recipient.recipientUserId || null,
                    recipientEmail: recipient.recipientEmail || null,
                    subject: subject,
                    message: message,
                    reason: reason,
                    actionUrl: `/ServiceRequests/Details/${requestId}`
                })
            });

            const payload = await response.json().catch(() => null);
            if (!response.ok || !payload?.success) {
                showFeedback("danger", payload?.errorMessage || `Falha ao enviar notificacao (${response.status}).`);
                return;
            }

            showFeedback("success", payload?.message || "Notificacao enviada com sucesso.");
        } catch (error) {
            console.error(error);
            showFeedback("danger", "Erro inesperado ao enviar notificacao.");
        }
    }

    function applyEvidenceFilters() {
        if (!evidenceItems.length) {
            return;
        }

        const selectedPhase = (evidencePhaseFilter?.value || "all").toLowerCase();
        const providerTerm = (evidenceProviderFilter?.value || "").trim().toLowerCase();
        let visibleCount = 0;

        evidenceItems.forEach((item) => {
            const itemPhase = (item.dataset.evidencePhase || "").toLowerCase();
            const itemProvider = (item.dataset.evidenceProvider || "").toLowerCase();
            const matchesPhase = selectedPhase === "all" || itemPhase === selectedPhase;
            const matchesProvider = !providerTerm || itemProvider.includes(providerTerm);
            const shouldShow = matchesPhase && matchesProvider;

            item.classList.toggle("d-none", !shouldShow);
            if (shouldShow) {
                visibleCount += 1;
            }
        });

        if (evidenceVisibleCount) {
            evidenceVisibleCount.textContent = visibleCount.toString();
        }

        if (evidenceEmptyFilter) {
            evidenceEmptyFilter.classList.toggle("d-none", visibleCount !== 0);
        }
    }

    updateStatusButton.addEventListener("click", updateStatus);
    sendNotificationButton.addEventListener("click", sendNotification);
    evidencePhaseFilter?.addEventListener("change", applyEvidenceFilters);
    evidenceProviderFilter?.addEventListener("input", applyEvidenceFilters);
    applyEvidenceFilters();
})();
