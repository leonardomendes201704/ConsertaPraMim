(function () {
    const config = window.cpmAdminNoShowThresholds || {};
    const saveUrl = config.saveUrl || "";

    const form = document.getElementById("noShowThresholdForm");
    const saveButton = document.getElementById("saveNoShowThresholdsBtn");
    const successState = document.getElementById("noShowThresholdSuccessState");
    const errorState = document.getElementById("noShowThresholdErrorState");
    const updatedLabel = document.getElementById("thresholdsLastUpdatedLabel");

    if (!form || !saveButton || !successState || !errorState || !updatedLabel || !saveUrl) {
        return;
    }

    function formatNumber(value) {
        return new Intl.NumberFormat("pt-BR").format(Number(value ?? 0));
    }

    function formatPercent(value) {
        return Number(value ?? 0).toLocaleString("pt-BR", { minimumFractionDigits: 1, maximumFractionDigits: 1 });
    }

    function formatDateTime(value) {
        if (!value) {
            return "-";
        }

        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return "-";
        }

        return date.toLocaleString("pt-BR");
    }

    function setSuccess(message) {
        if (!message) {
            successState.textContent = "";
            successState.classList.add("d-none");
            return;
        }

        successState.textContent = message;
        successState.classList.remove("d-none");
    }

    function setError(message) {
        if (!message) {
            errorState.textContent = "";
            errorState.classList.add("d-none");
            return;
        }

        errorState.textContent = message;
        errorState.classList.remove("d-none");
    }

    function updateSummary(configuration) {
        if (!configuration) {
            return;
        }

        const setText = (selector, value) => {
            const element = document.querySelector(selector);
            if (element) {
                element.textContent = value;
            }
        };

        setText("[data-threshold-summary-no-show-warning]", `${formatPercent(configuration.noShowRateWarningPercent)}%`);
        setText("[data-threshold-summary-no-show-critical]", `${formatPercent(configuration.noShowRateCriticalPercent)}%`);
        setText("[data-threshold-summary-queue-warning]", formatNumber(configuration.highRiskQueueWarningCount));
        setText("[data-threshold-summary-queue-critical]", formatNumber(configuration.highRiskQueueCriticalCount));
    }

    function readNumberValue(id) {
        const input = document.getElementById(id);
        if (!input) {
            return 0;
        }

        return Number(input.value ?? 0);
    }

    function buildPayload() {
        const notesInput = document.getElementById("thresholdNotes");

        return {
            noShowRateWarningPercent: readNumberValue("thresholdNoShowRateWarningPercent"),
            noShowRateCriticalPercent: readNumberValue("thresholdNoShowRateCriticalPercent"),
            highRiskQueueWarningCount: readNumberValue("thresholdHighRiskQueueWarningCount"),
            highRiskQueueCriticalCount: readNumberValue("thresholdHighRiskQueueCriticalCount"),
            reminderSendSuccessWarningPercent: readNumberValue("thresholdReminderSendSuccessWarningPercent"),
            reminderSendSuccessCriticalPercent: readNumberValue("thresholdReminderSendSuccessCriticalPercent"),
            notes: notesInput ? String(notesInput.value ?? "").trim() : ""
        };
    }

    form.addEventListener("submit", async function (event) {
        event.preventDefault();
        setSuccess(null);
        setError(null);

        const payload = buildPayload();
        const originalLabel = saveButton.innerHTML;
        saveButton.disabled = true;
        saveButton.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i>Salvando';

        try {
            const response = await fetch(saveUrl, {
                method: "POST",
                credentials: "same-origin",
                headers: {
                    "Content-Type": "application/json"
                },
                body: JSON.stringify(payload)
            });

            const result = await response.json().catch(() => null);
            if (!response.ok || !result || result.success !== true || !result.configuration) {
                setError(result?.errorMessage || "Falha ao atualizar thresholds de no-show.");
                return;
            }

            updateSummary(result.configuration);
            updatedLabel.textContent = `Atualizado em ${formatDateTime(result.updatedAtUtc || new Date().toISOString())}`;
            setSuccess("Thresholds atualizados com sucesso.");
        } catch (error) {
            setError("Nao foi possivel atualizar thresholds de no-show.");
            console.error(error);
        } finally {
            saveButton.disabled = false;
            saveButton.innerHTML = originalLabel;
        }
    });
})();

