(function () {
    const config = window.adminChatDetailsConfig || {};
    const sendNotificationUrl = config.sendNotificationUrl || "";
    const requestId = config.requestId || "";

    const feedback = document.getElementById("chat-feedback");
    const targetSelect = document.getElementById("notify-target");
    const emailWrapper = document.getElementById("notify-email-wrapper");
    const emailInput = document.getElementById("notify-email");
    const sendButton = document.getElementById("notify-send-btn");

    if (!feedback || !targetSelect || !emailWrapper || !emailInput || !sendButton || !sendNotificationUrl || !requestId) {
        return;
    }

    function showFeedback(type, message) {
        feedback.className = `alert alert-${type} mb-3`;
        feedback.textContent = message;
        feedback.classList.remove("d-none");
    }

    function clearFeedback() {
        feedback.classList.add("d-none");
        feedback.textContent = "";
    }

    function updateTargetMode() {
        const value = targetSelect.value || "";
        if (value.startsWith("email:")) {
            emailWrapper.classList.remove("d-none");
        } else {
            emailWrapper.classList.add("d-none");
            emailInput.value = "";
        }
    }

    function parseTargetValue() {
        const value = targetSelect.value || "";
        if (value.startsWith("id:")) {
            return { recipientUserId: value.substring(3), recipientEmail: null };
        }

        if (value.startsWith("email:")) {
            const email = emailInput.value?.trim();
            return { recipientUserId: null, recipientEmail: email || null };
        }

        return { recipientUserId: null, recipientEmail: null };
    }

    async function sendNotification() {
        const subject = document.getElementById("notify-subject").value?.trim();
        const message = document.getElementById("notify-message").value?.trim();
        const target = parseTargetValue();

        if (!subject || !message) {
            showFeedback("danger", "Assunto e mensagem sao obrigatorios.");
            return;
        }

        if (!target.recipientUserId && !target.recipientEmail) {
            showFeedback("danger", "Informe um destinatario valido.");
            return;
        }

        if (!confirm("Confirma envio da notificacao manual?")) {
            return;
        }

        clearFeedback();
        try {
            const response = await fetch(sendNotificationUrl, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "X-Requested-With": "XMLHttpRequest"
                },
                body: JSON.stringify({
                    recipientUserId: target.recipientUserId || null,
                    recipientEmail: target.recipientEmail || null,
                    subject: subject,
                    message: message,
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

    targetSelect.addEventListener("change", updateTargetMode);
    sendButton.addEventListener("click", sendNotification);
    updateTargetMode();
})();
