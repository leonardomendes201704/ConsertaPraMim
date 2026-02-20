(function () {
    const timeline = document.getElementById("supportTimeline");
    if (timeline) {
        timeline.scrollTop = timeline.scrollHeight;
    }

    function bindLoadingState(formId, buttonId, loadingText) {
        const form = document.getElementById(formId);
        const button = document.getElementById(buttonId);
        if (!form || !button) {
            return;
        }

        form.addEventListener("submit", function () {
            button.disabled = true;
            button.innerHTML = `<span class="spinner-border spinner-border-sm me-1" role="status"></span>${loadingText}`;
        });
    }

    bindLoadingState("adminSupportMessageForm", "adminSupportSendMessageBtn", "Enviando...");
    bindLoadingState("adminSupportStatusForm", "adminSupportStatusBtn", "Salvando...");
    bindLoadingState("adminSupportAssignForm", "adminSupportAssignBtn", "Salvando...");
})();
