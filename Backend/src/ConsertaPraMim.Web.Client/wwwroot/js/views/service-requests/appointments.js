(function () {
        const config = window.serviceRequestAppointmentsConfig || {};
        const emptyEl = document.getElementById("appointments-empty");
        const contentEl = document.getElementById("appointments-content");
        const upcomingListEl = document.getElementById("upcoming-list");
        const pastListEl = document.getElementById("past-list");
        const upcomingCountEl = document.getElementById("upcoming-count");
        const pastCountEl = document.getElementById("past-count");

        if (!emptyEl || !contentEl || !upcomingListEl || !pastListEl || !upcomingCountEl || !pastCountEl) {
            return;
        }

        const terminalStatuses = new Set([
            "RejectedByProvider",
            "ExpiredWithoutProviderAction",
            "CancelledByClient",
            "CancelledByProvider",
            "Completed"
        ]);

        let appointments = Array.isArray(config.initialAppointments) ? config.initialAppointments : [];
        let refreshInFlight = false;

        function formatDateTime(value) {
            if (!value) return "-";
            const date = new Date(value);
            return date.toLocaleString("pt-BR", {
                day: "2-digit",
                month: "2-digit",
                year: "numeric",
                hour: "2-digit",
                minute: "2-digit"
            });
        }

        function getStatusMeta(status) {
            switch (status) {
                case "PendingProviderConfirmation":
                    return { label: "Aguardando confirmacao", className: "bg-warning-subtle text-warning border border-warning-subtle" };
                case "Confirmed":
                    return { label: "Confirmado", className: "bg-success-subtle text-success border border-success-subtle" };
                case "RescheduleRequestedByClient":
                    return { label: "Reagendamento solicitado por voce", className: "bg-info-subtle text-info border border-info-subtle" };
                case "RescheduleRequestedByProvider":
                    return { label: "Prestador solicitou reagendamento", className: "bg-info-subtle text-info border border-info-subtle" };
                case "RescheduleConfirmed":
                    return { label: "Reagendamento confirmado", className: "bg-success-subtle text-success border border-success-subtle" };
                case "RejectedByProvider":
                    return { label: "Recusado pelo prestador", className: "bg-danger-subtle text-danger border border-danger-subtle" };
                case "ExpiredWithoutProviderAction":
                    return { label: "Expirado", className: "bg-secondary-subtle text-secondary border border-secondary-subtle" };
                case "CancelledByClient":
                    return { label: "Cancelado por voce", className: "bg-secondary-subtle text-secondary border border-secondary-subtle" };
                case "CancelledByProvider":
                    return { label: "Cancelado pelo prestador", className: "bg-secondary-subtle text-secondary border border-secondary-subtle" };
                case "Completed":
                    return { label: "Concluido", className: "bg-primary-subtle text-primary border border-primary-subtle" };
                default:
                    return { label: status || "-", className: "bg-light text-muted border border-light-subtle" };
            }
        }

        function getNoShowRiskMeta(level) {
            const normalized = String(level || "").toLowerCase();
            switch (normalized) {
                case "high":
                    return { label: "Risco alto", className: "bg-danger-subtle text-danger border border-danger-subtle", iconClass: "fa-triangle-exclamation" };
                case "medium":
                    return { label: "Risco medio", className: "bg-warning-subtle text-warning border border-warning-subtle", iconClass: "fa-circle-exclamation" };
                case "low":
                    return { label: "Risco baixo", className: "bg-success-subtle text-success border border-success-subtle", iconClass: "fa-shield-alt" };
                default:
                    return null;
            }
        }

        function isPast(item) {
            const end = new Date(item.windowEndUtc).getTime();
            return terminalStatuses.has(item.status) || end < Date.now();
        }

        function renderCard(item, isHistory) {
            const status = getStatusMeta(item.status);
            const riskMeta = getNoShowRiskMeta(item.noShowRiskLevel);
            const needsAction = item.status === "RescheduleRequestedByProvider";
            return `
                <div class="border rounded-4 p-3 bg-white">
                    <div class="d-flex justify-content-between gap-2 align-items-start mb-2">
                        <div>
                            <div class="fw-bold">${item.category || "Categoria"}</div>
                            <div class="text-muted small">${item.description || "Pedido"}</div>
                        </div>
                        <span class="badge ${status.className} rounded-pill px-3 text-wrap">${status.label}</span>
                    </div>
                    <div class="small text-muted mb-1">
                        <i class="fas fa-user-cog me-1"></i>Prestador: ${item.providerName || "Prestador"}
                    </div>
                    <div class="small text-muted mb-1">
                        <i class="fas fa-clock me-1"></i>${formatDateTime(item.windowStartUtc)} - ${formatDateTime(item.windowEndUtc)}
                    </div>
                    <div class="small text-muted mb-3">
                        <i class="fas fa-map-marker-alt me-1"></i>${item.street || ""} ${item.city ? `, ${item.city}` : ""}
                    </div>
                    <div class="d-flex justify-content-between align-items-center gap-2">
                        <div class="d-flex flex-wrap gap-2">
                            ${needsAction && !isHistory ? "<span class=\"badge bg-danger-subtle text-danger border border-danger-subtle\">Acao necessaria</span>" : ""}
                            ${riskMeta
                                ? `<span class="badge ${riskMeta.className}">
                                        <i class="fas ${riskMeta.iconClass} me-1"></i>${riskMeta.label}${item.noShowRiskScore !== null && item.noShowRiskScore !== undefined ? ` (${item.noShowRiskScore})` : ""}
                                   </span>`
                                : ""}
                        </div>
                        <a href="/ServiceRequests/Details/${item.serviceRequestId}" class="btn btn-sm btn-outline-primary rounded-pill px-3">Abrir pedido</a>
                    </div>
                    ${item.noShowRiskCalculatedAtUtc
                        ? `<div class="small text-muted mt-2">Ultima analise de risco: ${formatDateTime(item.noShowRiskCalculatedAtUtc)}</div>`
                        : ""}
                </div>`;
        }

        function renderEmptyState(targetEl, message) {
            targetEl.innerHTML = `<div class="text-muted small border rounded-4 p-3 bg-light">${message}</div>`;
        }

        function renderAppointments() {
            const upcoming = (appointments || [])
                .filter(item => !isPast(item))
                .sort((a, b) => new Date(a.windowStartUtc) - new Date(b.windowStartUtc));

            const past = (appointments || [])
                .filter(isPast)
                .sort((a, b) => new Date(b.windowStartUtc) - new Date(a.windowStartUtc));

            if (upcomingCountEl) upcomingCountEl.textContent = String(upcoming.length);
            if (pastCountEl) pastCountEl.textContent = String(past.length);

            if (!upcoming.length) {
                renderEmptyState(upcomingListEl, "Nenhum agendamento futuro.");
            } else {
                upcomingListEl.innerHTML = upcoming.map(item => renderCard(item, false)).join("");
            }

            if (!past.length) {
                renderEmptyState(pastListEl, "Nenhum item no historico.");
            } else {
                pastListEl.innerHTML = past.map(item => renderCard(item, true)).join("");
            }

            const hasAny = (appointments || []).length > 0;
            emptyEl.classList.toggle("d-none", hasAny);
            contentEl.classList.toggle("d-none", !hasAny);
        }

        async function refreshAppointments() {
            if (refreshInFlight) return;
            refreshInFlight = true;
            try {
                const response = await fetch("/ServiceRequests/AppointmentsData", {
                    credentials: "same-origin",
                    headers: { "X-Requested-With": "XMLHttpRequest" }
                });

                if (!response.ok) return;
                const data = await response.json();
                appointments = Array.isArray(data.appointments) ? data.appointments : [];
                renderAppointments();
            } catch (error) {
                console.error(error);
            } finally {
                refreshInFlight = false;
            }
        }

        function shouldRefreshBySubject(subject) {
            const normalized = String(subject || "").toLowerCase();
            return normalized.includes("agendamento") ||
                normalized.includes("reagendamento") ||
                normalized.includes("lembrete");
        }

        window.addEventListener("cpm:notification", function (event) {
            const subject = event?.detail?.subject || "";
            if (shouldRefreshBySubject(subject)) {
                refreshAppointments().catch(() => {});
            }
        });

        setInterval(function () {
            refreshAppointments().catch(() => {});
        }, 45000);

        renderAppointments();
    })();
