(function () {
    const config = window.adminCoverageMapConfig || {};
    const snapshotUrl = config.snapshotUrl || "";
    const mapElement = document.getElementById(config.mapElementId || "coverage-map");
    const stateElement = document.getElementById(config.statusElementId || "coverage-map-state");
    const refreshButton = document.getElementById(config.refreshButtonId || "coverage-map-refresh-btn");
    const citySelect = document.getElementById(config.citySelectId || "coverage-map-city-select");
    const autoRefreshToggle = document.getElementById(config.autoRefreshToggleId || "coverage-map-auto-refresh-toggle");
    const providerCountElement = document.getElementById(config.providerCountElementId || "coverage-map-provider-count");
    const requestCountElement = document.getElementById(config.requestCountElementId || "coverage-map-request-count");
    const attendedNeighborhoodRowsElement = document.getElementById(config.attendedNeighborhoodRowsId || "coverage-map-attended-neighborhood-rows");
    const unattendedNeighborhoodRowsElement = document.getElementById(config.unattendedNeighborhoodRowsId || "coverage-map-unattended-neighborhood-rows");
    const attendedNeighborhoodCountElement = document.getElementById(config.attendedNeighborhoodCountId || "coverage-map-attended-neighborhood-count");
    const unattendedNeighborhoodCountElement = document.getElementById(config.unattendedNeighborhoodCountId || "coverage-map-unattended-neighborhood-count");
    const pollIntervalMs = 60000;
    const autoRefreshStorageKey = "adminCoverageMapAutoRefreshEnabled";

    if (!snapshotUrl || !mapElement || !stateElement || typeof window.L === "undefined") {
        return;
    }

    let map = null;
    let providerLayer = null;
    let requestLayer = null;
    let radiusLayer = null;
    let providerPinIcon = null;
    let requestPinIcon = null;
    let requestInFlight = false;
    let pollHandle = null;
    let currentCityFilter = null;
    let isAutoRefreshEnabled = true;

    function escapeHtml(value) {
        return String(value ?? "")
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll("\"", "&quot;")
            .replaceAll("'", "&#39;");
    }

    function formatNumber(value) {
        return new Intl.NumberFormat("pt-BR").format(Number(value ?? 0));
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

    function formatPercent(value) {
        return new Intl.NumberFormat("pt-BR", {
            minimumFractionDigits: 1,
            maximumFractionDigits: 1
        }).format(Number(value ?? 0));
    }

    function resolveOperationalStatusLabel(status) {
        const normalized = String(status ?? "").toLowerCase();
        if (normalized === "online") return "Online";
        if (normalized === "ematendimento") return "Em atendimento";
        if (normalized === "ausente") return "Ausente";
        return status || "Nao informado";
    }

    function resolveRequestStatusLabel(status) {
        const normalized = String(status ?? "").toLowerCase();
        if (normalized === "created") return "Criado";
        if (normalized === "matching") return "Em matching";
        if (normalized === "scheduled") return "Agendado";
        if (normalized === "inprogress") return "Em andamento";
        if (normalized === "completed") return "Concluido";
        if (normalized === "validated") return "Validado";
        if (normalized === "pendingclientcompletionacceptance") return "Aguardando aceite do cliente";
        if (normalized === "canceled") return "Cancelado";
        return status || "Nao informado";
    }

    function setState(message, tone) {
        const normalizedTone = tone || "info";
        stateElement.className = `alert alert-${normalizedTone} py-2 px-3 mb-3`;
        stateElement.textContent = message;
    }

    function normalizeCityFilter(value) {
        const normalized = String(value ?? "").trim();
        return normalized.length > 0 ? normalized : null;
    }

    function normalizeNeighborhoodLabel(value) {
        const normalized = String(value ?? "").trim();
        return normalized.length > 0 ? normalized : "Bairro nao informado";
    }

    function syncCityToQueryString() {
        try {
            const url = new URL(window.location.href);
            if (currentCityFilter) {
                url.searchParams.set("city", currentCityFilter);
            } else {
                url.searchParams.delete("city");
            }
            window.history.replaceState({}, "", url.toString());
        } catch {
            // no-op
        }
    }

    function buildSnapshotUrl() {
        const url = new URL(snapshotUrl, window.location.origin);
        if (currentCityFilter) {
            url.searchParams.set("city", currentCityFilter);
        }
        return url.toString();
    }

    function buildCoordinateKey(latitude, longitude) {
        return `${latitude.toFixed(6)}|${longitude.toFixed(6)}`;
    }

    function toRadians(value) {
        return (Number(value) * Math.PI) / 180;
    }

    function calculateDistanceKm(fromLat, fromLng, toLat, toLng) {
        const earthRadiusKm = 6371;
        const deltaLat = toRadians(toLat - fromLat);
        const deltaLng = toRadians(toLng - fromLng);
        const a =
            Math.sin(deltaLat / 2) * Math.sin(deltaLat / 2) +
            Math.cos(toRadians(fromLat)) *
                Math.cos(toRadians(toLat)) *
                Math.sin(deltaLng / 2) *
                Math.sin(deltaLng / 2);

        const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
        return earthRadiusKm * c;
    }

    function buildCityListFromProviders(providers) {
        return providers
            .map(provider => normalizeCityFilter(provider?.city))
            .filter(cityName => !!cityName)
            .filter((cityName, index, list) => list.findIndex(item => item.localeCompare(cityName, "pt-BR", { sensitivity: "accent" }) === 0) === index)
            .sort((left, right) => left.localeCompare(right, "pt-BR", { sensitivity: "accent" }));
    }

    function setCityFilterOptions(data) {
        if (!citySelect) {
            return;
        }

        const providers = Array.isArray(data?.providers) ? data.providers : [];
        const fromPayload = Array.isArray(data?.availableProviderCities)
            ? data.availableProviderCities.map(normalizeCityFilter).filter(cityName => !!cityName)
            : [];
        const cityOptions = (fromPayload.length > 0 ? fromPayload : buildCityListFromProviders(providers))
            .filter((cityName, index, list) => list.findIndex(item => item.localeCompare(cityName, "pt-BR", { sensitivity: "accent" }) === 0) === index)
            .sort((left, right) => left.localeCompare(right, "pt-BR", { sensitivity: "accent" }));

        const previousValue = currentCityFilter || "";
        citySelect.innerHTML = "";

        const defaultOption = document.createElement("option");
        defaultOption.value = "";
        defaultOption.textContent = "Todas as cidades atendidas";
        citySelect.appendChild(defaultOption);

        cityOptions.forEach(cityName => {
            const option = document.createElement("option");
            option.value = cityName;
            option.textContent = cityName;
            citySelect.appendChild(option);
        });

        if (previousValue && cityOptions.some(cityName => cityName.localeCompare(previousValue, "pt-BR", { sensitivity: "accent" }) === 0)) {
            citySelect.value = previousValue;
            return;
        }

        if (previousValue) {
            currentCityFilter = null;
            syncCityToQueryString();
        }

        citySelect.value = "";
    }

    function loadAutoRefreshPreference() {
        try {
            const raw = window.localStorage.getItem(autoRefreshStorageKey);
            if (raw === "0") {
                return false;
            }
            if (raw === "1") {
                return true;
            }
        } catch {
            // no-op
        }

        return true;
    }

    function saveAutoRefreshPreference(enabled) {
        try {
            window.localStorage.setItem(autoRefreshStorageKey, enabled ? "1" : "0");
        } catch {
            // no-op
        }
    }

    function createPinIcon(type) {
        const safeType = type === "request" ? "request" : "provider";
        return window.L.divIcon({
            className: `coverage-map-pin ${safeType}`,
            html: '<i class="fas fa-map-marker-alt" aria-hidden="true"></i>',
            iconSize: [28, 40],
            iconAnchor: [14, 38],
            popupAnchor: [0, -34]
        });
    }

    function isRequestCoveredByAnyProvider(request, providers) {
        const requestLat = Number(request?.latitude);
        const requestLng = Number(request?.longitude);
        if (!Number.isFinite(requestLat) || !Number.isFinite(requestLng)) {
            return false;
        }

        return providers.some(provider => {
            const providerLat = Number(provider?.latitude);
            const providerLng = Number(provider?.longitude);
            const radiusKm = Math.max(0, Number(provider?.radiusKm ?? 0));

            if (!Number.isFinite(providerLat) || !Number.isFinite(providerLng) || radiusKm <= 0) {
                return false;
            }

            return calculateDistanceKm(providerLat, providerLng, requestLat, requestLng) <= radiusKm;
        });
    }

    function buildNeighborhoodCoverageSummary(providers, requests) {
        const groups = new Map();

        requests.forEach(request => {
            const city = normalizeCityFilter(request?.addressCity) || "Cidade nao informada";
            const neighborhood = normalizeNeighborhoodLabel(request?.addressNeighborhood);
            const key = `${city.toLocaleLowerCase("pt-BR")}||${neighborhood.toLocaleLowerCase("pt-BR")}`;
            const covered = isRequestCoveredByAnyProvider(request, providers);

            if (!groups.has(key)) {
                groups.set(key, {
                    city,
                    neighborhood,
                    totalRequests: 0,
                    coveredRequests: 0,
                    uncoveredRequests: 0
                });
            }

            const group = groups.get(key);
            group.totalRequests += 1;
            if (covered) {
                group.coveredRequests += 1;
            } else {
                group.uncoveredRequests += 1;
            }
        });

        const rows = Array.from(groups.values())
            .map(group => {
                const coverageRatePercent = group.totalRequests > 0
                    ? (group.coveredRequests / group.totalRequests) * 100
                    : 0;

                return {
                    ...group,
                    coverageRatePercent
                };
            });

        return {
            attended: rows
                .filter(row => row.uncoveredRequests === 0)
                .sort((left, right) =>
                    right.totalRequests - left.totalRequests ||
                    left.city.localeCompare(right.city, "pt-BR", { sensitivity: "accent" }) ||
                    left.neighborhood.localeCompare(right.neighborhood, "pt-BR", { sensitivity: "accent" })),
            unattended: rows
                .filter(row => row.uncoveredRequests > 0)
                .sort((left, right) =>
                    right.uncoveredRequests - left.uncoveredRequests ||
                    right.totalRequests - left.totalRequests ||
                    left.city.localeCompare(right.city, "pt-BR", { sensitivity: "accent" }) ||
                    left.neighborhood.localeCompare(right.neighborhood, "pt-BR", { sensitivity: "accent" }))
        };
    }

    function renderNeighborhoodRows(element, rows, emptyMessage) {
        if (!element) {
            return;
        }

        if (!Array.isArray(rows) || rows.length === 0) {
            element.innerHTML = `<tr><td colspan="6" class="text-muted py-3">${escapeHtml(emptyMessage)}</td></tr>`;
            return;
        }

        element.innerHTML = rows
            .map(row => {
                const coverageLabel = row.uncoveredRequests === 0
                    ? `${formatPercent(row.coverageRatePercent)}%`
                    : row.coveredRequests === 0
                        ? `${formatPercent(row.coverageRatePercent)}% (sem cobertura)`
                        : `${formatPercent(row.coverageRatePercent)}% (parcial)`;

                const coverageToneClass = row.uncoveredRequests === 0
                    ? "text-success"
                    : row.coveredRequests === 0
                        ? "text-danger"
                        : "text-warning";

                return `<tr>
                    <td class="fw-semibold">${escapeHtml(row.neighborhood)}</td>
                    <td class="text-muted">${escapeHtml(row.city)}</td>
                    <td class="text-end">${formatNumber(row.totalRequests)}</td>
                    <td class="text-end">${formatNumber(row.coveredRequests)}</td>
                    <td class="text-end">${formatNumber(row.uncoveredRequests)}</td>
                    <td class="text-end ${coverageToneClass}"><span class="coverage-map-neighborhood-tone">${escapeHtml(coverageLabel)}</span></td>
                </tr>`;
            })
            .join("");
    }

    function ensureMap() {
        if (!map) {
            map = window.L.map(mapElement, { zoomControl: true }).setView([-23.5505, -46.6333], 10);

            window.L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
                maxZoom: 19,
                attribution: "&copy; OpenStreetMap contributors"
            }).addTo(map);

            map.createPane("coverageRadiusPane");
            map.getPane("coverageRadiusPane").style.zIndex = "350";
            map.getPane("coverageRadiusPane").style.pointerEvents = "none";

            radiusLayer = window.L.layerGroup().addTo(map);
            providerLayer = window.L.layerGroup().addTo(map);
            requestLayer = window.L.layerGroup().addTo(map);

            providerPinIcon = createPinIcon("provider");
            requestPinIcon = createPinIcon("request");
        }
    }

    function renderMap(data) {
        ensureMap();

        const providers = Array.isArray(data?.providers) ? data.providers : [];
        const requests = Array.isArray(data?.requests) ? data.requests : [];
        const neighborhoodSummary = buildNeighborhoodCoverageSummary(providers, requests);
        const bounds = [];
        const providerCoordinateKeys = new Set();

        setCityFilterOptions(data);

        if (providerCountElement) {
            providerCountElement.textContent = formatNumber(providers.length);
        }

        if (requestCountElement) {
            requestCountElement.textContent = formatNumber(requests.length);
        }

        if (attendedNeighborhoodCountElement) {
            attendedNeighborhoodCountElement.textContent = formatNumber(neighborhoodSummary.attended.length);
        }

        if (unattendedNeighborhoodCountElement) {
            unattendedNeighborhoodCountElement.textContent = formatNumber(neighborhoodSummary.unattended.length);
        }

        renderNeighborhoodRows(
            attendedNeighborhoodRowsElement,
            neighborhoodSummary.attended,
            "Nenhum bairro com pedidos 100% cobertos no recorte atual."
        );
        renderNeighborhoodRows(
            unattendedNeighborhoodRowsElement,
            neighborhoodSummary.unattended,
            "Nenhum bairro com gap de cobertura no recorte atual."
        );

        radiusLayer.clearLayers();
        providerLayer.clearLayers();
        requestLayer.clearLayers();

        providers.forEach(provider => {
            const lat = Number(provider.latitude);
            const lng = Number(provider.longitude);
            const radiusKm = Math.max(0, Number(provider.radiusKm ?? 0));
            if (!Number.isFinite(lat) || !Number.isFinite(lng)) {
                return;
            }

            const marker = window.L.marker([lat, lng], {
                icon: providerPinIcon,
                keyboard: false,
                zIndexOffset: 200
            });
            const cityLine = provider.city
                ? `<div class="text-muted">${escapeHtml(provider.city)}</div>`
                : "";

            marker.bindPopup(
                `<div class="small">
                    <div class="fw-semibold">${escapeHtml(provider.providerName)}</div>
                    ${cityLine}
                    <div>Status: ${escapeHtml(resolveOperationalStatusLabel(provider.operationalStatus))}</div>
                    <div>Raio: ${Number(radiusKm).toLocaleString("pt-BR", { minimumFractionDigits: 1, maximumFractionDigits: 1 })} km</div>
                </div>`
            );
            marker.addTo(providerLayer);

            if (radiusKm > 0) {
                const radiusCircle = window.L.circle([lat, lng], {
                    radius: radiusKm * 1000,
                    pane: "coverageRadiusPane",
                    color: "#2563eb",
                    weight: 1.5,
                    fillColor: "#60a5fa",
                    fillOpacity: 0.03
                });

                radiusCircle.addTo(radiusLayer);
                if (typeof radiusCircle.bringToBack === "function") {
                    radiusCircle.bringToBack();
                }
            }

            providerCoordinateKeys.add(buildCoordinateKey(lat, lng));
            bounds.push([lat, lng]);
        });

        requests.forEach(request => {
            const lat = Number(request.latitude);
            const lng = Number(request.longitude);
            if (!Number.isFinite(lat) || !Number.isFinite(lng)) {
                return;
            }

            const requestCoordinateKey = buildCoordinateKey(lat, lng);
            const isOverProviderPoint = providerCoordinateKeys.has(requestCoordinateKey);
            const markerLat = isOverProviderPoint ? lat + 0.00028 : lat;
            const markerLng = isOverProviderPoint ? lng + 0.00028 : lng;

            const marker = window.L.marker([markerLat, markerLng], {
                icon: requestPinIcon,
                keyboard: false,
                zIndexOffset: 300
            });

            marker.bindPopup(
                `<div class="small">
                    <div class="fw-semibold">${escapeHtml(request.category)}</div>
                    <div>Status: ${escapeHtml(resolveRequestStatusLabel(request.status))}</div>
                    <div class="text-muted">${escapeHtml(request.addressStreet)}, ${escapeHtml(request.addressCity)}</div>
                    <div class="text-muted">${formatDateTime(request.createdAtUtc)}</div>
                </div>`
            );
            marker.addTo(requestLayer);
            bounds.push([markerLat, markerLng]);
        });

        if (bounds.length > 0) {
            map.fitBounds(window.L.latLngBounds(bounds).pad(0.16));
        } else {
            map.setView([-23.5505, -46.6333], 10);
        }

        map.invalidateSize();
        const cityLabel = currentCityFilter ? ` | Cidade: ${currentCityFilter}` : "";
        setState(
            `Prestadores: ${formatNumber(providers.length)} | Pedidos: ${formatNumber(requests.length)} | Bairros atendidos: ${formatNumber(neighborhoodSummary.attended.length)} | Bairros com gap: ${formatNumber(neighborhoodSummary.unattended.length)}${cityLabel} | Atualizado em ${formatDateTime(data?.generatedAtUtc)}`,
            "success"
        );
    }

    async function fetchMap(options) {
        if (requestInFlight) {
            return;
        }

        const showLoading = options?.showLoading ?? true;
        requestInFlight = true;
        if (showLoading) {
            setState("Atualizando mapa operacional...", "info");
        }

        try {
            const response = await fetch(buildSnapshotUrl(), {
                method: "GET",
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });

            const payload = await response.json().catch(() => null);
            if (!response.ok || !payload || payload.success !== true || !payload.data) {
                const fallbackMessage = `Falha ao carregar mapa operacional (${response.status}).`;
                setState(payload?.errorMessage || fallbackMessage, "warning");
                return;
            }

            renderMap(payload.data);
        } catch (error) {
            setState("Nao foi possivel carregar o mapa operacional.", "warning");
            console.error(error);
        } finally {
            requestInFlight = false;
        }
    }

    function startPolling() {
        if (!isAutoRefreshEnabled) {
            return;
        }

        stopPolling();
        pollHandle = setInterval(function () {
            if (!document.hidden) {
                fetchMap({ showLoading: false });
            }
        }, pollIntervalMs);
    }

    function stopPolling() {
        if (!pollHandle) {
            return;
        }

        clearInterval(pollHandle);
        pollHandle = null;
    }

    function setAutoRefreshEnabled(enabled, options) {
        const normalized = enabled !== false;
        const settings = options || {};

        isAutoRefreshEnabled = normalized;
        saveAutoRefreshPreference(normalized);

        if (settings.syncToggle !== false && autoRefreshToggle) {
            autoRefreshToggle.checked = normalized;
        }

        if (normalized) {
            startPolling();
            if (settings.fetchNow && !document.hidden) {
                fetchMap({ showLoading: false });
            }
            return;
        }

        stopPolling();
    }

    if (refreshButton) {
        refreshButton.addEventListener("click", function () {
            fetchMap({ showLoading: true });
        });
    }

    if (citySelect) {
        citySelect.addEventListener("change", function () {
            currentCityFilter = normalizeCityFilter(citySelect.value);
            syncCityToQueryString();
            fetchMap({ showLoading: true });
        });
    }

    if (autoRefreshToggle) {
        autoRefreshToggle.addEventListener("change", function () {
            setAutoRefreshEnabled(autoRefreshToggle.checked, {
                syncToggle: false,
                fetchNow: autoRefreshToggle.checked
            });
        });
    }

    try {
        const initialCityFilter = normalizeCityFilter(new URL(window.location.href).searchParams.get("city"));
        currentCityFilter = initialCityFilter;
        if (citySelect && initialCityFilter) {
            citySelect.value = initialCityFilter;
        }
    } catch {
        currentCityFilter = normalizeCityFilter(citySelect?.value);
    }

    document.addEventListener("visibilitychange", function () {
        if (!document.hidden && isAutoRefreshEnabled) {
            fetchMap({ showLoading: false });
        }
    });

    setAutoRefreshEnabled(loadAutoRefreshPreference(), { syncToggle: true, fetchNow: false });
    fetchMap({ showLoading: true });
})();
