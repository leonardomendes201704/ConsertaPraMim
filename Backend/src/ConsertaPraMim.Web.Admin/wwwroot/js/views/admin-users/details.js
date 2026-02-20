(function () {
    const config = window.cpmAdminUsersDetails || {};
    const sendNotificationUrl = config.sendNotificationUrl || "";
    const userId = config.userId || "";
    const feedback = document.getElementById("user-notification-feedback");
    const sendButton = document.getElementById("user-notify-send-btn");

    if (!sendNotificationUrl || !userId || !feedback || !sendButton) {
        return;
    }

    function showFeedback(type, message) {
        feedback.className = `alert alert-${type} mb-2`;
        feedback.textContent = message;
        feedback.classList.remove("d-none");
    }

    function clearFeedback() {
        feedback.classList.add("d-none");
        feedback.textContent = "";
    }

    async function sendNotification() {
        const subject = document.getElementById("user-notify-subject").value?.trim();
        const message = document.getElementById("user-notify-message").value?.trim();
        const reason = document.getElementById("user-notify-reason").value?.trim() || null;

        if (!subject || !message) {
            showFeedback("danger", "Assunto e mensagem sao obrigatorios.");
            return;
        }

        if (!confirm("Confirma envio da notificacao manual para este usuario?")) {
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
                    recipientUserId: userId,
                    recipientEmail: null,
                    subject,
                    message,
                    reason
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

    sendButton.addEventListener("click", sendNotification);
})();
