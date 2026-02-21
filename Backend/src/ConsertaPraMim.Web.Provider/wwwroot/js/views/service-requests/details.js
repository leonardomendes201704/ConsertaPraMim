(function () {
        const config = window.providerServiceRequestDetailsConfig || {};
        window.providerServiceRequestDetailsRuntime = {
            requestId: String(config.requestId || ""),
            currentUserId: String(config.currentUserId || ""),
            appointmentHistory: Array.isArray(config.appointmentHistory) ? config.appointmentHistory : [],
            receiptsDataUrl: String(config.receiptsDataUrl || ""),
            receiptBaseUrl: String(config.receiptBaseUrl || ""),
            hasExistingProposal: config.hasExistingProposal === true || String(config.hasExistingProposal || "").toLowerCase() === "true"
        };
    })();

(function () {
        const mapElement = document.getElementById("request-route-map");
        const summaryElement = document.getElementById("request-route-summary");
        if (!mapElement || typeof L === "undefined") return;

        const providerLat = Number(mapElement.dataset.providerLat);
        const providerLng = Number(mapElement.dataset.providerLng);
        const requestLat = Number(mapElement.dataset.requestLat);
        const requestLng = Number(mapElement.dataset.requestLng);
        if (![providerLat, providerLng, requestLat, requestLng].every(Number.isFinite)) {
            if (summaryElement) {
                summaryElement.textContent = "Coordenadas invalidas para montar a rota.";
            }
            return;
        }

        const map = L.map(mapElement, {
            zoomControl: true,
            scrollWheelZoom: true
        });

        L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
            maxZoom: 19,
            attribution: "&copy; OpenStreetMap contributors"
        }).addTo(map);

        const providerIcon = L.divIcon({
            className: "request-route-marker-wrapper",
            html: '<div class="request-route-marker provider"><i class="bi bi-wrench"></i></div>',
            iconSize: [34, 34],
            iconAnchor: [17, 17],
            popupAnchor: [0, -16]
        });

        const requestIcon = L.divIcon({
            className: "request-route-marker-wrapper",
            html: '<div class="request-route-marker client"><i class="bi bi-flag-fill"></i></div>',
            iconSize: [34, 34],
            iconAnchor: [17, 17],
            popupAnchor: [0, -16]
        });

        const providerMarker = L.marker([providerLat, providerLng], { icon: providerIcon }).addTo(map);
        providerMarker.bindPopup("Base do prestador");

        const requestMarker = L.marker([requestLat, requestLng], { icon: requestIcon }).addTo(map);
        requestMarker.bindPopup("Endereco do pedido");

        const baseBounds = L.latLngBounds([
            [providerLat, providerLng],
            [requestLat, requestLng]
        ]);
        map.fitBounds(baseBounds.pad(0.2));
        const routeEndpoint = String(mapElement.dataset.routeUrl || "").trim();

        function formatDuration(seconds) {
            const totalMinutes = Math.max(1, Math.round(seconds / 60));
            const hours = Math.floor(totalMinutes / 60);
            const minutes = totalMinutes % 60;
            if (hours <= 0) return `${totalMinutes} min`;
            return `${hours}h ${minutes}min`;
        }

        async function resolveDrivingRoute() {
            if (!routeEndpoint) {
                return null;
            }

            try {
                const params = new URLSearchParams({
                    providerLat: String(providerLat),
                    providerLng: String(providerLng),
                    requestLat: String(requestLat),
                    requestLng: String(requestLng)
                });
                const separator = routeEndpoint.includes("?") ? "&" : "?";
                const response = await fetch(`${routeEndpoint}${separator}${params.toString()}`, {
                    method: "GET",
                    credentials: "same-origin",
                    cache: "no-store",
                    headers: { "Accept": "application/json" }
                });

                if (!response.ok) {
                    return null;
                }

                const data = await response.json();
                if (!data?.success || !Array.isArray(data?.geometry) || data.geometry.length < 2) {
                    return null;
                }

                const coordinates = data.geometry
                    .map(point => [Number(point?.[0]), Number(point?.[1])])
                    .filter(point => Number.isFinite(point[0]) && Number.isFinite(point[1]));
                if (coordinates.length < 2) {
                    return null;
                }

                return {
                    distance: Number(data.distance || 0),
                    duration: Number(data.duration || 0),
                    geometry: coordinates
                };
            } catch {
                return null;
            }
        }

        resolveDrivingRoute()
            .then(route => {
                if (!route) {
                    throw new Error("Rota nao encontrada");
                }

                const polyline = L.polyline(route.geometry, {
                    color: "#2563eb",
                    weight: 5,
                    opacity: 0.85
                }).addTo(map);
                map.fitBounds(polyline.getBounds().pad(0.15));

                if (summaryElement) {
                    const distanceKm = (Number(route.distance || 0) / 1000).toLocaleString("pt-BR", {
                        minimumFractionDigits: 1,
                        maximumFractionDigits: 1
                    });
                    summaryElement.textContent = `Rota de carro: ${distanceKm} km · ${formatDuration(Number(route.duration || 0))}.`;
                }
            })
            .catch(() => {
                L.polyline([[providerLat, providerLng], [requestLat, requestLng]], {
                    color: "#64748b",
                    weight: 4,
                    opacity: 0.65,
                    dashArray: "8,8"
                }).addTo(map);

                if (summaryElement) {
                    summaryElement.textContent = "Nao foi possivel calcular a rota de carro agora. Exibindo trajeto direto.";
                }
            })
            .finally(() => {
                map.invalidateSize();
            });
    })();

