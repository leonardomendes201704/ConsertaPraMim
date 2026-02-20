(function () {
    function badgeClass(status) {
        if (status === "Created") return "bg-info-subtle text-info border-info-subtle";
        if (status === "Matching") return "bg-primary-subtle text-primary border-primary-subtle";
        if (status === "Scheduled") return "bg-warning-subtle text-warning border-warning-subtle";
        if (status === "PendingClientCompletionAcceptance") return "bg-info-subtle text-info border-info-subtle";
        if (status === "Completed") return "bg-success-subtle text-success border-success-subtle";
        return "bg-light text-muted border-light-subtle";
    }

    function statusLabel(status) {
        if (status === "PendingClientCompletionAcceptance") return "Aguardando aceite de conclusao";
        return status;
    }

    function renderRequests(items) {
        const container = document.getElementById("service-requests-container");
        if (!container) return;

        if (!items || !items.length) {
            container.innerHTML = `
                <div class="card border-0 shadow-sm p-5 text-center bg-white rounded-4">
                    <div class="mb-3 text-muted">
                        <i class="fas fa-folder-open fa-4x opacity-25"></i>
                    </div>
                    <h5>Voce ainda nao tem pedidos</h5>
                    <p class="text-muted">Crie seu primeiro pedido para receber orcamentos.</p>
                    <div class="mt-3">
                        <a href="/ServiceRequests/Create" class="btn btn-primary rounded-pill px-4">Solicitar Agora</a>
                    </div>
                </div>`;
            return;
        }

        const html = items.map(item => `
            <div class="col-12">
                <div class="card border-0 shadow-sm transition-hover rounded-4">
                    <div class="card-body p-4">
                        <div class="row align-items-center">
                            <div class="col-md-7">
                                <div class="d-flex align-items-center mb-3">
                                    <span class="badge bg-primary-subtle text-primary rounded-pill px-3 me-3">${item.category}</span>
                                    <small class="text-muted"><i class="far fa-clock me-1"></i> Postado em ${item.createdAt}</small>
                                </div>
                                <h5 class="fw-bold mb-1">${item.description}</h5>
                                <p class="text-muted small mb-0"><i class="fas fa-map-marker-alt me-1"></i> ${item.street}, ${item.city}</p>
                            </div>
                            <div class="col-md-3 text-md-center py-3 py-md-0">
                                <span class="badge ${badgeClass(item.status)} border rounded-pill px-4 py-2">${statusLabel(item.status)}</span>
                            </div>
                            <div class="col-md-2 text-end">
                                <a href="/ServiceRequests/Details/${item.id}" class="btn btn-outline-primary rounded-pill px-4 btn-sm fw-bold">Detalhes</a>
                            </div>
                        </div>
                    </div>
                </div>
            </div>`).join("");

        container.innerHTML = `<div class="row g-4">${html}</div>`;
    }

    async function refreshServiceRequests() {
        const response = await fetch("/ServiceRequests/ListData", { credentials: "same-origin" });
        if (!response.ok) return;

        const data = await response.json();
        renderRequests(data.requests);
    }

    window.addEventListener("cpm:notification", function (event) {
        const subject = event?.detail?.subject ?? "";
        if (subject === "Nova Proposta Recebida!") {
            refreshServiceRequests().catch(() => {});
        }
    });
})();
