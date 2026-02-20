(function () {
    const form = document.getElementById("supportFiltersForm");
    const submit = document.getElementById("supportFilterSubmit");
    if (!form || !submit) {
        return;
    }

    form.addEventListener("submit", function () {
        submit.disabled = true;
        submit.innerHTML = '<span class="spinner-border spinner-border-sm me-2" role="status"></span>Carregando...';
    });
})();