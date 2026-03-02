$(document).ready(function () {
    const config = window.serviceRequestCreateConfig || {};
    const resolveZipUrl = config.resolveZipUrl || "";
    const analyzeProblemUrl = config.analyzeProblemUrl || "";
    const pendingReviewGateConfig = config.pendingReviewGate || {};
    const submitPendingReviewUrl = pendingReviewGateConfig.submitReviewUrl || "";
    const initialPendingReviews = Array.isArray(pendingReviewGateConfig.pendingReviews)
        ? pendingReviewGateConfig.pendingReviews.slice()
        : [];
    if (!resolveZipUrl || !analyzeProblemUrl) return;

    const maxSteps = 4;
    const minimumDescriptionLength = 15;

    let currentStep = 1;
    let analysisResult = null;
    let analysisKey = null;
    let isAnalyzing = false;
    let isSubmittingPendingReview = false;
    let resolvedPendingReviews = 0;
    let pendingReviewQueue = initialPendingReviews.slice();
    let pendingReviewModal = null;

    const $wizardForm = $("#wizard-form");
    const antiforgeryToken = $wizardForm.find("input[name='__RequestVerificationToken']").val() || "";
    const $pendingReviewGateModal = $("#pending-review-gate-modal");
    const $pendingReviewGateDescription = $("#pending-review-gate-description");
    const $pendingReviewGateError = $("#pending-review-gate-error");
    const $pendingReviewGateSuccess = $("#pending-review-gate-success");
    const $pendingReviewGateProgressLabel = $("#pending-review-gate-progress-label");
    const $pendingReviewGateRemainingLabel = $("#pending-review-gate-remaining-label");
    const $pendingReviewGateCategory = $("#pending-review-gate-category");
    const $pendingReviewGateRole = $("#pending-review-gate-role");
    const $pendingReviewGateCounterparty = $("#pending-review-gate-counterparty");
    const $pendingReviewGateCompletedAt = $("#pending-review-gate-completed-at");
    const $pendingReviewGateDeadline = $("#pending-review-gate-deadline");
    const $pendingReviewRatingInputs = $(".pending-review-rating-input");
    const $pendingReviewComment = $("#pending-review-comment");
    const $pendingReviewSubmitButton = $("#pending-review-submit-btn");
    const $descriptionInput = $("textarea[name='Description']");
    const $categoryInputs = $("input[name='CategoryId']");
    const $zipInput = $("#zip-input");
    const $zipStatus = $("#zip-status");
    const $streetHidden = $("#street-hidden");
    const $neighborhoodHidden = $("#neighborhood-hidden");
    const $cityHidden = $("#city-hidden");
    const $latitudeHidden = $("#latitude-hidden");
    const $longitudeHidden = $("#longitude-hidden");
    const $problemAnalysisSummaryHidden = $("#problem-analysis-summary-hidden");
    const $problemAnalysisHighlightsHidden = $("#problem-analysis-highlights-hidden");
    const $streetDisplay = $("#street-display");
    const $neighborhoodDisplay = $("#neighborhood-display");
    const $cityDisplay = $("#city-display");
    const $locationMapWrapper = $("#location-map-wrapper");
    const $analysisLoading = $("#analysis-loading");
    const $analysisError = $("#analysis-error");
    const $analysisResultCard = $("#analysis-result");
    const $analysisCategoryLabel = $("#analysis-category-label");
    const $analysisFallbackBadge = $("#analysis-fallback-badge");
    const $analysisSummary = $("#analysis-summary");
    const $analysisHighlights = $("#analysis-highlights");
    const $analysisRetryButton = $("#analysis-retry-btn");
    const $analysisNextButton = $("#analysis-next-btn");
    let locationMap = null;
    let locationMarker = null;
    let locationRadius = null;

    if ($streetHidden.val()) {
        $streetDisplay.val($streetHidden.val());
    }

    if ($cityHidden.val()) {
        $cityDisplay.val($cityHidden.val());
    }

    if ($neighborhoodHidden.val()) {
        $neighborhoodDisplay.val($neighborhoodHidden.val());
    }

    function hasBlockingPendingReviews() {
        return pendingReviewQueue.length > 0;
    }

    function clearPendingReviewMessages() {
        $pendingReviewGateError.addClass("d-none").text("");
        $pendingReviewGateSuccess.addClass("d-none").text("");
    }

    function ensurePendingReviewModal() {
        if (!$pendingReviewGateModal.length || !window.bootstrap || !window.bootstrap.Modal) {
            return null;
        }

        if (!pendingReviewModal) {
            pendingReviewModal = new window.bootstrap.Modal($pendingReviewGateModal[0], {
                backdrop: "static",
                keyboard: false
            });

            $pendingReviewGateModal.on("hide.bs.modal", function (event) {
                if (hasBlockingPendingReviews()) {
                    event.preventDefault();
                }
            });
        }

        return pendingReviewModal;
    }

    function formatBusinessDate(value) {
        if (!value) return "---";

        const parsed = new Date(value);
        if (Number.isNaN(parsed.getTime())) {
            return "---";
        }

        return parsed.toLocaleString("pt-BR", {
            timeZone: "America/Sao_Paulo",
            dateStyle: "short",
            timeStyle: "short"
        });
    }

    function syncPendingReviewRatingButtons() {
        $pendingReviewRatingInputs.each(function () {
            const $input = $(this);
            const $label = $input.closest("label");
            const checked = $input.is(":checked");

            $label
                .toggleClass("btn-primary", checked)
                .toggleClass("text-white", checked)
                .toggleClass("active", checked)
                .toggleClass("btn-outline-primary", !checked);
        });
    }

    function resetPendingReviewForm() {
        $pendingReviewRatingInputs.prop("checked", false);
        $pendingReviewComment.val("");
        syncPendingReviewRatingButtons();
    }

    function selectedPendingReviewRating() {
        const selectedValue = $pendingReviewRatingInputs.filter(":checked").val();
        return selectedValue ? Number(selectedValue) : 0;
    }

    function renderPendingReviewGate() {
        const currentPendingReview = pendingReviewQueue[0];
        const totalPendingReviews = resolvedPendingReviews + pendingReviewQueue.length;

        if (!currentPendingReview) {
            $pendingReviewGateProgressLabel.text("Sem pendencias");
            $pendingReviewGateRemainingLabel.text("Nenhuma avaliacao pendente.");
            $pendingReviewGateCategory.text("Servico");
            $pendingReviewGateRole.text("Prestador");
            $pendingReviewGateCounterparty.text("---");
            $pendingReviewGateCompletedAt.text("---");
            $pendingReviewGateDeadline.text("---");
            return;
        }

        $pendingReviewGateDescription.text(
            pendingReviewGateConfig.blockMessage ||
            "Antes de abrir um novo pedido, conclua a avaliacao dos servicos ja finalizados."
        );
        $pendingReviewGateProgressLabel.text(`Pendencia ${resolvedPendingReviews + 1} de ${totalPendingReviews}`);
        $pendingReviewGateRemainingLabel.text(
            pendingReviewQueue.length === 1
                ? "Resta 1 avaliacao obrigatoria."
                : `Restam ${pendingReviewQueue.length} avaliacoes obrigatorias.`
        );
        $pendingReviewGateCategory.text(currentPendingReview.category || "Servico");
        $pendingReviewGateRole.text(currentPendingReview.counterpartyRole || "Prestador");
        $pendingReviewGateCounterparty.text(currentPendingReview.counterpartyName || "---");
        $pendingReviewGateCompletedAt.text(formatBusinessDate(currentPendingReview.completedAtUtc));
        $pendingReviewGateDeadline.text(formatBusinessDate(currentPendingReview.reviewDeadlineUtc));
    }

    function applyPendingReviewGateState() {
        const modalInstance = ensurePendingReviewModal();
        const blocked = hasBlockingPendingReviews();

        $wizardForm.toggleClass("pending-review-gate-blocked", blocked);

        if (!blocked) {
            clearPendingReviewMessages();
            if (modalInstance) {
                modalInstance.hide();
            }
            return;
        }

        renderPendingReviewGate();
        resetPendingReviewForm();
        clearPendingReviewMessages();

        if (modalInstance) {
            modalInstance.show();
        }
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
        $problemAnalysisSummaryHidden.val("");
        $problemAnalysisHighlightsHidden.val("");
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
        $neighborhoodHidden.val("");
        $cityHidden.val("");
        $latitudeHidden.val("0");
        $longitudeHidden.val("0");
        $streetDisplay.val("");
        $neighborhoodDisplay.val("");
        $cityDisplay.val("");
        hideLocationMap();
    }

    function ensureLocationMap() {
        if (locationMap || typeof L === "undefined") {
            return locationMap;
        }

        const mapElement = document.getElementById("service-location-map");
        if (!mapElement) {
            return null;
        }

        locationMap = L.map(mapElement, {
            zoomControl: true,
            attributionControl: true
        });

        L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
            maxZoom: 19,
            attribution: "&copy; OpenStreetMap contributors"
        }).addTo(locationMap);

        return locationMap;
    }

    function showLocationOnMap(latitude, longitude) {
        const lat = Number(latitude);
        const lng = Number(longitude);
        if (!Number.isFinite(lat) || !Number.isFinite(lng)) {
            hideLocationMap();
            return;
        }

        const map = ensureLocationMap();
        if (!map) {
            return;
        }

        $locationMapWrapper.removeClass("d-none");

        if (!locationMarker) {
            locationMarker = L.marker([lat, lng]).addTo(map);
        } else {
            locationMarker.setLatLng([lat, lng]);
        }

        if (!locationRadius) {
            locationRadius = L.circle([lat, lng], {
                radius: 1000,
                color: "#2563eb",
                weight: 2,
                fillColor: "#3b82f6",
                fillOpacity: 0.18
            }).addTo(map);
        } else {
            locationRadius.setLatLng([lat, lng]);
            locationRadius.setRadius(1000);
        }

        map.setView([lat, lng], 14);
        setTimeout(() => map.invalidateSize(), 50);
    }

    function hideLocationMap() {
        $locationMapWrapper.addClass("d-none");
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
        const neighborhood = $neighborhoodHidden.val() || "Bairro nao informado";
        const city = $cityHidden.val() || "Cidade nao informada";

        $("#review-desc").text(description || "---");
        $("#review-address").text(`${street}, ${neighborhood}, ${city} - CEP ${zip || "---"}`);

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

    async function submitPendingReviewAndContinue() {
        if (isSubmittingPendingReview) {
            return;
        }

        const currentPendingReview = pendingReviewQueue[0];
        if (!currentPendingReview) {
            applyPendingReviewGateState();
            return;
        }

        if (!submitPendingReviewUrl) {
            $pendingReviewGateError
                .removeClass("d-none")
                .text("O envio da avaliacao obrigatoria nao esta disponivel no momento. Atualize a pagina e tente novamente.");
            return;
        }

        const rating = selectedPendingReviewRating();
        if (!rating) {
            $pendingReviewGateError
                .removeClass("d-none")
                .text("Selecione uma nota entre 1 e 5 para desbloquear a criacao do novo pedido.");
            return;
        }

        clearPendingReviewMessages();
        isSubmittingPendingReview = true;
        $pendingReviewSubmitButton.prop("disabled", true).text("Enviando avaliacao...");

        const headers = {
            "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8",
            "X-Requested-With": "XMLHttpRequest"
        };

        if (antiforgeryToken) {
            headers.RequestVerificationToken = antiforgeryToken;
        }

        const body = new URLSearchParams();
        body.set("requestId", currentPendingReview.requestId);
        body.set("rating", `${rating}`);
        body.set("comment", `${($pendingReviewComment.val() || "").trim()}`);

        try {
            const response = await fetch(submitPendingReviewUrl, {
                method: "POST",
                headers,
                body: body.toString()
            });

            const rawResponse = await response.text();
            let payload = null;

            try {
                payload = rawResponse ? JSON.parse(rawResponse) : null;
            } catch {
                payload = null;
            }

            if (!response.ok) {
                const message = payload && payload.message
                    ? payload.message
                    : (parseErrorMessage(rawResponse) || "Nao foi possivel registrar a avaliacao obrigatoria.");

                $pendingReviewGateError.removeClass("d-none").text(message);
                return;
            }

            resolvedPendingReviews += 1;
            pendingReviewQueue = payload && Array.isArray(payload.remainingPendingReviews)
                ? payload.remainingPendingReviews
                : [];

            const successMessage = payload && payload.message
                ? payload.message
                : "Avaliacao enviada com sucesso.";

            if (pendingReviewQueue.length > 0) {
                renderPendingReviewGate();
                resetPendingReviewForm();
                $pendingReviewGateSuccess
                    .removeClass("d-none")
                    .text(`${successMessage} Ainda restam avaliacoes obrigatorias.`);
                return;
            }

            $pendingReviewGateSuccess.removeClass("d-none").text(successMessage);
            window.setTimeout(() => {
                applyPendingReviewGateState();
            }, 400);
        } catch {
            $pendingReviewGateError
                .removeClass("d-none")
                .text("Falha de comunicacao ao registrar a avaliacao obrigatoria. Tente novamente.");
        } finally {
            isSubmittingPendingReview = false;
            $pendingReviewSubmitButton.prop("disabled", false).text("Enviar avaliacao e continuar");
        }
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
        const normalizedHighlights = [];
        if (highlights.length === 0) {
            $analysisHighlights.append("<li>Sem highlights adicionais para este problema.</li>");
        } else {
            highlights.forEach((highlight) => {
                const text = `${highlight || ""}`.trim();
                if (text.length > 0) {
                    $analysisHighlights.append(`<li>${text}</li>`);
                    normalizedHighlights.push(text);
                }
            });
        }

        $problemAnalysisSummaryHidden.val((result.understandingSummary || "").trim());
        $problemAnalysisHighlightsHidden.val(JSON.stringify(normalizedHighlights));

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
            const neighborhood = data.neighborhood && data.neighborhood.trim().length > 0
                ? data.neighborhood
                : "Bairro nao informado";
            const city = data.city && data.city.trim().length > 0 ? data.city : "Cidade nao informada";
            const latitude = Number(data.latitude);
            const longitude = Number(data.longitude);

            $zipInput.val(formatZip(data.zipCode || digits));
            $streetHidden.val(street);
            $neighborhoodHidden.val(neighborhood);
            $cityHidden.val(city);
            $latitudeHidden.val(Number.isFinite(latitude) ? latitude : 0);
            $longitudeHidden.val(Number.isFinite(longitude) ? longitude : 0);
            $streetDisplay.val(street);
            $neighborhoodDisplay.val(neighborhood);
            $cityDisplay.val(city);
            setZipStatus("Endereco preenchido automaticamente.", false);
            showLocationOnMap(latitude, longitude);
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

    const initialLat = Number($latitudeHidden.val());
    const initialLng = Number($longitudeHidden.val());
    if (Number.isFinite(initialLat) && Number.isFinite(initialLng) && Math.abs(initialLat) > 0.000001 && Math.abs(initialLng) > 0.000001) {
        showLocationOnMap(initialLat, initialLng);
    }

    $analysisRetryButton.on("click", async function () {
        await analyzeProblem(true);
    });

    $pendingReviewRatingInputs.on("change", function () {
        syncPendingReviewRatingButtons();
        if (selectedPendingReviewRating()) {
            $pendingReviewGateError.addClass("d-none").text("");
        }
    });

    $pendingReviewSubmitButton.on("click", async function () {
        await submitPendingReviewAndContinue();
    });

    $(".next-step").on("click", async function () {
        if (hasBlockingPendingReviews()) {
            applyPendingReviewGateState();
            return;
        }

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
        if (hasBlockingPendingReviews()) {
            applyPendingReviewGateState();
            return;
        }

        if (currentStep > 1) {
            showStep(currentStep - 1);
        }
    });

    $wizardForm.on("submit", function (event) {
        if (!hasBlockingPendingReviews()) {
            return;
        }

        event.preventDefault();
        applyPendingReviewGateState();
    });

    updateProgress();
    updateReview();
    syncPendingReviewRatingButtons();
    applyPendingReviewGateState();
});
