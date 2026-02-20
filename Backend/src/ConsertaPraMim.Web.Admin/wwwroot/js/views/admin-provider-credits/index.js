(function () {
    const config = window.adminProviderCreditsConfig || {};
    const providerId = config.providerId || null;
    const grantUrl = config.grantUrl || "";
    const reverseUrl = config.reverseUrl || "";

    if (!grantUrl || !reverseUrl) {
        return;
    }

    const feedback = document.getElementById("credits-feedback");
    const grantModalElement = document.getElementById("grantModal");
    const reversalModalElement = document.getElementById("reversalModal");
    const grantModal = grantModalElement ? new bootstrap.Modal(grantModalElement) : null;
    const reversalModal = reversalModalElement ? new bootstrap.Modal(reversalModalElement) : null;

    const openGrantButton = document.getElementById("open-grant-modal-btn");
    const openReversalButton = document.getElementById("open-reversal-modal-btn");
    const confirmGrantButton = document.getElementById("confirm-grant-btn");
    const confirmReversalButton = document.getElementById("confirm-reversal-btn");

    const grantAmountInput = document.getElementById("grant-amount");
    const grantReasonInput = document.getElementById("grant-reason");
    const grantTypeInput = document.getElementById("grant-type");
    const grantExpiresAtInput = document.getElementById("grant-expires-at");
    const grantCampaignWrapper = document.getElementById("grant-campaign-wrapper");
    const grantCampaignCodeInput = document.getElementById("grant-campaign-code");
    const grantNotesInput = document.getElementById("grant-notes");
    const grantModalFeedback = document.getElementById("grant-modal-feedback");

    const reversalAmountInput = document.getElementById("reversal-amount");
    const reversalReasonInput = document.getElementById("reversal-reason");
    const reversalOriginalEntryInput = document.getElementById("reversal-original-entry");
    const reversalNotesInput = document.getElementById("reversal-notes");
    const reversalModalFeedback = document.getElementById("reversal-modal-feedback");

    const formatPtBr = (value) => {
        return Number(value || 0).toLocaleString("pt-BR", {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        });
    };

    const showFeedback = (type, message) => {
        if (!feedback) {
            return;
        }

        feedback.className = `alert alert-${type} mb-3`;
        feedback.textContent = message;
        feedback.classList.remove("d-none");
    };

    const clearFeedback = () => {
        if (!feedback) {
            return;
        }

        feedback.className = "d-none alert mb-3";
        feedback.textContent = "";
    };

    const showModalError = (container, message) => {
        if (!container) {
            return;
        }

        container.classList.remove("d-none");
        container.textContent = message;
    };

    const clearModalError = (container) => {
        if (!container) {
            return;
        }

        container.classList.add("d-none");
        container.textContent = "";
    };

    const applyCurrencyMask = (input) => {
        if (!input) {
            return;
        }

        const digits = input.value.replace(/\D/g, "").slice(0, 11);
        if (!digits) {
            input.value = "";
            return;
        }

        const numeric = Number(digits) / 100;
        input.value = `R$ ${formatPtBr(numeric)}`;
    };

    const parseDecimal = (value) => {
        if (!value) {
            return null;
        }

        const normalized = value
            .replace(/\s/g, "")
            .replace("R$", "")
            .replace(/\./g, "")
            .replace(",", ".")
            .trim();

        if (!normalized) {
            return null;
        }

        const parsed = Number(normalized);
        return Number.isFinite(parsed) ? parsed : null;
    };

    const toUtcIso = (dateTimeLocal) => {
        if (!dateTimeLocal) {
            return null;
        }

        const parsed = new Date(dateTimeLocal);
        if (Number.isNaN(parsed.getTime())) {
            return null;
        }

        return parsed.toISOString();
    };

    const postJson = async (url, payload) => {
        const response = await fetch(url, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "X-Requested-With": "XMLHttpRequest"
            },
            body: JSON.stringify(payload)
        });

        const body = await response.json().catch(() => null);
        if (!response.ok || !body?.success) {
            throw new Error(body?.errorMessage || `Falha na operacao (${response.status}).`);
        }

        return body;
    };

    const syncGrantTypeUi = () => {
        const isCampaign = (grantTypeInput?.value || "") === "Campanha";
        if (grantCampaignWrapper) {
            grantCampaignWrapper.classList.toggle("d-none", !isCampaign);
        }
    };

    openGrantButton?.addEventListener("click", function () {
        clearFeedback();
        clearModalError(grantModalFeedback);
        if (grantAmountInput) grantAmountInput.value = "";
        if (grantReasonInput) grantReasonInput.value = "";
        if (grantTypeInput) grantTypeInput.value = "Premio";
        if (grantExpiresAtInput) grantExpiresAtInput.value = "";
        if (grantCampaignCodeInput) grantCampaignCodeInput.value = "";
        if (grantNotesInput) grantNotesInput.value = "";
        syncGrantTypeUi();
        grantModal?.show();
    });

    openReversalButton?.addEventListener("click", function () {
        clearFeedback();
        clearModalError(reversalModalFeedback);
        if (reversalAmountInput) reversalAmountInput.value = "";
        if (reversalReasonInput) reversalReasonInput.value = "";
        if (reversalOriginalEntryInput) reversalOriginalEntryInput.value = "";
        if (reversalNotesInput) reversalNotesInput.value = "";
        reversalModal?.show();
    });

    grantTypeInput?.addEventListener("change", syncGrantTypeUi);
    grantAmountInput?.addEventListener("input", function () { applyCurrencyMask(grantAmountInput); });
    reversalAmountInput?.addEventListener("input", function () { applyCurrencyMask(reversalAmountInput); });

    confirmGrantButton?.addEventListener("click", async function () {
        if (!providerId) {
            showModalError(grantModalFeedback, "Selecione um prestador antes de conceder credito.");
            return;
        }

        confirmGrantButton.disabled = true;
        clearModalError(grantModalFeedback);
        clearFeedback();

        try {
            const amount = parseDecimal(grantAmountInput?.value || "");
            const reason = (grantReasonInput?.value || "").trim();
            const grantType = grantTypeInput?.value || "Premio";
            const expiresAtUtc = toUtcIso(grantExpiresAtInput?.value || "");
            const campaignCode = (grantCampaignCodeInput?.value || "").trim();
            const notes = (grantNotesInput?.value || "").trim();

            if (amount === null || amount <= 0) {
                throw new Error("Informe um valor de credito valido.");
            }

            if (!reason) {
                throw new Error("Informe o motivo da concessao.");
            }

            if (grantType === "Campanha" && !expiresAtUtc) {
                throw new Error("Para campanha, informe a data de expiracao.");
            }

            const result = await postJson(grantUrl, {
                providerId: providerId,
                amount: amount,
                reason: reason,
                grantType: grantType,
                expiresAtUtc: expiresAtUtc,
                notes: notes || null,
                campaignCode: campaignCode || null
            });

            const notificationSent = result?.mutation?.notificationSent === true;
            showFeedback(
                "success",
                notificationSent
                    ? "Credito concedido com sucesso e notificacao enviada ao prestador."
                    : "Credito concedido com sucesso."
            );

            grantModal?.hide();
            window.location.reload();
        } catch (error) {
            showModalError(grantModalFeedback, error.message || "Falha ao conceder credito.");
        } finally {
            confirmGrantButton.disabled = false;
        }
    });

    confirmReversalButton?.addEventListener("click", async function () {
        if (!providerId) {
            showModalError(reversalModalFeedback, "Selecione um prestador antes de estornar credito.");
            return;
        }

        confirmReversalButton.disabled = true;
        clearModalError(reversalModalFeedback);
        clearFeedback();

        try {
            const amount = parseDecimal(reversalAmountInput?.value || "");
            const reason = (reversalReasonInput?.value || "").trim();
            const originalEntryId = (reversalOriginalEntryInput?.value || "").trim();
            const notes = (reversalNotesInput?.value || "").trim();

            if (amount === null || amount <= 0) {
                throw new Error("Informe um valor de estorno valido.");
            }

            if (!reason) {
                throw new Error("Informe o motivo do estorno.");
            }

            const result = await postJson(reverseUrl, {
                providerId: providerId,
                amount: amount,
                reason: reason,
                originalEntryId: originalEntryId || null,
                notes: notes || null
            });

            const notificationSent = result?.mutation?.notificationSent === true;
            showFeedback(
                "success",
                notificationSent
                    ? "Estorno executado com sucesso e notificacao enviada ao prestador."
                    : "Estorno executado com sucesso."
            );

            reversalModal?.hide();
            window.location.reload();
        } catch (error) {
            showModalError(reversalModalFeedback, error.message || "Falha ao estornar credito.");
        } finally {
            confirmReversalButton.disabled = false;
        }
    });
})();
