(function () {
    const config = window.cpmAdminTrustQueue || {};
    const reviewUrl = config.reviewUrl || "";
    const historyUrl = config.historyUrl || "";

    const feedbackBox = document.getElementById("provider-trust-feedback");
    const reviewModalElement = document.getElementById("trustReviewModal");
    const historyOffcanvasElement = document.getElementById("trustHistoryOffcanvas");
    const historyContent = document.getElementById("trust-history-content");
    const historyProviderName = document.getElementById("trust-history-provider-name");
    const providerSummary = document.getElementById("trust-review-provider-summary");
    const providerIdInput = document.getElementById("trust-review-provider-id");
    const trustStatusSelect = document.getElementById("trust-review-status");
    const riskLevelSelect = document.getElementById("trust-review-risk");
    const reasonInput = document.getElementById("trust-review-reason");
    const evidenceInput = document.getElementById("trust-review-evidence");
    const submitButton = document.getElementById("trust-review-submit-btn");

    if (!reviewUrl || !historyUrl || !feedbackBox || !reviewModalElement || !historyOffcanvasElement || !submitButton || !window.bootstrap) {
        return;
    }

    const reviewModal = new bootstrap.Modal(reviewModalElement);
    const historyOffcanvas = new bootstrap.Offcanvas(historyOffcanvasElement);

    function showFeedback(type, message) {
        feedbackBox.className = `alert alert-${type} mb-3`;
        feedbackBox.textContent = message;
        feedbackBox.classList.remove("d-none");
    }

    function clearFeedback() {
        feedbackBox.classList.add("d-none");
        feedbackBox.textContent = "";
    }

    function formatDate(value) {
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return "-";
        }

        return date.toLocaleString("pt-BR", {
            day: "2-digit",
            month: "2-digit",
            year: "numeric",
            hour: "2-digit",
            minute: "2-digit"
        });
    }

    function openReviewModal(button) {
        const providerId = button.dataset.providerId || "";
        const providerName = button.dataset.providerName || "Prestador";
        const trustStatus = button.dataset.trustStatus || "Pending";
        const riskLevel = button.dataset.riskLevel || "Low";

        providerSummary.textContent = `${providerName} (${providerId})`;
        providerIdInput.value = providerId;
        trustStatusSelect.value = trustStatus;
        riskLevelSelect.value = riskLevel;
        reasonInput.value = "";
        evidenceInput.value = "";
        reviewModal.show();
    }

    async function submitReview() {
        const providerId = providerIdInput.value;
        if (!providerId) {
            showFeedback("danger", "Prestador invalido para revisao.");
            return;
        }

        clearFeedback();
        submitButton.disabled = true;

        try {
            const response = await fetch(reviewUrl, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "X-Requested-With": "XMLHttpRequest"
                },
                body: JSON.stringify({
                    providerUserId: providerId,
                    trustStatus: trustStatusSelect.value,
                    riskLevel: riskLevelSelect.value,
                    decisionReason: reasonInput.value?.trim() || null,
                    evidenceSummary: evidenceInput.value?.trim() || null
                })
            });

            const payload = await response.json().catch(() => null);
            if (!response.ok || !payload?.success) {
                showFeedback("danger", payload?.errorMessage || `Falha ao registrar revisao (${response.status}).`);
                return;
            }

            reviewModal.hide();
            showFeedback("success", payload?.message || "Revisao registrada com sucesso.");
            window.setTimeout(() => window.location.reload(), 900);
        } catch (error) {
            console.error(error);
            showFeedback("danger", "Falha inesperada ao registrar revisao de confianca.");
        } finally {
            submitButton.disabled = false;
        }
    }

    async function loadHistory(button) {
        const providerId = button.dataset.providerId || "";
        const providerName = button.dataset.providerName || "Prestador";
        if (!providerId) {
            showFeedback("danger", "Prestador invalido para consulta de historico.");
            return;
        }

        historyProviderName.textContent = providerName;
        historyContent.innerHTML = "<div class=\"text-muted\">Carregando historico...</div>";
        historyOffcanvas.show();

        try {
            const url = `${historyUrl}?providerUserId=${encodeURIComponent(providerId)}&take=30`;
            const response = await fetch(url, {
                method: "GET",
                headers: {
                    "X-Requested-With": "XMLHttpRequest"
                }
            });

            const payload = await response.json().catch(() => null);
            if (!response.ok || !payload?.success) {
                historyContent.innerHTML = `<div class="alert alert-warning mb-0">${payload?.errorMessage || `Falha ao carregar historico (${response.status}).`}</div>`;
                return;
            }

            const items = Array.isArray(payload.items) ? payload.items : [];
            if (items.length === 0) {
                historyContent.innerHTML = "<div class=\"text-muted\">Nenhuma revisao registrada para este prestador.</div>";
                return;
            }

            const rows = items
                .map(item => `
                    <tr>
                        <td>${formatDate(item.reviewedAtUtc)}</td>
                        <td>${item.previousTrustStatus} -> ${item.newTrustStatus}</td>
                        <td>${item.previousRiskLevel} -> ${item.newRiskLevel}</td>
                        <td>${item.reviewedByAdminEmail || "-"}</td>
                        <td>${item.decisionReason || "-"}</td>
                    </tr>`)
                .join("");

            historyContent.innerHTML = `
                <div class="table-responsive">
                    <table class="table table-sm align-middle mb-0">
                        <thead class="table-light">
                            <tr>
                                <th>Data</th>
                                <th>Confianca</th>
                                <th>Risco</th>
                                <th>Admin</th>
                                <th>Motivo</th>
                            </tr>
                        </thead>
                        <tbody>${rows}</tbody>
                    </table>
                </div>`;
        } catch (error) {
            console.error(error);
            historyContent.innerHTML = "<div class=\"alert alert-danger mb-0\">Falha inesperada ao carregar historico.</div>";
        }
    }

    document.addEventListener("click", event => {
        const reviewButton = event.target.closest(".js-trust-review-btn");
        if (reviewButton) {
            openReviewModal(reviewButton);
            return;
        }

        const historyButton = event.target.closest(".js-trust-history-btn");
        if (historyButton) {
            loadHistory(historyButton);
        }
    });

    submitButton.addEventListener("click", submitReview);
})();
