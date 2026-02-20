(function () {
        const config = window.providerHomeDashboardConfig || {};
        const currentProviderId = String(config.currentProviderId || "").toLowerCase();
        const initialCoverageMapRaw = config.initialCoverageMapRaw || {};
        const providerAvatarUrl = config.providerAvatarUrl || "";
        const statusBadgeEl = document.getElementById("provider-operational-badge");
        const statusTextEl = document.getElementById("provider-operational-text");
        const coverageMapEl = document.getElementById("provider-coverage-map");
        const coverageEmptyEl = document.getElementById("provider-coverage-empty");
        const coverageRadiusEl = document.getElementById("provider-coverage-radius-km");
        const coveragePinsCountEl = document.getElementById("provider-coverage-pins-count");
        const coveragePinsLoadedEl = document.getElementById("provider-coverage-pins-loaded");
        const coveragePinsTotalEl = document.getElementById("provider-coverage-pins-total");
        const coverageLoadMoreEl = document.getElementById("provider-coverage-load-more");
        const coverageCategoryFilterEl = document.getElementById("coverage-filter-category");
        const coverageDistanceFilterEl = document.getElementById("coverage-filter-distance");
        const defaultPinPageSize = 120;
        const drivingRouteUrl = config.drivingRouteUrl || "";
        const recentMatchesLimit = Number(config.recentMatchesLimit) > 0 ? Number(config.recentMatchesLimit) : 15;
        const recentMatchesDistanceCache = new Map();

        let coverageMapState = null;
        let leafletMap = null;
        let coverageMarkersLayer = null;
        let coverageRadiusLayer = null;
        let coverageProviderLayer = null;
        const coverageCategoryCatalog = new Map();
        const coverageMarkersByRequestId = new Map();
        let refreshDebounceHandle = null;
        let refreshAbortController = null;

        function escapeHtml(value) {
            return String(value || "")
                .replaceAll("&", "&amp;")
                .replaceAll("<", "&lt;")
                .replaceAll(">", "&gt;")
                .replaceAll('"', "&quot;")
                .replaceAll("'", "&#39;");
        }

        function statusPresentation(status) {
            if (status === "Ausente") {
                return { cssClass: "bg-secondary", label: "Ausente" };
            }

            if (status === "EmAtendimento") {
                return { cssClass: "bg-warning text-dark", label: "Em atendimento" };
            }

            return { cssClass: "bg-success", label: "Online para Servicos" };
        }

        function normalizeCategory(value) {
            return String(value || "")
                .normalize("NFD")
                .replace(/[\u0300-\u036f]/g, "")
                .toLowerCase();
        }

        function categoryIconClass(category) {
            const normalized = normalizeCategory(category);

            if (normalized.includes("hidraul") || normalized.includes("plumbing")) return "fas fa-faucet-drip";
            if (normalized.includes("eletric") || normalized.includes("electrical")) return "fas fa-bolt";
            if (normalized.includes("eletron") || normalized.includes("electronics")) return "fas fa-microchip";
            if (normalized.includes("eletrodomest") || normalized.includes("appliances")) return "fas fa-blender";
            if (normalized.includes("pintur")) return "fas fa-paint-roller";
            if (normalized.includes("limpez") || normalized.includes("cleaning")) return "fas fa-broom";
            if (normalized.includes("jardin")) return "fas fa-seedling";
            if (normalized.includes("alvenar") || normalized.includes("pedrei") || normalized.includes("masonry")) return "fas fa-hammer";
            if (normalized.includes("ar condicionado") || normalized.includes("arcond") || normalized.includes("climat")) return "fas fa-wind";
            if (normalized.includes("mudanc")) return "fas fa-truck";
            if (normalized.includes("montagem") || normalized.includes("moveis") || normalized.includes("mobili")) return "fas fa-screwdriver-wrench";
            if (normalized.includes("outro") || normalized.includes("other")) return "fas fa-circle-question";

            return "fas fa-screwdriver-wrench";
        }

        function resolveCategoryIconText(categoryIcon, categoryName) {
            const rawIcon = String(categoryIcon || "").trim().toLowerCase();
            if (/^[a-z0-9_]{1,80}$/.test(rawIcon)) {
                return rawIcon;
            }

            const normalized = normalizeCategory(categoryName);
            if (normalized.includes("hidraul") || normalized.includes("plumbing")) return "plumbing";
            if (normalized.includes("eletric") || normalized.includes("electrical")) return "bolt";
            if (normalized.includes("eletron") || normalized.includes("electronics")) return "memory";
            if (normalized.includes("eletrodomest") || normalized.includes("appliances")) return "kitchen";
            if (normalized.includes("pintur")) return "format_paint";
            if (normalized.includes("limpez") || normalized.includes("cleaning")) return "cleaning_services";
            if (normalized.includes("jardin")) return "yard";
            if (normalized.includes("alvenar") || normalized.includes("pedrei") || normalized.includes("masonry")) return "construction";
            if (normalized.includes("ar condicionado") || normalized.includes("arcond") || normalized.includes("climat")) return "air";
            if (normalized.includes("mudanc")) return "local_shipping";
            if (normalized.includes("montagem") || normalized.includes("moveis") || normalized.includes("mobili")) return "handyman";

            return "build_circle";
        }

        function createProviderAvatarIcon(avatarUrl) {
            return L.divIcon({
                className: "provider-map-provider-icon-wrap",
                html: `<div class="provider-map-provider-icon"><img src="${escapeHtml(avatarUrl)}" alt="Prestador"></div>`,
                iconSize: [44, 44],
                iconAnchor: [22, 22],
                popupAnchor: [0, -20]
            });
        }

        function createCategoryPinIcon(pin, compact) {
            const stateClass = !pin.isCategoryMatch
                ? "category-miss"
                : (pin.isWithinInterestRadius ? "inside" : "outside");
            const iconSize = compact ? 44 : 60;
            const iconAnchor = compact ? 22 : 30;
            const compactClass = compact ? "compact" : "";

            return L.divIcon({
                className: "provider-map-category-icon-wrap",
                html: `<div class="provider-map-category-icon ${stateClass} ${compactClass}" title="${escapeHtml(pin.category)}">
                           <i class="${categoryIconClass(pin.category)}" aria-hidden="true"></i>
                       </div>`,
                iconSize: [iconSize, iconSize],
                iconAnchor: [iconAnchor, iconAnchor],
                popupAnchor: [0, -28]
            });
        }

        function updateOperationalBadge(status) {
            if (!statusBadgeEl || !statusTextEl) return;
            const presentation = statusPresentation(status);
            statusBadgeEl.className = `badge ${presentation.cssClass} p-2 px-3 rounded-pill shadow-sm`;
            statusTextEl.textContent = presentation.label;
        }

        function normalizeCoveragePin(rawPin) {
            if (!rawPin) return null;
            const latitude = Number(rawPin.latitude ?? rawPin.Latitude);
            const longitude = Number(rawPin.longitude ?? rawPin.Longitude);
            if (!Number.isFinite(latitude) || !Number.isFinite(longitude)) return null;

            return {
                requestId: String(rawPin.requestId ?? rawPin.RequestId ?? ""),
                category: String(rawPin.category ?? rawPin.Category ?? ""),
                categoryIcon: String(rawPin.categoryIcon ?? rawPin.CategoryIcon ?? ""),
                description: String(rawPin.description ?? rawPin.Description ?? ""),
                street: String(rawPin.street ?? rawPin.Street ?? ""),
                city: String(rawPin.city ?? rawPin.City ?? ""),
                zip: String(rawPin.zip ?? rawPin.Zip ?? ""),
                createdAt: String(rawPin.createdAt ?? rawPin.CreatedAt ?? ""),
                createdAtIso: String(rawPin.createdAtIso ?? rawPin.CreatedAtIso ?? ""),
                latitude: latitude,
                longitude: longitude,
                distanceKm: Number(rawPin.distanceKm ?? rawPin.DistanceKm ?? 0),
                isWithinInterestRadius: Boolean(rawPin.isWithinInterestRadius ?? rawPin.IsWithinInterestRadius),
                isCategoryMatch: Boolean(rawPin.isCategoryMatch ?? rawPin.IsCategoryMatch)
            };
        }

        function formatRelativeTime(createdAtIso) {
            const timestamp = Date.parse(String(createdAtIso || ""));
            if (!Number.isFinite(timestamp)) return "";

            const diffMs = Math.max(0, Date.now() - timestamp);
            const minuteMs = 60 * 1000;
            const hourMs = 60 * minuteMs;
            const dayMs = 24 * hourMs;

            if (diffMs < minuteMs) return "Agora";

            const minutes = Math.floor(diffMs / minuteMs);
            if (minutes < 60) return `Ha ${minutes} minuto${minutes === 1 ? "" : "s"}`;

            const hours = Math.floor(diffMs / hourMs);
            if (hours < 24) return `Ha ${hours} hora${hours === 1 ? "" : "s"}`;

            const days = Math.floor(diffMs / dayMs);
            if (days < 7) return `Ha ${days} dia${days === 1 ? "" : "s"}`;
            if (days < 30) {
                const weeks = Math.floor(days / 7);
                return `Ha ${weeks} semana${weeks === 1 ? "" : "s"}`;
            }
            if (days < 365) {
                const months = Math.floor(days / 30);
                return `Ha ${months} mes${months === 1 ? "" : "es"}`;
            }

            const years = Math.floor(days / 365);
            return `Ha ${years} ano${years === 1 ? "" : "s"}`;
        }

        function normalizeCoverageMap(rawMap) {
            const map = rawMap || {};
            const pinsRaw = Array.isArray(map.pins)
                ? map.pins
                : (Array.isArray(map.Pins) ? map.Pins : []);

            return {
                hasBaseLocation: Boolean(map.hasBaseLocation ?? map.HasBaseLocation),
                providerLatitude: Number(map.providerLatitude ?? map.ProviderLatitude),
                providerLongitude: Number(map.providerLongitude ?? map.ProviderLongitude),
                interestRadiusKm: Number(map.interestRadiusKm ?? map.InterestRadiusKm),
                mapSearchRadiusKm: Number(map.mapSearchRadiusKm ?? map.MapSearchRadiusKm),
                baseZipCode: String(map.baseZipCode ?? map.BaseZipCode ?? ""),
                appliedCategoryFilter: String(map.appliedCategoryFilter ?? map.AppliedCategoryFilter ?? ""),
                appliedMaxDistanceKm: Number(map.appliedMaxDistanceKm ?? map.AppliedMaxDistanceKm),
                pinPage: Number(map.pinPage ?? map.PinPage ?? 1),
                pinPageSize: Number(map.pinPageSize ?? map.PinPageSize ?? defaultPinPageSize),
                totalPins: Number(map.totalPins ?? map.TotalPins ?? pinsRaw.length),
                hasMorePins: Boolean(map.hasMorePins ?? map.HasMorePins),
                pins: pinsRaw
                    .map(normalizeCoveragePin)
                    .filter(pin => !!pin)
            };
        }

        function markerRenderKey(pin) {
            return [
                pin.category,
                pin.description,
                pin.street,
                pin.city,
                pin.distanceKm,
                pin.isWithinInterestRadius,
                pin.isCategoryMatch,
                pin.latitude,
                pin.longitude,
                pin.createdAtIso
            ].join("|");
        }

        function buildPinPopupHtml(pin) {
            const isInside = !!pin.isWithinInterestRadius;
            const distanceLabel = Number.isFinite(pin.distanceKm) ? `${pin.distanceKm.toFixed(1)} km (estimada)` : "-";
            const radiusLabel = isInside ? "Dentro do raio" : "Fora do raio";
            const categoryLabel = pin.isCategoryMatch ? "Categoria atendida" : "Categoria fora do seu filtro";

            return `
                <div class="provider-map-popup">
                    <div class="fw-semibold mb-1">${escapeHtml(pin.category)} - ${escapeHtml(distanceLabel)}</div>
                    <div class="small text-muted mb-1">${escapeHtml(pin.description)}</div>
                    <div class="small text-muted mb-2">${escapeHtml(pin.street)}, ${escapeHtml(pin.city)}</div>
                    <div class="small mb-2">${escapeHtml(radiusLabel)} | ${escapeHtml(categoryLabel)}</div>
                    <a href="/ServiceRequests/Details/${encodeURIComponent(pin.requestId)}" class="btn btn-sm btn-primary">Ver detalhes</a>
                </div>
            `;
        }

        function applyPinTooltip(marker, pin, highVolumeMode) {
            const relativeTimeLabel = formatRelativeTime(pin.createdAtIso);
            if (!relativeTimeLabel || highVolumeMode) {
                marker.unbindTooltip();
                return;
            }

            marker.bindTooltip(relativeTimeLabel, {
                permanent: true,
                direction: "bottom",
                offset: [0, 32],
                className: "provider-map-time-tooltip"
            });
        }

        function registerCategoryOptionsFromPins(pins) {
            (pins || []).forEach(function (pin) {
                const key = normalizeCategory(pin.category);
                if (!key) return;
                if (!coverageCategoryCatalog.has(key)) {
                    coverageCategoryCatalog.set(key, String(pin.category || "").trim());
                }
            });
        }

        function renderCategoryFilterOptions() {
            if (!coverageCategoryFilterEl) return;
            const selected = String(coverageCategoryFilterEl.value || "");
            const ordered = Array.from(coverageCategoryCatalog.entries())
                .sort((a, b) => a[1].localeCompare(b[1], "pt-BR"));

            coverageCategoryFilterEl.innerHTML = "<option value=\"\">Todas categorias</option>";
            ordered.forEach(function ([value, label]) {
                const option = document.createElement("option");
                option.value = value;
                option.textContent = label;
                coverageCategoryFilterEl.appendChild(option);
            });

            if (selected) {
                coverageCategoryFilterEl.value = selected;
            }
        }

        function syncFilterControlsFromMapData(data) {
            if (!data) return;

            if (coverageCategoryFilterEl && data.appliedCategoryFilter) {
                const normalized = normalizeCategory(data.appliedCategoryFilter);
                if (normalized && coverageCategoryCatalog.has(normalized)) {
                    coverageCategoryFilterEl.value = normalized;
                }
            }

            if (coverageDistanceFilterEl && Number.isFinite(data.appliedMaxDistanceKm) && data.appliedMaxDistanceKm > 0) {
                const value = String(Number(data.appliedMaxDistanceKm));
                coverageDistanceFilterEl.value = value;
            }
        }

        function getCurrentMapFilters() {
            return {
                category: normalizeCategory(coverageCategoryFilterEl?.value || ""),
                maxDistanceKm: String(coverageDistanceFilterEl?.value || "").trim()
            };
        }

        function buildRecentMatchesFromPins(pins) {
            return (pins || [])
                .slice()
                .sort((a, b) => Number(a.distanceKm || 0) - Number(b.distanceKm || 0))
                .slice(0, recentMatchesLimit)
                .map(pin => ({
                    id: pin.requestId,
                    category: pin.category,
                    categoryIcon: pin.categoryIcon,
                    description: pin.description,
                    createdAt: pin.createdAt,
                    createdAtIso: pin.createdAtIso,
                    street: pin.street,
                    city: pin.city,
                    distanceKm: pin.distanceKm,
                    latitude: pin.latitude,
                    longitude: pin.longitude
                }));
        }

        function formatDistanceKm(distanceKm) {
            return new Intl.NumberFormat("pt-BR", {
                minimumFractionDigits: 1,
                maximumFractionDigits: 1
            }).format(distanceKm);
        }

        function extractNeighborhood(street, city) {
            const safeStreet = String(street || "").trim();
            const safeCity = String(city || "").trim();

            if (!safeStreet) {
                return safeCity || "Bairro nao informado";
            }

            const parts = safeStreet
                .split("-")
                .map(part => part.trim())
                .filter(Boolean);

            if (parts.length > 1) {
                return parts[parts.length - 1];
            }

            return safeCity || safeStreet;
        }

        function fetchDrivingDistanceKm(providerLat, providerLng, requestLat, requestLng) {
            const key = [
                providerLat.toFixed(6),
                providerLng.toFixed(6),
                requestLat.toFixed(6),
                requestLng.toFixed(6)
            ].join("|");

            const cachedPromise = recentMatchesDistanceCache.get(key);
            if (cachedPromise) {
                return cachedPromise;
            }

            const queryString = new URLSearchParams({
                providerLat: String(providerLat),
                providerLng: String(providerLng),
                requestLat: String(requestLat),
                requestLng: String(requestLng)
            }).toString();

            const promise = (async () => {
                const controller = new AbortController();
                const timeout = window.setTimeout(() => controller.abort(), 9000);
                try {
                    const response = await fetch(`${drivingRouteUrl}?${queryString}`, {
                        method: "GET",
                        signal: controller.signal,
                        headers: { "X-Requested-With": "XMLHttpRequest" }
                    });

                    if (!response.ok) {
                        return null;
                    }

                    const payload = await response.json();
                    const rawDistance = Number(payload && payload.distance);
                    if (!payload || payload.success !== true || !Number.isFinite(rawDistance) || rawDistance <= 0) {
                        return null;
                    }

                    return rawDistance / 1000;
                } catch {
                    return null;
                } finally {
                    window.clearTimeout(timeout);
                }
            })();

            recentMatchesDistanceCache.set(key, promise);
            return promise;
        }

        function renderRecentDistanceBadge(cell, km, source) {
            if (!cell) return;

            if (Number.isFinite(km) && km > 0) {
                const suffix = source === "route" ? "" : " (estimada)";
                cell.textContent = `${formatDistanceKm(km)} km${suffix}`;
                return;
            }

            cell.textContent = "N/D";
        }

        async function enhanceRecentMatchesDistances(rootElement) {
            const providerLat = Number(coverageMapState && coverageMapState.providerLatitude);
            const providerLng = Number(coverageMapState && coverageMapState.providerLongitude);
            if (!Number.isFinite(providerLat) || !Number.isFinite(providerLng) || !drivingRouteUrl) {
                return;
            }

            const scope = rootElement || document;
            const cells = Array
                .from(scope.querySelectorAll("[data-recent-distance-cell='1']"))
                .filter(cell => cell.dataset.distanceResolved !== "1");

            if (!cells.length) {
                return;
            }

            const maxConcurrent = 4;
            let cursor = 0;

            async function worker() {
                while (cursor < cells.length) {
                    const index = cursor++;
                    const cell = cells[index];
                    const requestLat = Number(cell.dataset.requestLat);
                    const requestLng = Number(cell.dataset.requestLng);
                    const estimatedKm = Number(cell.dataset.estimatedDistance);

                    if (!Number.isFinite(requestLat) || !Number.isFinite(requestLng)) {
                        renderRecentDistanceBadge(cell, estimatedKm, "estimated");
                        cell.dataset.distanceResolved = "1";
                        continue;
                    }

                    const routeKm = await fetchDrivingDistanceKm(providerLat, providerLng, requestLat, requestLng);
                    if (Number.isFinite(routeKm) && routeKm > 0) {
                        renderRecentDistanceBadge(cell, routeKm, "route");
                    } else {
                        renderRecentDistanceBadge(cell, estimatedKm, "estimated");
                    }
                    cell.dataset.distanceResolved = "1";
                }
            }

            const workers = Array.from({ length: Math.min(maxConcurrent, cells.length) }, () => worker());
            await Promise.all(workers);
        }

        function ensureLeafletMap(lat, lng) {
            if (!coverageMapEl || typeof L === "undefined") return;
            if (leafletMap) return;

            leafletMap = L.map(coverageMapEl, {
                zoomControl: true
            });

            L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
                maxZoom: 19,
                attribution: "&copy; OpenStreetMap contributors"
            }).addTo(leafletMap);

            leafletMap.setView([lat, lng], 12);
        }

        function showCoverageEmptyState(messageHtml) {
            if (coverageMapEl) {
                coverageMapEl.classList.add("d-none");
            }

            if (!coverageEmptyEl) return;
            coverageEmptyEl.classList.remove("d-none");
            coverageEmptyEl.innerHTML = messageHtml;
        }

        function clearCoverageMarkers() {
            coverageMarkersByRequestId.forEach(function (entry) {
                if (coverageMarkersLayer) {
                    coverageMarkersLayer.removeLayer(entry.marker);
                }
            });
            coverageMarkersByRequestId.clear();
        }

        function syncCoverageMarkers(pins, totalPins) {
            const highVolumeMode = Number(totalPins) > 120;
            const expectedIds = new Set();

            (pins || []).forEach(function (pin) {
                const pinId = String(pin.requestId || "");
                if (!pinId) return;

                expectedIds.add(pinId);
                const currentKey = markerRenderKey(pin);
                const existing = coverageMarkersByRequestId.get(pinId);
                if (existing) {
                    if (existing.renderKey !== currentKey) {
                        existing.marker.setLatLng([pin.latitude, pin.longitude]);
                        existing.marker.setIcon(createCategoryPinIcon(pin, highVolumeMode));
                        existing.marker.setPopupContent(buildPinPopupHtml(pin));
                        applyPinTooltip(existing.marker, pin, highVolumeMode);
                        existing.renderKey = currentKey;
                    } else {
                        applyPinTooltip(existing.marker, pin, highVolumeMode);
                    }
                    return;
                }

                const marker = L.marker(
                    [pin.latitude, pin.longitude],
                    { icon: createCategoryPinIcon(pin, highVolumeMode) })
                    .addTo(coverageMarkersLayer);
                marker.bindPopup(buildPinPopupHtml(pin));
                applyPinTooltip(marker, pin, highVolumeMode);
                coverageMarkersByRequestId.set(pinId, {
                    marker: marker,
                    renderKey: currentKey
                });
            });

            Array.from(coverageMarkersByRequestId.keys()).forEach(function (pinId) {
                if (expectedIds.has(pinId)) return;
                const entry = coverageMarkersByRequestId.get(pinId);
                if (entry && coverageMarkersLayer) {
                    coverageMarkersLayer.removeLayer(entry.marker);
                }
                coverageMarkersByRequestId.delete(pinId);
            });
        }

        function updateCoverageMeta(data) {
            const loadedPins = Array.isArray(data.pins) ? data.pins.length : 0;
            const totalPins = Number.isFinite(data.totalPins) ? data.totalPins : loadedPins;
            const hasMorePins = Boolean(data.hasMorePins);
            const pinPage = Number.isFinite(data.pinPage) ? Math.max(1, Number(data.pinPage)) : 1;
            const pinPageSize = Number.isFinite(data.pinPageSize) ? Math.max(1, Number(data.pinPageSize)) : defaultPinPageSize;

            if (coveragePinsCountEl) {
                coveragePinsCountEl.textContent = String(loadedPins);
            }

            if (coveragePinsLoadedEl) {
                coveragePinsLoadedEl.textContent = String(loadedPins);
            }

            if (coveragePinsTotalEl) {
                coveragePinsTotalEl.textContent = String(totalPins);
            }

            const totalMatchesEl = document.getElementById("total-matches-count");
            if (totalMatchesEl) {
                totalMatchesEl.textContent = String(totalPins);
            }

            if (coverageLoadMoreEl) {
                coverageLoadMoreEl.classList.toggle("d-none", !hasMorePins);
                coverageLoadMoreEl.dataset.nextPage = String(pinPage + 1);
                coverageLoadMoreEl.dataset.pageSize = String(pinPageSize);
                coverageLoadMoreEl.disabled = false;
            }
        }

        function renderCoverageMap(mapData) {
            const data = normalizeCoverageMap(mapData);
            coverageMapState = data;
            registerCategoryOptionsFromPins(data.pins);
            renderCategoryFilterOptions();
            syncFilterControlsFromMapData(data);
            updateCoverageMeta(data);

            if (coverageRadiusEl) {
                coverageRadiusEl.textContent = Number.isFinite(data.interestRadiusKm)
                    ? `${data.interestRadiusKm.toFixed(1)} km`
                    : "-";
            }

            if (!data.hasBaseLocation || !Number.isFinite(data.providerLatitude) || !Number.isFinite(data.providerLongitude)) {
                clearCoverageMarkers();
                showCoverageEmptyState('Defina CEP/base e coordenadas no seu perfil para visualizar o mapa de cobertura. <a href="/Profile/Index" class="fw-semibold ms-1">Ir para Perfil</a>');
                return;
            }

            if (typeof L === "undefined") {
                showCoverageEmptyState("Mapa indisponivel no momento.");
                return;
            }

            if (coverageEmptyEl) {
                coverageEmptyEl.classList.add("d-none");
            }

            if (coverageMapEl) {
                coverageMapEl.classList.remove("d-none");
            }

            ensureLeafletMap(data.providerLatitude, data.providerLongitude);
            if (!leafletMap) return;

            if (!coverageMarkersLayer) {
                coverageMarkersLayer = L.layerGroup().addTo(leafletMap);
            }

            if (!coverageProviderLayer) {
                coverageProviderLayer = L.marker(
                    [data.providerLatitude, data.providerLongitude],
                    { icon: createProviderAvatarIcon(providerAvatarUrl) })
                    .addTo(leafletMap);
            } else {
                coverageProviderLayer.setLatLng([data.providerLatitude, data.providerLongitude]);
            }
            coverageProviderLayer.bindPopup(`<strong>Sua base</strong><br/>CEP: ${escapeHtml(data.baseZipCode || "-")}`);

            if (Number.isFinite(data.interestRadiusKm) && data.interestRadiusKm > 0) {
                if (!coverageRadiusLayer) {
                    coverageRadiusLayer = L.circle([data.providerLatitude, data.providerLongitude], {
                        radius: data.interestRadiusKm * 1000,
                        color: "#2563eb",
                        fillColor: "#60a5fa",
                        fillOpacity: 0.1,
                        weight: 2
                    }).addTo(leafletMap);
                } else {
                    coverageRadiusLayer.setLatLng([data.providerLatitude, data.providerLongitude]);
                    coverageRadiusLayer.setRadius(data.interestRadiusKm * 1000);
                }
            } else if (coverageRadiusLayer) {
                coverageRadiusLayer.remove();
                coverageRadiusLayer = null;
            }

            syncCoverageMarkers(data.pins, data.totalPins);

            const bounds = L.latLngBounds([[data.providerLatitude, data.providerLongitude]]);
            coverageMarkersByRequestId.forEach(function (entry) {
                const latLng = entry.marker.getLatLng();
                bounds.extend([latLng.lat, latLng.lng]);
            });

            if (bounds.isValid()) {
                leafletMap.fitBounds(bounds.pad(0.15), { maxZoom: 13 });
            } else {
                leafletMap.setView([data.providerLatitude, data.providerLongitude], 12);
            }
        }

        function renderMatches(items) {
            const tbody = document.getElementById("recent-matches-tbody");
            if (!tbody) return;

            const list = items || [];
            if (!list.length) {
                tbody.innerHTML = `
                    <tr>
                        <td colspan="4" class="text-center py-5">
                            <div class="text-muted">
                                <i class="fas fa-search fa-3x mb-3 opacity-25"></i>
                                <p>Nenhuma oportunidade encontrada no seu radar atual.</p>
                                <a href="/Profile/Index" class="btn btn-outline-primary btn-sm">Aumentar Raio de Busca</a>
                            </div>
                        </td>
                    </tr>`;
                return;
            }

            tbody.innerHTML = list.map(request => {
                const createdAt = request.createdAt
                    ? String(request.createdAt)
                    : (request.createdAtIso
                        ? new Date(request.createdAtIso).toLocaleString("pt-BR", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" })
                        : "-");
                const distanceKm = Number(request.distanceKm);
                const requestLatitude = Number(request.latitude);
                const requestLongitude = Number(request.longitude);
                const neighborhood = extractNeighborhood(request.street, request.city);
                const categoryIcon = resolveCategoryIconText(request.categoryIcon, request.category);
                const distanceLabel = Number.isFinite(distanceKm) ? `${formatDistanceKm(distanceKm)} km (estimada)` : "";
                const locationSuffix = distanceLabel
                    ? ` <span class="ms-2 badge rounded-pill text-bg-light border" data-recent-distance-cell="1" data-request-lat="${String(requestLatitude)}" data-request-lng="${String(requestLongitude)}" data-estimated-distance="${String(distanceKm)}">${escapeHtml(distanceLabel)}</span>`
                    : "";
                return `
                    <tr>
                        <td class="px-4">
                            <span class="badge bg-light text-primary border border-primary-subtle px-2 py-1">
                                <span class="material-symbols-outlined me-1 align-middle" style="font-size: 1rem;">${escapeHtml(categoryIcon)}</span>
                                ${escapeHtml(request.category)}
                            </span>
                        </td>
                        <td>
                            <div class="fw-semibold text-dark text-truncate" style="max-width: 250px;">${escapeHtml(request.description)}</div>
                            <small class="text-muted">Pedido em ${escapeHtml(createdAt)}</small>
                        </td>
                        <td>
                            <small class="text-muted"><i class="fas fa-map-marker-alt me-1"></i> Bairro: ${escapeHtml(neighborhood)}</small>
                            ${locationSuffix}
                        </td>
                        <td class="text-end px-4">
                            <a href="/ServiceRequests/Details/${request.id}" class="btn btn-primary btn-sm rounded-pill px-3 shadow-sm">Ver Detalhes</a>
                        </td>
                    </tr>`;
            }).join("");

            enhanceRecentMatchesDistances(tbody).catch(() => { });
        }

        async function refreshDashboard(options) {
            const settings = Object.assign({
                append: false,
                pinPage: 1,
                pinPageSize: defaultPinPageSize
            }, options || {});

            const filters = getCurrentMapFilters();
            const params = new URLSearchParams();
            if (filters.category) {
                params.set("category", filters.category);
            }
            if (filters.maxDistanceKm) {
                params.set("maxDistanceKm", filters.maxDistanceKm);
            }
            params.set("pinPage", String(Math.max(1, Number(settings.pinPage) || 1)));
            params.set("pinPageSize", String(Math.max(1, Number(settings.pinPageSize) || defaultPinPageSize)));

            const url = params.toString()
                ? `/Home/RecentMatchesData?${params.toString()}`
                : "/Home/RecentMatchesData";

            if (refreshAbortController) {
                refreshAbortController.abort();
            }

            refreshAbortController = new AbortController();
            let data;
            try {
                const response = await fetch(url, {
                    credentials: "same-origin",
                    signal: refreshAbortController.signal
                });
                if (!response.ok) return;
                data = await response.json();
            } catch (error) {
                if (error && error.name === "AbortError") return;
                throw error;
            } finally {
                refreshAbortController = null;
            }

            const totalMatches = document.getElementById("total-matches-count");
            const activeProposals = document.getElementById("active-proposals-count");
            const convertedJobs = document.getElementById("converted-jobs-count");
            const pendingAppointments = document.getElementById("pending-appointments-count");
            const upcomingVisits = document.getElementById("upcoming-visits-count");

            if (totalMatches) totalMatches.textContent = data.totalMatches;
            if (activeProposals) activeProposals.textContent = data.activeProposals;
            if (convertedJobs) convertedJobs.textContent = data.convertedJobs;
            if (pendingAppointments) pendingAppointments.textContent = data.pendingAppointments ?? 0;
            if (upcomingVisits) upcomingVisits.textContent = data.upcomingConfirmedVisits ?? 0;
            updateOperationalBadge(data.providerOperationalStatus);

            let coveragePayload = normalizeCoverageMap(data.coverageMap);
            if (settings.append && coverageMapState) {
                const mergedById = new Map();
                (coverageMapState.pins || []).forEach(pin => mergedById.set(pin.requestId, pin));
                (coveragePayload.pins || []).forEach(pin => mergedById.set(pin.requestId, pin));
                coveragePayload = Object.assign({}, coveragePayload, {
                    pins: Array.from(mergedById.values())
                        .sort((a, b) => Number(a.distanceKm || 0) - Number(b.distanceKm || 0))
                });
            }

            renderCoverageMap(coveragePayload);
            const recentMatches = buildRecentMatchesFromPins(coveragePayload?.pins ?? []);
            renderMatches(recentMatches);
        }

        function scheduleDashboardRefresh(reason, options, delayMs) {
            if (refreshDebounceHandle) {
                clearTimeout(refreshDebounceHandle);
                refreshDebounceHandle = null;
            }

            refreshDebounceHandle = setTimeout(function () {
                refreshDashboard(options).catch(function (error) {
                    console.error(`[dashboard-refresh:${reason}]`, error);
                });
            }, Number(delayMs) > 0 ? Number(delayMs) : 220);
        }

        function shouldRefreshDashboard(subject) {
            const normalized = String(subject || "").toLowerCase();
            return normalized.includes("pedido") ||
                normalized.includes("agendamento") ||
                normalized.includes("reagendamento") ||
                normalized.includes("visita");
        }

        window.addEventListener("cpm:notification", function (event) {
            const subject = event?.detail?.subject ?? "";
            if (shouldRefreshDashboard(subject)) {
                scheduleDashboardRefresh("notification", { append: false, pinPage: 1, pinPageSize: defaultPinPageSize }, 180);
            }
        });

        window.addEventListener("cpm:realtime-reconnected", function () {
            scheduleDashboardRefresh("signalr-reconnected", { append: false, pinPage: 1, pinPageSize: defaultPinPageSize }, 120);
        });

        window.addEventListener("cpm:provider-status", function (event) {
            const payload = event?.detail || {};
            if (!payload.status) return;

            const providerId = String(payload.providerId || "").toLowerCase();
            if (providerId && providerId !== currentProviderId) return;

            updateOperationalBadge(String(payload.status));
        });

        window.addEventListener("resize", function () {
            if (leafletMap) {
                leafletMap.invalidateSize();
            }
        });

        if (coverageCategoryFilterEl) {
            coverageCategoryFilterEl.addEventListener("change", function () {
                scheduleDashboardRefresh("filter-category", { append: false, pinPage: 1, pinPageSize: defaultPinPageSize }, 80);
            });
        }

        if (coverageDistanceFilterEl) {
            coverageDistanceFilterEl.addEventListener("change", function () {
                scheduleDashboardRefresh("filter-distance", { append: false, pinPage: 1, pinPageSize: defaultPinPageSize }, 80);
            });
        }

        if (coverageLoadMoreEl) {
            coverageLoadMoreEl.addEventListener("click", function () {
                const nextPage = Number(coverageLoadMoreEl.dataset.nextPage || "2");
                const pageSize = Number(coverageLoadMoreEl.dataset.pageSize || String(defaultPinPageSize));
                coverageLoadMoreEl.disabled = true;
                refreshDashboard({
                    append: true,
                    pinPage: Math.max(2, nextPage),
                    pinPageSize: Math.max(1, pageSize)
                })
                    .catch(function (error) {
                        console.error("[dashboard-refresh:load-more]", error);
                    })
                    .finally(function () {
                        coverageLoadMoreEl.disabled = false;
                    });
            });
        }

        renderCoverageMap(initialCoverageMapRaw);
        if (coverageMapState?.pins?.length) {
            renderMatches(buildRecentMatchesFromPins(coverageMapState.pins));
        }
    })();
