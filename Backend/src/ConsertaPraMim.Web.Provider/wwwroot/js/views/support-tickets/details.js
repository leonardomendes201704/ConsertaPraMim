(function () {
    const timeline = document.getElementById("supportTimeline");
    if (timeline) {
        timeline.scrollTop = timeline.scrollHeight;
    }

    const messageForm = document.getElementById("supportMessageForm");
    const sendButton = document.getElementById("supportSendMessageBtn");
    const closeButton = document.getElementById("supportCloseTicketBtn");

    if (messageForm && sendButton) {
        messageForm.addEventListener("submit", function () {
            sendButton.disabled = true;
            if (closeButton) {
                closeButton.disabled = true;
            }
            sendButton.innerHTML = '<span class="spinner-border spinner-border-sm me-2" role="status"></span>Enviando...';
        });
    }
})();