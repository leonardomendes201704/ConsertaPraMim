(function () {
    function badgeClass(status) {
        if (status === "Created") return "bg-info";
        if (status === "Matching") return "bg-primary";
        if (status === "Scheduled") return "bg-warning";
        if (status === "PendingClientCompletionAcceptance") return "bg-info";
        if (status === "Completed") return "bg-success";
        return "bg-secondary";
    }

    function statusLabel(status) {
        if (status === "PendingClientCompletionAcceptance") return "Aguardando aceite de conclusao";
        return status;
    }

    function renderRecentRequests(items) {
        const container = document.getElementById("recent-requests-container");
        if (!container) return;

        if (!items || !items.length) {
            container.innerHTML = `
                <div class="card border-0 shadow-sm p-5 text-center bg-white">
                    <div class="mb-3 text-muted">
                        <i class="fas fa-clipboard-list fa-4x opacity-25"></i>
                    </div>
                    <h5>Você ainda não abriu nenhum chamado</h5>
                    <p class="text-muted">Descreva seu problema e receba orçamentos grátis.</p>
                </div>`;
            return;
        }

        const html = items.map(item => `
            <div class="col-md-6 col-xl-4">
                <div class="card border-0 shadow-sm h-100 transition-hover recent-request-card" role="link" tabindex="0" data-url="/ServiceRequests/Details/${item.id}">
                    <div class="card-body p-4">
                        <div class="d-flex justify-content-between align-items-start mb-3">
                            <span class="badge bg-primary-subtle text-primary rounded-pill px-3">${item.category}</span>
                            <span class="badge ${badgeClass(item.status)} rounded-pill">${statusLabel(item.status)}</span>
                        </div>
                        <h5 class="fw-bold mb-2">${item.description}</h5>
                        <p class="text-muted small mb-4"><i class="fas fa-map-marker-alt me-1"></i> ${item.city}, ${item.street}</p>
                        <div class="d-flex justify-content-between align-items-center mt-auto pt-3 border-top">
                            <small class="text-muted">Postado em ${item.createdAt}</small>
                            <a href="/ServiceRequests/Details/${item.id}" class="btn btn-sm btn-link text-decoration-none fw-bold p-0">Ver Detalhes <i class="fas fa-chevron-right ms-1 small"></i></a>
                        </div>
                    </div>
                </div>
            </div>`).join("");

        container.innerHTML = `<div class="row g-4">${html}</div>`;
    }

    async function refreshDashboardCards() {
        const response = await fetch("/Home/RecentRequestsData", { credentials: "same-origin" });
        if (!response.ok) return;

        const data = await response.json();
        const pending = document.getElementById("pending-proposals-count");
        const completed = document.getElementById("completed-payments-count");

        if (pending) pending.textContent = data.pendingProposals;
        if (completed) completed.textContent = data.completedPayments;
        renderRecentRequests(data.recentRequests);
    }

    window.addEventListener("cpm:notification", function (event) {
        const subject = event?.detail?.subject ?? "";
        if (subject === "Nova Proposta Recebida!") {
            refreshDashboardCards().catch(() => {});
        }
    });

    document.addEventListener("click", function (event) {
        const card = event.target.closest(".recent-request-card");
        if (!card) return;
        if (event.target.closest("a, button, input, textarea, select, label")) return;

        const url = card.dataset.url;
        if (url) {
            window.location.href = url;
        }
    });

    document.addEventListener("keydown", function (event) {
        if (event.key !== "Enter" && event.key !== " ") return;

        const card = event.target.closest(".recent-request-card");
        if (!card) return;
        if (event.target.closest("a, button, input, textarea, select, label")) return;

        event.preventDefault();
        const url = card.dataset.url;
        if (url) {
            window.location.href = url;
        }
    });
})();
