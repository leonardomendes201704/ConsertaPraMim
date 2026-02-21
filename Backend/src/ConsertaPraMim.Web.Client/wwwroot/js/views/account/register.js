(function () {
    var phoneInput = document.getElementById("registerPhone");
    if (!phoneInput) {
        return;
    }

    function formatPhone(value) {
        var digits = String(value || "").replace(/\D/g, "").slice(0, 11);
        if (digits.length <= 2) {
            return digits;
        }

        if (digits.length <= 6) {
            return "(" + digits.slice(0, 2) + ") " + digits.slice(2);
        }

        if (digits.length <= 10) {
            return "(" + digits.slice(0, 2) + ") " + digits.slice(2, 6) + "-" + digits.slice(6);
        }

        return "(" + digits.slice(0, 2) + ") " + digits.slice(2, 7) + "-" + digits.slice(7);
    }

    phoneInput.addEventListener("input", function () {
        phoneInput.value = formatPhone(phoneInput.value);
    });

    var form = phoneInput.closest("form");
    if (form) {
        form.addEventListener("submit", function () {
            phoneInput.value = phoneInput.value.replace(/\D/g, "");
        });
    }

    phoneInput.value = formatPhone(phoneInput.value);
})();
