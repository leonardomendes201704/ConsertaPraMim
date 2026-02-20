$(document).ready(function () {
    const config = window.serviceRequestCreateConfig || {};
    const resolveZipUrl = config.resolveZipUrl || "";
    if (!resolveZipUrl) return;

    let currentStep = 1;
    const $zipInput = $("#zip-input");
    const $zipStatus = $("#zip-status");
    const $streetHidden = $("#street-hidden");
    const $cityHidden = $("#city-hidden");
    const $streetDisplay = $("#street-display");
    const $cityDisplay = $("#city-display");

    if ($streetHidden.val()) {
        $streetDisplay.val($streetHidden.val());
    }

    if ($cityHidden.val()) {
        $cityDisplay.val($cityHidden.val());
    }

    function onlyDigits(value) {
        return (value || "").replace(/\D/g, "");
    }

    function formatZip(value) {
        const digits = onlyDigits(value).slice(0, 8);
        if (digits.length <= 5) return digits;
        return `${digits.slice(0, 5)}-${digits.slice(5)}`;
    }

    function setZipStatus(message, isError) {
        $zipStatus
            .text(message)
            .toggleClass("text-danger", !!isError)
            .toggleClass("text-muted", !isError);
    }

    function clearResolvedAddress() {
        $streetHidden.val("");
        $cityHidden.val("");
        $streetDisplay.val("");
        $cityDisplay.val("");
    }

    async function resolveZip() {
        const digits = onlyDigits($zipInput.val());
        if (digits.length !== 8) {
            clearResolvedAddress();
            setZipStatus("Informe um CEP valido com 8 digitos.", true);
            return false;
        }

        setZipStatus("Buscando endereco...", false);

        try {
            const response = await fetch(`${resolveZipUrl}?zipCode=${digits}`, {
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });

            if (!response.ok) {
                clearResolvedAddress();
                setZipStatus("Nao foi possivel localizar esse CEP.", true);
                return false;
            }

            const data = await response.json();
            const street = data.street && data.street.trim().length > 0 ? data.street : "Endereco nao informado";
            const city = data.city && data.city.trim().length > 0 ? data.city : "Cidade nao informada";

            $zipInput.val(formatZip(data.zipCode || digits));
            $streetHidden.val(street);
            $cityHidden.val(city);
            $streetDisplay.val(street);
            $cityDisplay.val(city);
            setZipStatus("Endereco preenchido automaticamente.", false);
            return true;
        } catch {
            clearResolvedAddress();
            setZipStatus("Erro ao consultar CEP. Tente novamente.", true);
            return false;
        }
    }

    $zipInput.on("input", function () {
        const formatted = formatZip($(this).val());
        $(this).val(formatted);

        if (onlyDigits(formatted).length < 8) {
            clearResolvedAddress();
            setZipStatus("Informe o CEP para preencher o endereco automaticamente.", false);
        }
    });

    $zipInput.on("blur", async function () {
        if (onlyDigits($(this).val()).length === 8) {
            await resolveZip();
        }
    });

    if (onlyDigits($zipInput.val()).length === 8 && (!$streetHidden.val() || !$cityHidden.val())) {
        resolveZip();
    }

    $(".next-step").click(async function () {
        if (currentStep === 2) {
            const ok = await resolveZip();
            if (!ok) {
                return;
            }
        }

        if (currentStep < 3) {
            $(`#step-${currentStep}`).addClass("d-none");
            currentStep++;
            $(`#step-${currentStep}`).removeClass("d-none");
            updateProgress();
            updateReview();
        }
    });

    $(".prev-step").click(function () {
        if (currentStep > 1) {
            $(`#step-${currentStep}`).addClass("d-none");
            currentStep--;
            $(`#step-${currentStep}`).removeClass("d-none");
            updateProgress();
        }
    });

    function updateProgress() {
        const pct = (currentStep / 3) * 100;
        $("#creation-progress").css("width", `${pct}%`).attr("aria-valuenow", pct);

        $(".step-label").removeClass("active fw-bold text-primary");
        $(`#label-step-${currentStep}`).addClass("active fw-bold text-primary");
    }

    function updateReview() {
        $("#review-desc").text($("textarea[name='Description']").val());
        const zip = $zipInput.val();
        const street = $streetHidden.val() || "Endereco nao informado";
        const city = $cityHidden.val() || "Cidade nao informada";
        $("#review-address").text(`${street}, ${city} - CEP ${zip}`);
    }
});
