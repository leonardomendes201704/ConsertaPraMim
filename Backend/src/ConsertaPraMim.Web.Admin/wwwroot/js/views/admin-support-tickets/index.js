(function () {
    const filterForm = document.getElementById("adminSupportFilterForm");
    const submitButton = document.getElementById("adminSupportFilterSubmit");

    if (!filterForm || !submitButton) {
        return;
    }

    const pageInput = filterForm.querySelector('input[name="page"]');
    const controlsThatResetPage = filterForm.querySelectorAll("select, input[type='text'], input[type='number']");

    controlsThatResetPage.forEach(function (control) {
        control.addEventListener("change", function () {
            if (pageInput) {
                pageInput.value = "1";
            }
        });
    });

    filterForm.addEventListener("submit", function () {
        submitButton.disabled = true;
        submitButton.innerHTML = '<span class="spinner-border spinner-border-sm me-1" role="status"></span>Aplicando...';
    });
})();
