(function () {
    const form = document.getElementById("supportCreateForm");
    const submit = document.getElementById("createTicketSubmit");
    if (!form || !submit) {
        return;
    }

    form.addEventListener("submit", function () {
        submit.disabled = true;
        submit.innerHTML = '<span class="spinner-border spinner-border-sm me-2" role="status"></span>Abrindo...';
    });
})();