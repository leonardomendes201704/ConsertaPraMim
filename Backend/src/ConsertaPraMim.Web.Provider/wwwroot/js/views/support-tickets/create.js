(function () {
    const form = document.getElementById("supportCreateForm");
    const submit = document.getElementById("createTicketSubmit");
    const attachmentInput = document.getElementById("supportTicketAttachments");
    const attachmentHint = document.getElementById("supportAttachmentHint");

    if (!form || !submit) {
        return;
    }

    if (attachmentInput && attachmentHint) {
        attachmentInput.addEventListener("change", function () {
            const files = attachmentInput.files ? attachmentInput.files.length : 0;
            attachmentHint.textContent = files > 0
                ? `${files} anexo(s) selecionado(s). Revise antes de abrir o chamado.`
                : "Opcional. Envie fotos, videos ou documentos de apoio (maximo de 10 anexos, 25MB por arquivo).";
        });
    }

    form.addEventListener("submit", function () {
        submit.disabled = true;
        submit.innerHTML = '<span class="spinner-border spinner-border-sm me-2" role="status"></span>Abrindo chamado...';
    });
})();
