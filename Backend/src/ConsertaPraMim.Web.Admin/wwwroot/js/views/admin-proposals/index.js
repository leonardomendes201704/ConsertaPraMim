(function () {
    const config = window.cpmAdminProposals || {};
    const invalidateUrl = config.invalidateUrl || "";

    const feedback = document.getElementById("proposal-feedback");
    const modalEl = document.getElementById("invalidateModal");
    const reasonInput = document.getElementById("invalidate-reason");
    const messageEl = document.getElementById("invalidate-message");
    const confirmButton = document.getElementById("confirm-invalidate-btn");

    if (!invalidateUrl || !feedback || !modalEl || !reasonInput || !messageEl || !confirmButton || !window.bootstrap) {
        return;
    }

    const modal = new bootstrap.Modal(modalEl);
    let targetProposalId = null;

    function showFeedback(type, message) {
        feedback.className = `alert alert-${type} mb-3`;
        feedback.textContent = message;
        feedback.classList.remove("d-none");
    }

    function clearFeedback() {
        feedback.classList.add("d-none");
        feedback.textContent = "";
    }

    function setRowAsInvalidated(proposalId) {
        const row = document.getElementById(`proposal-row-${proposalId.replaceAll("-", "").toLowerCase()}`);
        if (!row) {
            return;
        }

        row.dataset.isInvalidated = "true";
        const statusCell = row.querySelector(".js-proposal-status");
        if (statusCell) {
            statusCell.innerHTML = "<span class=\"badge bg-danger-subtle text-danger-emphasis\">Invalidada</span>";
        }

        const actionButton = row.querySelector(".js-invalidate-proposal-btn");
        if (actionButton) {
            actionButton.remove();
        }
    }

    document.addEventListener("click", function (event) {
        const button = event.target.closest(".js-invalidate-proposal-btn");
        if (!button) {
            return;
        }

        targetProposalId = button.dataset.proposalId;
        const provider = button.dataset.providerName || "prestador";
        messageEl.textContent = `Confirma invalidar a proposta deste ${provider}?`;
        reasonInput.value = "";
        modal.show();
    });

    confirmButton.addEventListener("click", async function () {
        if (!targetProposalId) {
            return;
        }

        clearFeedback();
        confirmButton.disabled = true;
        try {
            const response = await fetch(invalidateUrl, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "X-Requested-With": "XMLHttpRequest"
                },
                body: JSON.stringify({
                    proposalId: targetProposalId,
                    reason: reasonInput.value?.trim() || null
                })
            });

            const payload = await response.json().catch(() => null);
            if (!response.ok || !payload?.success) {
                showFeedback("danger", payload?.errorMessage || `Falha ao invalidar proposta (${response.status}).`);
                return;
            }

            setRowAsInvalidated(targetProposalId);
            modal.hide();
            showFeedback("success", payload?.message || "Proposta invalidada com sucesso.");
        } catch (error) {
            console.error(error);
            showFeedback("danger", "Erro inesperado ao invalidar proposta.");
        } finally {
            confirmButton.disabled = false;
            targetProposalId = null;
        }
    });
})();
