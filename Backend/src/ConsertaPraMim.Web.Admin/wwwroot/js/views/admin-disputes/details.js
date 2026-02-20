(function () {
    const config = window.adminDisputeDetailsConfig || {};
    const disputeCaseId = config.disputeCaseId || "";
    const currentStatus = config.currentStatus || "";
    const updateWorkflowUrl = config.updateWorkflowUrl || "";
    const registerDecisionUrl = config.registerDecisionUrl || "";
    const isClosed = currentStatus === "Resolved" || currentStatus === "Rejected" || currentStatus === "Cancelled";

    const workflowStatus = document.getElementById("workflow-status");
    const workflowWaitingRole = document.getElementById("workflow-waiting-role");
    const workflowNote = document.getElementById("workflow-note");
    const workflowClaimOwnership = document.getElementById("workflow-claim-ownership");
    const workflowUpdateBtn = document.getElementById("workflow-update-btn");
    const decisionOutcome = document.getElementById("decision-outcome");
    const decisionJustification = document.getElementById("decision-justification");
    const decisionSummary = document.getElementById("decision-summary");
    const decisionFinancialAction = document.getElementById("decision-financial-action");
    const decisionFinancialAmount = document.getElementById("decision-financial-amount");
    const decisionFinancialReason = document.getElementById("decision-financial-reason");
    const decisionSubmitBtn = document.getElementById("decision-submit-btn");

    if (!disputeCaseId || !updateWorkflowUrl || !registerDecisionUrl) {
        return;
    }

    async function updateWorkflow() {
        if (isClosed) {
            alert("Disputa encerrada. Workflow bloqueado para edicao.");
            return;
        }

        const status = workflowStatus?.value || "";
        const waitingForRole = workflowWaitingRole?.value || "";
        const note = workflowNote?.value?.trim() || null;
        const claimOwnership = !!workflowClaimOwnership?.checked;

        if (!status) {
            alert("Selecione um status para o workflow.");
            return;
        }

        if (status === "WaitingParties" && !waitingForRole) {
            alert("Para WaitingParties, informe quem deve responder.");
            return;
        }

        if (workflowUpdateBtn) {
            workflowUpdateBtn.disabled = true;
        }
        try {
            const response = await fetch(updateWorkflowUrl, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "X-Requested-With": "XMLHttpRequest"
                },
                body: JSON.stringify({
                    disputeCaseId: disputeCaseId,
                    status: status,
                    waitingForRole: status === "WaitingParties" ? waitingForRole : null,
                    note: note,
                    claimOwnership: claimOwnership
                })
            });

            const payload = await response.json().catch(() => null);
            if (!response.ok || !payload?.success) {
                alert(payload?.errorMessage || `Falha ao atualizar workflow (${response.status}).`);
                return;
            }

            alert(payload?.message || "Workflow atualizado com sucesso.");
            window.location.reload();
        } catch (error) {
            console.error(error);
            alert("Erro inesperado ao atualizar workflow.");
        } finally {
            if (workflowUpdateBtn) {
                workflowUpdateBtn.disabled = false;
            }
        }
    }

    async function registerDecision() {
        if (isClosed) {
            alert("Disputa encerrada. Nao e possivel registrar nova decisao.");
            return;
        }

        const outcome = decisionOutcome?.value || "";
        const justification = decisionJustification?.value?.trim() || "";
        const resolutionSummary = decisionSummary?.value?.trim() || null;
        const financialAction = decisionFinancialAction?.value || "none";
        const financialAmountRaw = decisionFinancialAmount?.value || "";
        const financialReason = decisionFinancialReason?.value?.trim() || null;
        const financialAmount = financialAmountRaw ? Number(financialAmountRaw) : null;

        if (!outcome || !justification) {
            alert("Outcome e justificativa sao obrigatorios para registrar decisao.");
            return;
        }

        if (justification.length > 3000) {
            alert("Justificativa deve ter no maximo 3000 caracteres.");
            return;
        }

        if (financialAction !== "none") {
            if (!financialAmount || Number.isNaN(financialAmount) || financialAmount <= 0) {
                alert("Informe um valor financeiro valido maior que zero.");
                return;
            }

            if (!financialReason) {
                alert("Informe o motivo financeiro para a acao selecionada.");
                return;
            }
        }

        if (decisionSubmitBtn) {
            decisionSubmitBtn.disabled = true;
        }
        try {
            const response = await fetch(registerDecisionUrl, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "X-Requested-With": "XMLHttpRequest"
                },
                body: JSON.stringify({
                    disputeCaseId: disputeCaseId,
                    outcome: outcome,
                    justification: justification,
                    resolutionSummary: resolutionSummary,
                    financialAction: financialAction,
                    financialAmount: financialAction === "none" ? null : financialAmount,
                    financialReason: financialAction === "none" ? null : financialReason
                })
            });

            const payload = await response.json().catch(() => null);
            if (!response.ok || !payload?.success) {
                alert(payload?.errorMessage || `Falha ao registrar decisao (${response.status}).`);
                return;
            }

            alert(payload?.message || "Decisao registrada com sucesso.");
            window.location.reload();
        } catch (error) {
            console.error(error);
            alert("Erro inesperado ao registrar decisao.");
        } finally {
            if (decisionSubmitBtn) {
                decisionSubmitBtn.disabled = false;
            }
        }
    }

    workflowUpdateBtn?.addEventListener("click", updateWorkflow);
    decisionSubmitBtn?.addEventListener("click", registerDecision);
})();