(function () {
        const tabButtons = document.querySelectorAll("[data-details-tab]");
        const sections = document.querySelectorAll(".details-tab-section");
        if (!tabButtons.length || !sections.length) return;

        const runtime = window.providerServiceRequestDetailsRuntime || {};
        const storageKey = `cpm:provider:details-tab:${String(runtime.requestId || window.location.pathname || "").toLowerCase()}`;
        const availableTabs = new Set(Array.from(tabButtons).map(button => String(button.dataset.detailsTab || "").toLowerCase()).filter(Boolean));

        function normalizeTab(tabName) {
            const normalized = String(tabName || "").trim().toLowerCase();
            return availableTabs.has(normalized) ? normalized : null;
        }

        function readInitialTab() {
            const fromQuery = normalizeTab(new URLSearchParams(window.location.search).get("tab"));
            if (fromQuery) return fromQuery;

            const fromHash = normalizeTab(window.location.hash.replace(/^#/, ""));
            if (fromHash) return fromHash;

            try {
                return normalizeTab(window.sessionStorage.getItem(storageKey));
            } catch {
                return null;
            }
        }

        function persistTab(tabName) {
            try {
                window.sessionStorage.setItem(storageKey, tabName);
            } catch {
                // no-op
            }
        }

        function activateTab(tabName) {
            const normalizedTab = normalizeTab(tabName) || "geral";

            tabButtons.forEach(button => {
                const isActive = button.dataset.detailsTab === normalizedTab;
                button.classList.toggle("active", isActive);
                button.setAttribute("aria-selected", isActive ? "true" : "false");
            });

            sections.forEach(section => {
                const isActive = section.dataset.detailsSection === normalizedTab;
                section.classList.toggle("d-none", !isActive);
            });

            persistTab(normalizedTab);
        }

        tabButtons.forEach(button => {
            button.addEventListener("click", function (event) {
                event.preventDefault();
                activateTab(button.dataset.detailsTab || "geral");
            });
        });

        activateTab(readInitialTab() || "geral");
    })();

(function () {
        const runtime = window.providerServiceRequestDetailsRuntime || {};
        if (!runtime.hasExistingProposal) return;

        const button = document.getElementById("openChatBtn");
        const statusTitle = document.getElementById("proposal-status-title");
        const statusSubtitle = document.getElementById("proposal-status-subtitle");
        const statusBadge = document.getElementById("proposal-status-badge");
        const currentRequestId = String(runtime.requestId || "").toLowerCase();

        if (button) {
            button.addEventListener("click", function () {
                window.dispatchEvent(new CustomEvent("cpm:open-chat", {
                    detail: {
                        requestId: runtime.requestId,
                        providerId: runtime.currentUserId,
                        title: "Chat com Cliente"
                    }
                }));
            });
        }

        window.addEventListener("cpm:notification", function (event) {
            const subject = String(event?.detail?.subject || "");
            const actionUrl = String(event?.detail?.actionUrl || "").toLowerCase();
            const actionPath = actionUrl.split("?")[0];
            const expectedPath = `/servicerequests/details/${currentRequestId}`;
            const normalizedSubject = subject.toLowerCase();
            if (!actionPath.endsWith(expectedPath)) return;

            if (normalizedSubject.includes("agendamento") ||
                normalizedSubject.includes("operacional") ||
                normalizedSubject.includes("aditivo")) {
                window.location.reload();
                return;
            }

            if (subject !== "Sua Proposta foi Aceita!") return;

            if (statusTitle) statusTitle.textContent = "Proposta aceita pelo Cliente!";
            if (statusSubtitle) statusSubtitle.textContent = "Entre em contato para acertar os detalhes.";
            if (statusBadge) {
                statusBadge.className = "badge bg-success w-100 py-2 mb-3";
                statusBadge.textContent = "PROPOSTA ACEITA";
            }
        });
    })();

(function () {
        const display = document.getElementById("estimatedValueDisplay");
        const hidden = document.getElementById("estimatedValue");
        if (!display || !hidden) return;

        const formatter = new Intl.NumberFormat("pt-BR", {
            style: "currency",
            currency: "BRL",
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        });

        function onlyDigits(value) {
            return String(value || "").replace(/\D/g, "");
        }

        function applyMask(raw) {
            const digits = onlyDigits(raw);
            if (!digits) {
                display.value = "";
                hidden.value = "";
                return;
            }

            const amount = Number(digits) / 100;
            display.value = formatter.format(amount);
            hidden.value = amount.toFixed(2);
        }

        display.addEventListener("input", function () {
            applyMask(display.value);
        });

        display.addEventListener("blur", function () {
            if (!onlyDigits(display.value)) {
                display.value = "";
                hidden.value = "";
            }
        });

        const form = display.closest("form");
        if (form) {
            form.addEventListener("submit", function () {
                if (!onlyDigits(display.value)) {
                    hidden.value = "";
                }
            });
        }
    })();

(function () {
        function bindArrivalForm(form) {
            if (!form) return;

            form.addEventListener("submit", function (event) {
                if (form.dataset.geoResolved === "1") {
                    return;
                }

                event.preventDefault();

                const latitudeInput = form.querySelector('input[name="latitude"]');
                const longitudeInput = form.querySelector('input[name="longitude"]');
                const accuracyInput = form.querySelector('input[name="accuracyMeters"]');
                const manualReasonInput = form.querySelector('input[name="manualReason"]');

                const submitManualFallback = function () {
                    const reason = window.prompt("Nao foi possivel obter o GPS. Informe o motivo do check-in manual:");
                    if (!reason || !reason.trim()) {
                        alert("O motivo do check-in manual e obrigatorio.");
                        return;
                    }

                    if (latitudeInput) latitudeInput.value = "";
                    if (longitudeInput) longitudeInput.value = "";
                    if (accuracyInput) accuracyInput.value = "";
                    if (manualReasonInput) manualReasonInput.value = reason.trim();
                    form.dataset.geoResolved = "1";
                    form.submit();
                };

                if (!navigator.geolocation) {
                    submitManualFallback();
                    return;
                }

                navigator.geolocation.getCurrentPosition(
                    function (position) {
                        if (latitudeInput) latitudeInput.value = String(position.coords.latitude);
                        if (longitudeInput) longitudeInput.value = String(position.coords.longitude);
                        if (accuracyInput) accuracyInput.value = String(position.coords.accuracy || "");
                        if (manualReasonInput) manualReasonInput.value = "";
                        form.dataset.geoResolved = "1";
                        form.submit();
                    },
                    function () {
                        submitManualFallback();
                    },
                    {
                        enableHighAccuracy: true,
                        timeout: 8000,
                        maximumAge: 60000
                    });
            });
        }

        document.querySelectorAll(".js-arrival-form").forEach(bindArrivalForm);
    })();

(function () {
        const container = document.getElementById("provider-financial-policy-memo");
        if (!container) return;

        const runtime = window.providerServiceRequestDetailsRuntime || {};
        const history = Array.isArray(runtime.appointmentHistory) ? runtime.appointmentHistory : [];

        function escapeHtml(value) {
            return String(value || "")
                .replaceAll("&", "&amp;")
                .replaceAll("<", "&lt;")
                .replaceAll(">", "&gt;")
                .replaceAll('"', "&quot;")
                .replaceAll("'", "&#39;");
        }

        function parseHistoryMetadata(rawMetadata) {
            if (!rawMetadata) return null;
            if (typeof rawMetadata === "object") return rawMetadata;
            if (typeof rawMetadata !== "string") return null;

            try {
                return JSON.parse(rawMetadata);
            } catch {
                return null;
            }
        }

        function getMetadataValue(source, camelName, pascalName) {
            if (!source || typeof source !== "object") return undefined;
            if (Object.prototype.hasOwnProperty.call(source, camelName)) return source[camelName];
            if (Object.prototype.hasOwnProperty.call(source, pascalName)) return source[pascalName];
            return undefined;
        }

        function getFinancialPolicyEventLabel(eventType) {
            switch (eventType) {
                case "ClientCancellation":
                    return "Cancelamento pelo cliente";
                case "ProviderCancellation":
                    return "Cancelamento pelo prestador";
                case "ClientNoShow":
                    return "No-show do cliente";
                case "ProviderNoShow":
                    return "No-show do prestador";
                default:
                    return eventType || "-";
            }
        }

        function formatCurrency(value) {
            const numeric = Number(value);
            if (!Number.isFinite(numeric)) return "-";
            return numeric.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
        }

        function formatPercent(value) {
            const numeric = Number(value);
            if (!Number.isFinite(numeric)) return "-";
            return `${numeric.toLocaleString("pt-BR", { minimumFractionDigits: 0, maximumFractionDigits: 2 })}%`;
        }

        function formatDateTime(value) {
            if (!value) return "-";
            return new Date(value).toLocaleString("pt-BR", {
                day: "2-digit",
                month: "2-digit",
                year: "numeric",
                hour: "2-digit",
                minute: "2-digit"
            });
        }

        function renderFinancialMemo(item) {
            const metadata = parseHistoryMetadata(item?.metadata);
            const type = metadata ? getMetadataValue(metadata, "type", "Type") : null;
            if (!type || !String(type).toLowerCase().startsWith("financial_policy_")) {
                return "";
            }

            if (String(type).toLowerCase() === "financial_policy_calculation_failed") {
                const eventType = getMetadataValue(metadata, "eventType", "EventType");
                const errorCode = getMetadataValue(metadata, "errorCode", "ErrorCode") || "unknown_error";
                const errorMessage = getMetadataValue(metadata, "errorMessage", "ErrorMessage") || "Falha nao detalhada.";
                return `
                    <div class="border border-warning-subtle rounded-3 p-2 bg-warning-subtle bg-opacity-25">
                        <div class="d-flex justify-content-between align-items-center gap-2 mb-1">
                            <span class="badge bg-warning-subtle text-warning border border-warning-subtle">Falha de calculo</span>
                            <small class="text-muted">${escapeHtml(formatDateTime(item?.occurredAtUtc))}</small>
                        </div>
                        <div><span class="text-muted">Evento:</span> ${escapeHtml(getFinancialPolicyEventLabel(eventType))}</div>
                        <div><span class="text-muted">Erro:</span> ${escapeHtml(errorCode)} - ${escapeHtml(errorMessage)}</div>
                    </div>`;
            }

            const breakdown = getMetadataValue(metadata, "breakdown", "Breakdown");
            if (!breakdown || typeof breakdown !== "object") {
                return "";
            }

            const eventType = getMetadataValue(metadata, "eventType", "EventType");
            const serviceValue = getMetadataValue(metadata, "serviceValue", "ServiceValue");
            const ruleName = getMetadataValue(breakdown, "ruleName", "RuleName") || "-";
            const counterpartyActorLabel = getMetadataValue(breakdown, "counterpartyActorLabel", "CounterpartyActorLabel") || "-";
            const penaltyPercent = getMetadataValue(breakdown, "penaltyPercent", "PenaltyPercent");
            const penaltyAmount = getMetadataValue(breakdown, "penaltyAmount", "PenaltyAmount");
            const compensationPercent = getMetadataValue(breakdown, "counterpartyCompensationPercent", "CounterpartyCompensationPercent");
            const compensationAmount = getMetadataValue(breakdown, "counterpartyCompensationAmount", "CounterpartyCompensationAmount");
            const retainedPercent = getMetadataValue(breakdown, "platformRetainedPercent", "PlatformRetainedPercent");
            const retainedAmount = getMetadataValue(breakdown, "platformRetainedAmount", "PlatformRetainedAmount");
            const remainingAmount = getMetadataValue(breakdown, "remainingAmount", "RemainingAmount");

            const ledger = getMetadataValue(metadata, "ledger", "Ledger");
            const ledgerRequested = ledger ? getMetadataValue(ledger, "requested", "Requested") : null;
            const ledgerResult = ledger ? getMetadataValue(ledger, "result", "Result") : null;
            let ledgerText = `<span class="badge bg-light text-muted border border-light-subtle">Sem lancamento de ledger</span>`;
            if (ledgerRequested && typeof ledgerRequested === "object") {
                const entryType = getMetadataValue(ledgerRequested, "entryType", "EntryType") || "-";
                const amount = getMetadataValue(ledgerRequested, "amount", "Amount");
                const ledgerSuccess = ledgerResult && typeof ledgerResult === "object"
                    ? getMetadataValue(ledgerResult, "success", "Success")
                    : null;
                const ledgerErrorCode = ledgerResult && typeof ledgerResult === "object"
                    ? getMetadataValue(ledgerResult, "errorCode", "ErrorCode")
                    : null;
                const statusClass = ledgerSuccess === true
                    ? "bg-success-subtle text-success border border-success-subtle"
                    : ledgerSuccess === false
                        ? "bg-danger-subtle text-danger border border-danger-subtle"
                        : "bg-warning-subtle text-warning border border-warning-subtle";
                const statusLabel = ledgerSuccess === true
                    ? "Lancado"
                    : ledgerSuccess === false
                        ? `Falha (${escapeHtml(ledgerErrorCode || "erro")})`
                        : "Pendente";
                ledgerText = `
                    <div class="small d-flex flex-wrap align-items-center gap-2">
                        <span class="badge ${statusClass}">${statusLabel}</span>
                        <span><span class="text-muted">Tipo:</span> ${escapeHtml(entryType)}</span>
                        <span><span class="text-muted">Valor:</span> ${escapeHtml(formatCurrency(amount))}</span>
                    </div>`;
            }

            return `
                <div class="border border-info-subtle rounded-3 p-2 bg-white">
                    <div class="d-flex justify-content-between align-items-center gap-2 mb-1">
                        <div class="fw-semibold text-info-emphasis">${escapeHtml(getFinancialPolicyEventLabel(eventType))}</div>
                        <small class="text-muted">${escapeHtml(formatDateTime(item?.occurredAtUtc))}</small>
                    </div>
                    <div class="small mb-1"><span class="text-muted">Regra:</span> ${escapeHtml(ruleName)}</div>
                    <div class="small mb-1"><span class="text-muted">Valor base:</span> ${escapeHtml(formatCurrency(serviceValue))} | <span class="text-muted">Contraparte:</span> ${escapeHtml(counterpartyActorLabel)}</div>
                    <div class="small mb-1"><span class="text-muted">Multa:</span> ${escapeHtml(formatPercent(penaltyPercent))} (${escapeHtml(formatCurrency(penaltyAmount))}) | <span class="text-muted">Compensacao:</span> ${escapeHtml(formatPercent(compensationPercent))} (${escapeHtml(formatCurrency(compensationAmount))})</div>
                    <div class="small mb-1"><span class="text-muted">Retencao plataforma:</span> ${escapeHtml(formatPercent(retainedPercent))} (${escapeHtml(formatCurrency(retainedAmount))}) | <span class="text-muted">Saldo:</span> ${escapeHtml(formatCurrency(remainingAmount))}</div>
                    ${ledgerText}
                </div>`;
        }

        const entries = Array.isArray(history)
            ? history.map(renderFinancialMemo).filter(Boolean)
            : [];

        if (!entries.length) {
            container.innerHTML = `<div class="small text-muted">Nenhuma memoria de calculo financeiro registrada para este agendamento.</div>`;
            return;
        }

        container.innerHTML = `
            <div class="border border-info-subtle rounded-3 p-3 bg-info-subtle bg-opacity-10">
                <div class="fw-semibold mb-2">
                    <i class="fas fa-receipt text-primary me-2"></i>Memoria de calculo financeiro
                </div>
                <div class="d-grid gap-2">
                    ${entries.join("")}
                </div>
            </div>`;
    })();

(function () {
        const receiptsContainer = document.getElementById("provider-payment-receipts-section");
        const paymentStatusSummaryBadge = document.getElementById("provider-payment-status-summary-badge");
        if (!receiptsContainer) return;

        const runtime = window.providerServiceRequestDetailsRuntime || {};
        const requestId = String(runtime.requestId || "");
        const receiptsDataUrl = String(runtime.receiptsDataUrl || "");
        const receiptBaseUrl = String(runtime.receiptBaseUrl || "");

        function escapeHtml(value) {
            return String(value || "")
                .replaceAll("&", "&amp;")
                .replaceAll("<", "&lt;")
                .replaceAll(">", "&gt;")
                .replaceAll('"', "&quot;")
                .replaceAll("'", "&#39;");
        }

        function statusMeta(status) {
            switch (status) {
                case "Paid":
                    return { label: "Pago", badge: "bg-success-subtle text-success border border-success-subtle" };
                case "Pending":
                    return { label: "Pendente", badge: "bg-warning-subtle text-warning border border-warning-subtle" };
                case "Failed":
                    return { label: "Falhou", badge: "bg-danger-subtle text-danger border border-danger-subtle" };
                case "Refunded":
                    return { label: "Estornado", badge: "bg-secondary-subtle text-secondary border border-secondary-subtle" };
                default:
                    return { label: status || "-", badge: "bg-light text-muted border border-light-subtle" };
            }
        }

        function methodLabel(method) {
            if (method === "Pix") return "PIX";
            if (method === "Card") return "Cartao";
            return method || "-";
        }

        function updateSummary(receipts) {
            if (!paymentStatusSummaryBadge) return;

            if (!Array.isArray(receipts) || receipts.length === 0) {
                paymentStatusSummaryBadge.textContent = "Sem pagamento";
                paymentStatusSummaryBadge.className = "badge bg-light text-muted border border-light-subtle rounded-pill mt-1";
                return;
            }

            const sorted = receipts.slice().sort((a, b) => {
                const aDate = new Date(a.processedAtUtc || a.createdAtUtc || 0).getTime();
                const bDate = new Date(b.processedAtUtc || b.createdAtUtc || 0).getTime();
                return bDate - aDate;
            });
            const latest = sorted[0];
            const status = statusMeta(latest.status);
            paymentStatusSummaryBadge.textContent = `${status.label} - ${methodLabel(latest.method)}`;
            paymentStatusSummaryBadge.className = `badge ${status.badge} rounded-pill mt-1`;
        }

        function formatCurrency(value) {
            const numeric = Number(value || 0);
            return numeric.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
        }

        function formatDateTime(value) {
            if (!value) return "Aguardando processamento";
            return new Date(value).toLocaleString("pt-BR", {
                day: "2-digit",
                month: "2-digit",
                year: "numeric",
                hour: "2-digit",
                minute: "2-digit"
            });
        }

        function render(receipts) {
            if (!Array.isArray(receipts) || receipts.length === 0) {
                receiptsContainer.innerHTML = `<div class="text-muted">Nenhum comprovante disponivel ainda.</div>`;
                updateSummary([]);
                return;
            }

            receiptsContainer.innerHTML = receipts.map(receipt => {
                const status = statusMeta(receipt.status);
                const processedAt = formatDateTime(receipt.processedAtUtc);
                const url = `${receiptBaseUrl}?requestId=${encodeURIComponent(requestId)}&transactionId=${encodeURIComponent(receipt.transactionId)}`;

                return `
                    <div class="border rounded-3 p-3 mb-2">
                        <div class="d-flex justify-content-between align-items-start gap-2 mb-2">
                            <div>
                                <div class="fw-semibold">${escapeHtml(receipt.receiptNumber || "CPM")}</div>
                                <div class="text-muted small">${escapeHtml(methodLabel(receipt.method))} · ${escapeHtml(formatCurrency(receipt.amount))}</div>
                            </div>
                            <span class="badge ${status.badge}">${escapeHtml(status.label)}</span>
                        </div>
                        <div class="text-muted small mb-2">Processado: ${escapeHtml(processedAt)}</div>
                        <a href="${url}" target="_blank" rel="noopener noreferrer" class="btn btn-outline-primary btn-sm rounded-pill">
                            <i class="fas fa-file-invoice me-1"></i>Ver comprovante
                        </a>
                    </div>
                `;
            }).join("");
            updateSummary(receipts);
        }

        fetch(receiptsDataUrl, {
            credentials: "same-origin",
            headers: { "X-Requested-With": "XMLHttpRequest" }
        })
            .then(response => response.ok ? response.json() : Promise.reject())
            .then(data => render(data.receipts))
            .catch(() => {
                receiptsContainer.innerHTML = `<div class="text-danger">Nao foi possivel carregar os comprovantes.</div>`;
                updateSummary([]);
            });
    })();
