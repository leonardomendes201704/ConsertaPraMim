$(document).ready(function () {
    const config = window.serviceRequestCreateConfig || {};
    const resolveZipUrl = config.resolveZipUrl || "";
    const analyzeProblemUrl = config.analyzeProblemUrl || "";
    if (!resolveZipUrl || !analyzeProblemUrl) return;

    const maxSteps = 4;
    const minimumDescriptionLength = 15;

    let currentStep = 1;
    let analysisResult = null;
    let analysisKey = null;
    let isAnalyzing = false;

    const $wizardForm = $("#wizard-form");
    const antiforgeryToken = $wizardForm.find("input[name='__RequestVerificationToken']").val() || "";
    const $descriptionInput = $("textarea[name='Description']");
    const $categoryInputs = $("input[name='CategoryId']");
    const $zipInput = $("#zip-input");
    const $zipStatus = $("#zip-status");
    const $streetHidden = $("#street-hidden");
    const $cityHidden = $("#city-hidden");
    const $streetDisplay = $("#street-display");
    const $cityDisplay = $("#city-display");
    const $analysisLoading = $("#analysis-loading");
    const $analysisError = $("#analysis-error");
    const $analysisResultCard = $("#analysis-result");
    const $analysisCategoryLabel = $("#analysis-category-label");
    const $analysisFallbackBadge = $("#analysis-fallback-badge");
    const $analysisSummary = $("#analysis-summary");
    const $analysisHighlights = $("#analysis-highlights");
    const $analysisRetryButton = $("#analysis-retry-btn");
    const $analysisNextButton = $("#analysis-next-btn");

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

    function selectedCategoryId() {
        return $categoryInputs.filter(":checked").val() || "";
    }

    function selectedCategoryLabel() {
        const id = selectedCategoryId();
        if (!id) return "Categoria";
        const $label = $(`label[for='cat-${id}'] .small`);
        return $label.length ? $label.first().text().trim() : "Categoria";
    }

    function normalizedDescription() {
        return ($descriptionInput.val() || "").trim();
    }

    function currentAnalysisFingerprint() {
        return `${selectedCategoryId()}|${normalizedDescription()}`;
    }

    function invalidateAnalysis() {
        analysisResult = null;
        analysisKey = null;
        $analysisResultCard.addClass("d-none");
        $analysisError.addClass("d-none").text("");
        $analysisNextButton.prop("disabled", true);
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

    function updateProgress() {
        const pct = (currentStep / maxSteps) * 100;
        $("#creation-progress").css("width", `${pct}%`).attr("aria-valuenow", pct);

        $(".step-label").removeClass("active fw-bold text-primary");
        $(`#label-step-${currentStep}`).addClass("active fw-bold text-primary");
    }

    function showStep(stepNumber) {
        $(".step-content").addClass("d-none");
        $(`#step-${stepNumber}`).removeClass("d-none");
        currentStep = stepNumber;
        updateProgress();
        updateReview();
    }

    function updateReview() {
        const description = normalizedDescription();
        const zip = $zipInput.val();
        const street = $streetHidden.val() || "Endereco nao informado";
        const city = $cityHidden.val() || "Cidade nao informada";

        $("#review-desc").text(description || "---");
        $("#review-address").text(`${street}, ${city} - CEP ${zip || "---"}`);

        if (!analysisResult) {
            $("#review-analysis").text("---");
            return;
        }

        const prefix = analysisResult.usedFallback ? "Resumo inicial (fallback): " : "Resumo IA: ";
        $("#review-analysis").text(`${prefix}${analysisResult.understandingSummary || "---"}`);
    }

    function parseErrorMessage(rawText) {
        if (!rawText) return null;

        try {
            const payload = JSON.parse(rawText);
            if (payload && typeof payload.message === "string" && payload.message.trim().length > 0) {
                return payload.message.trim();
            }
        } catch {
            // ignore parse errors
        }

        return rawText;
    }

    function setAnalyzeLoading(isLoading) {
        isAnalyzing = isLoading;
        $analysisLoading.toggleClass("d-none", !isLoading);
        $analysisRetryButton.prop("disabled", isLoading);
        $analysisNextButton.prop("disabled", isLoading || !analysisResult);
    }

    function renderAnalysis(result) {
        analysisResult = result;
        analysisKey = currentAnalysisFingerprint();

        const categoryLabel = result.categoryName && result.categoryName.trim().length > 0
            ? result.categoryName.trim()
            : selectedCategoryLabel();

        $analysisCategoryLabel.text(categoryLabel);
        $analysisSummary.text(result.understandingSummary || "---");

        $analysisHighlights.empty();
        const highlights = Array.isArray(result.highlights) ? result.highlights : [];
        if (highlights.length === 0) {
            $analysisHighlights.append("<li>Sem highlights adicionais para este problema.</li>");
        } else {
            highlights.forEach((highlight) => {
                const text = `${highlight || ""}`.trim();
                if (text.length > 0) {
                    $analysisHighlights.append(`<li>${text}</li>`);
                }
            });
        }

        const usedFallback = !!result.usedFallback;
        $analysisFallbackBadge.toggleClass("d-none", !usedFallback);
        $analysisResultCard.removeClass("d-none");
        $analysisError.addClass("d-none").text("");
        $analysisNextButton.prop("disabled", false);
        updateReview();
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

    async function analyzeProblem(force) {
        if (isAnalyzing) {
            return false;
        }

        const categoryId = selectedCategoryId();
        const description = normalizedDescription();
        if (!categoryId) {
            $analysisError.removeClass("d-none").text("Selecione uma categoria para analisar o problema.");
            return false;
        }

        if (description.length < minimumDescriptionLength) {
            $analysisError
                .removeClass("d-none")
                .text(`Descreva o problema com mais detalhes (minimo ${minimumDescriptionLength} caracteres).`);
            return false;
        }

        const fingerprint = currentAnalysisFingerprint();
        if (!force && analysisResult && analysisKey === fingerprint) {
            return true;
        }

        setAnalyzeLoading(true);
        $analysisError.addClass("d-none").text("");
        $analysisResultCard.addClass("d-none");

        const headers = {
            "Content-Type": "application/json",
            "X-Requested-With": "XMLHttpRequest"
        };

        if (antiforgeryToken) {
            headers.RequestVerificationToken = antiforgeryToken;
        }

        try {
            const response = await fetch(analyzeProblemUrl, {
                method: "POST",
                headers,
                body: JSON.stringify({
                    categoryId,
                    description
                })
            });

            if (!response.ok) {
                const rawError = await response.text();
                const message = parseErrorMessage(rawError) || "Nao foi possivel analisar o problema no momento.";
                $analysisError.removeClass("d-none").text(message);
                setAnalyzeLoading(false);
                return false;
            }

            const payload = await response.json();
            renderAnalysis(payload);
            setAnalyzeLoading(false);
            return true;
        } catch {
            $analysisError.removeClass("d-none").text("Falha de comunicacao com a API de analise. Tente novamente.");
            setAnalyzeLoading(false);
            return false;
        }
    }

    $descriptionInput.on("input", invalidateAnalysis);
    $categoryInputs.on("change", invalidateAnalysis);

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

    $analysisRetryButton.on("click", async function () {
        await analyzeProblem(true);
    });

    $(".next-step").on("click", async function () {
        if (currentStep === 1) {
            showStep(2);
            await analyzeProblem(false);
            return;
        }

        if (currentStep === 2) {
            const analyzed = await analyzeProblem(false);
            if (!analyzed) {
                return;
            }

            showStep(3);
            return;
        }

        if (currentStep === 3) {
            const zipOk = await resolveZip();
            if (!zipOk) {
                return;
            }

            showStep(4);
            return;
        }
    });

    $(".prev-step").on("click", function () {
        if (currentStep > 1) {
            showStep(currentStep - 1);
        }
    });

    updateProgress();
    updateReview();
});
