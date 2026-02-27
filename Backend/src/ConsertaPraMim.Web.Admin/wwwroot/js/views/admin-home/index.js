(function () {
    const config = window.adminHomeConfig || {};
    const snapshotUrl = config.snapshotUrl || "";
    const coverageMapSnapshotUrl = config.coverageMapSnapshotUrl || "";
    const form = document.getElementById("dashboard-filters");
    const filtersDrawer = document.getElementById("dashboardFiltersDrawer");
    const refreshButton = document.getElementById("refresh-dashboard-btn");
    const loadingState = document.getElementById("loading-state");
    const errorState = document.getElementById("error-state");
    const dashboardContent = document.getElementById("dashboard-content");
    const noShowContent = document.getElementById("no-show-content");
    const noShowErrorState = document.getElementById("no-show-error-state");
    const emptyState = document.getElementById("empty-state");
    const lastUpdatedLabel = document.getElementById("last-updated-label");
    const homeCoverageMapElement = document.getElementById(config.homeCoverageMapElementId || "home-coverage-map");
    const homeCoverageMapStateElement = document.getElementById(config.homeCoverageMapStateElementId || "home-coverage-map-state");
    const homeCoverageMapCitySelect = document.getElementById(config.homeCoverageMapCitySelectId || "home-coverage-map-city-select");
    const homeCoverageMapPanel = document.getElementById("home-coverage-map-panel");
    const homeCoverageMapFullscreenButton = document.getElementById("home-coverage-map-fullscreen-btn");
    const recentEventsBody = document.getElementById("recent-events-body");
    const recentEventsFiltersForm = document.getElementById("recent-events-filters");
    const recentEventsFiltersDrawer = document.getElementById("recentEventsFiltersDrawer");
    const recentEventsActiveFiltersLabel = document.getElementById("recent-events-active-filters");
    const recentEventsClearFiltersButton = document.getElementById("recent-events-clear-filters-btn");
    const recentEventsDrawerClearButton = document.getElementById("recent-events-drawer-clear-btn");
    const recentEventsSortButtons = Array.from(document.querySelectorAll(".event-sort-btn"));
    const dashboardKpiCards = Array.from(document.querySelectorAll("[data-kpi-card][data-kpi-scope='dashboard']"));
    const noShowKpiCards = Array.from(document.querySelectorAll("[data-kpi-card][data-kpi-scope='no-show']"));
    const dashboardWidgets = Array.from(document.querySelectorAll("[data-dashboard-widget]"));
    const pollIntervalMs = 30000;
    const coverageMapPollIntervalMs = 60000;

    if (!snapshotUrl || !form || !refreshButton || !loadingState || !errorState || !dashboardContent || !noShowContent || !noShowErrorState || !emptyState || !lastUpdatedLabel) {
        return;
    }

    let requestInFlight = false;
    let pollHandle = null;
    let homeCoverageMap = null;
    let homeCoverageProviderLayer = null;
    let homeCoverageRequestLayer = null;
    let homeCoverageRadiusLayer = null;
    let homeCoverageProviderPinIcon = null;
    let homeCoverageRequestPinIcon = null;
    let homeCoverageMapCityFilter = null;
    let homeCoverageMapInFlight = false;
    let homeCoverageMapLastRefreshAt = 0;
    let currentRecentEvents = Array.isArray(config.initialRecentEvents) ? config.initialRecentEvents.slice() : [];
    let recentEventsSort = {
        column: "createdAt",
        direction: "desc"
    };

            function buildQueryString() {
                const formData = new FormData(form);
                const params = new URLSearchParams();

                for (const [key, value] of formData.entries()) {
                    const stringValue = String(value ?? "").trim();
                    if (stringValue.length > 0) {
                        params.set(key, stringValue);
                    }
                }

                if (!params.has("page")) {
                    params.set("page", "1");
                }

                if (!params.has("pageSize")) {
                    params.set("pageSize", "20");
                }

                return params.toString();
            }

            function setLoadingState(isVisible) {
                loadingState.classList.toggle("d-none", !isVisible);
            }

            function setError(message) {
                if (!message) {
                    errorState.classList.add("d-none");
                    errorState.textContent = "";
                    return;
                }

                errorState.textContent = message;
                errorState.classList.remove("d-none");
            }

            function setNoShowError(message) {
                if (!message) {
                    noShowErrorState.classList.add("d-none");
                    noShowErrorState.textContent = "";
                    return;
                }

                noShowErrorState.textContent = message;
                noShowErrorState.classList.remove("d-none");
            }

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

            function formatCurrency(value) {
                return new Intl.NumberFormat("pt-BR", {
                    style: "currency",
                    currency: "BRL"
                }).format(Number(value ?? 0));
            }

            function formatPercent(value) {
                return `${Number(value ?? 0).toLocaleString("pt-BR", { minimumFractionDigits: 1, maximumFractionDigits: 1 })}%`;
            }

            function resolveKpiCardParts(card) {
                return {
                    spinner: card.querySelector("[data-kpi-spinner]"),
                    skeleton: card.querySelector("[data-kpi-skeleton]"),
                    content: card.querySelector("[data-kpi-content]"),
                    error: card.querySelector("[data-kpi-error]"),
                    title: card.querySelector("[data-kpi-title]"),
                    value: card.querySelector("[data-kpi-value]"),
                    caption: card.querySelector("[data-kpi-caption]"),
                    details: card.querySelector("[data-kpi-details]")
                };
            }

            function setKpiCardLoading(card, options) {
                const parts = resolveKpiCardParts(card);
                const hasLoaded = card.dataset.loaded === "true";
                const forceSkeleton = options?.forceSkeleton === true || !hasLoaded;

                if (parts.error) {
                    parts.error.classList.add("d-none");
                    parts.error.textContent = "";
                }

                if (parts.spinner) {
                    parts.spinner.classList.remove("d-none");
                }

                if (forceSkeleton) {
                    parts.skeleton?.classList.remove("d-none");
                    parts.content?.classList.add("d-none");
                } else {
                    parts.skeleton?.classList.add("d-none");
                    parts.content?.classList.remove("d-none");
                }
            }

            function setKpiCardError(card, message) {
                const parts = resolveKpiCardParts(card);
                if (parts.spinner) {
                    parts.spinner.classList.add("d-none");
                }

                parts.skeleton?.classList.add("d-none");

                if (parts.error) {
                    parts.error.textContent = message;
                    parts.error.classList.remove("d-none");
                }

                if (card.dataset.loaded === "true") {
                    parts.content?.classList.remove("d-none");
                } else {
                    parts.content?.classList.add("d-none");
                }
            }

            function renderKpiCard(card, payload) {
                const parts = resolveKpiCardParts(card);
                const details = Array.isArray(payload?.details) ? payload.details : [];

                if (parts.title) {
                    parts.title.textContent = payload?.title || card.dataset.kpiKey || "KPI";
                }

                if (parts.value) {
                    parts.value.textContent = payload?.value || "--";
                }

                if (parts.caption) {
                    const caption = String(payload?.caption ?? "").trim();
                    parts.caption.textContent = caption;
                    parts.caption.classList.toggle("d-none", caption.length === 0);
                }

                if (parts.details) {
                    const detailCssClass = card.dataset.kpiDetailClass || "metric-subtitle";
                    parts.details.innerHTML = details
                        .map(item => `<div class="${escapeHtml(detailCssClass)}">${escapeHtml(item.label)}: <span class="fw-semibold">${escapeHtml(item.value)}</span></div>`)
                        .join("");
                }

                parts.spinner?.classList.add("d-none");
                parts.skeleton?.classList.add("d-none");
                parts.error?.classList.add("d-none");
                parts.content?.classList.remove("d-none");
                card.dataset.loaded = "true";
            }

            async function fetchKpiCard(card, options) {
                const endpoint = card.dataset.kpiEndpoint || "";
                if (!endpoint) {
                    return;
                }

                setKpiCardLoading(card, options);

                try {
                    const query = buildQueryString();
                    const url = query ? `${endpoint}?${query}` : endpoint;
                    const response = await fetch(url, {
                        method: "GET",
                        headers: { "X-Requested-With": "XMLHttpRequest" }
                    });

                    const payload = await response.json().catch(() => null);
                    if (!response.ok || !payload || payload.success !== true || !payload.data) {
                        const fallbackMessage = `Falha ao carregar KPI (${response.status}).`;
                        setKpiCardError(card, payload?.errorMessage || fallbackMessage);
                        return;
                    }

                    renderKpiCard(card, payload.data);
                } catch (error) {
                    setKpiCardError(card, "Nao foi possivel atualizar este KPI.");
                    console.error(error);
                }
            }

            function refreshKpiCards(cards, options) {
                if (!cards.length) {
                    return Promise.resolve();
                }

                return Promise.allSettled(
                    cards.map(card => fetchKpiCard(card, options))
                );
            }

            function refreshDashboardKpiCards(options) {
                return refreshKpiCards(dashboardKpiCards, options);
            }

            function refreshNoShowKpiCards(options) {
                return refreshKpiCards(noShowKpiCards, options);
            }

            function resolveDashboardWidgetParts(widget) {
                return {
                    spinner: widget.querySelector("[data-widget-spinner]"),
                    skeleton: widget.querySelector("[data-widget-skeleton]"),
                    content: widget.querySelector("[data-widget-content]"),
                    error: widget.querySelector("[data-widget-error]")
                };
            }

            function setDashboardWidgetLoading(widget, options) {
                const parts = resolveDashboardWidgetParts(widget);
                const hasLoaded = widget.dataset.loaded === "true";
                const forceSkeleton = options?.forceSkeleton === true || !hasLoaded;

                if (parts.error) {
                    parts.error.classList.add("d-none");
                    parts.error.textContent = "";
                }

                parts.spinner?.classList.remove("d-none");

                if (forceSkeleton) {
                    parts.skeleton?.classList.remove("d-none");
                    parts.content?.classList.add("d-none");
                } else {
                    parts.skeleton?.classList.add("d-none");
                    parts.content?.classList.remove("d-none");
                }
            }

            function setDashboardWidgetError(widget, message) {
                const parts = resolveDashboardWidgetParts(widget);
                parts.spinner?.classList.add("d-none");
                parts.skeleton?.classList.add("d-none");

                if (parts.error) {
                    parts.error.textContent = message;
                    parts.error.classList.remove("d-none");
                }

                if (widget.dataset.loaded === "true") {
                    parts.content?.classList.remove("d-none");
                } else {
                    parts.content?.classList.add("d-none");
                }
            }

            function normalizeWidgetPayload(raw) {
                const value = raw && typeof raw === "object" ? raw : {};
                return {
                    key: value.key ?? value.Key ?? "",
                    subtitle: value.subtitle ?? value.Subtitle ?? "",
                    primaryValue: value.primaryValue ?? value.PrimaryValue ?? null,
                    secondaryValue: value.secondaryValue ?? value.SecondaryValue ?? null,
                    items: Array.isArray(value.items) ? value.items : (Array.isArray(value.Items) ? value.Items : []),
                    rows: Array.isArray(value.rows) ? value.rows : (Array.isArray(value.Rows) ? value.Rows : []),
                    recentEvents: Array.isArray(value.recentEvents) ? value.recentEvents : (Array.isArray(value.RecentEvents) ? value.RecentEvents : [])
                };
            }

            function normalizeWidgetRow(row) {
                const cells = Array.isArray(row?.cells) ? row.cells : (Array.isArray(row?.Cells) ? row.Cells : []);
                return cells.map(cell => ({
                    value: cell?.value ?? cell?.Value ?? "",
                    tone: cell?.tone ?? cell?.Tone ?? null,
                    isMuted: Boolean(cell?.isMuted ?? cell?.IsMuted ?? false),
                    isEmphasis: Boolean(cell?.isEmphasis ?? cell?.IsEmphasis ?? false)
                }));
            }

            function resolveWidgetBadgeClass(tone) {
                const normalized = String(tone ?? "").toLowerCase();
                if (normalized === "primary") return "bg-primary";
                if (normalized === "secondary") return "bg-secondary";
                if (normalized === "dark") return "bg-dark";
                if (normalized === "danger") return "bg-danger";
                if (normalized === "info") return "bg-info text-dark";
                if (normalized === "warning") return "bg-warning text-dark";
                if (normalized === "success") return "bg-success";
                return "bg-secondary";
            }

            function renderDashboardWidget(widget, rawPayload) {
                const parts = resolveDashboardWidgetParts(widget);
                const payload = normalizeWidgetPayload(rawPayload);
                const widgetKey = (widget.dataset.widgetKey || payload.key || "").toLowerCase();
                const subtitleLabel = widget.querySelector("[data-widget-subtitle]");
                if (subtitleLabel && payload.subtitle) {
                    subtitleLabel.textContent = payload.subtitle;
                }

                if (widgetKey === "monthly-revenue") {
                    const totalElement = widget.querySelector("[data-widget-primary-value]");
                    const providersElement = widget.querySelector("[data-widget-secondary-value]");
                    const body = widget.querySelector("#subscription-revenue-body");
                    if (totalElement && payload.primaryValue) {
                        totalElement.textContent = payload.primaryValue;
                    }
                    if (providersElement && payload.secondaryValue) {
                        providersElement.textContent = payload.secondaryValue;
                    }
                    if (body) {
                        const rows = payload.rows.map(normalizeWidgetRow);
                        if (!rows.length) {
                            body.innerHTML = "<tr><td colspan=\"4\" class=\"text-center text-muted py-3\">Sem assinaturas pagantes no periodo atual.</td></tr>";
                        } else {
                            body.innerHTML = rows.map(cells => `
                                <tr>
                                    <td class="fw-semibold">${escapeHtml(cells[0]?.value ?? "-")}</td>
                                    <td class="text-end">${escapeHtml(cells[1]?.value ?? "0")}</td>
                                    <td class="text-end">${escapeHtml(cells[2]?.value ?? "R$ 0,00")}</td>
                                    <td class="text-end fw-semibold">${escapeHtml(cells[3]?.value ?? "R$ 0,00")}</td>
                                </tr>`).join("");
                        }
                    }
                }

                if (widgetKey === "request-status" || widgetKey === "request-category" || widgetKey === "operational-status" || widgetKey === "payment-failures-by-channel") {
                    const listId = widgetKey === "request-status"
                        ? "#request-status-list"
                        : widgetKey === "request-category"
                            ? "#request-category-list"
                            : widgetKey === "operational-status"
                                ? "#operational-status-list"
                                : "#payment-failure-channel-list";
                    const emptyText = widgetKey === "request-status"
                        ? "Sem dados de status para o filtro selecionado."
                        : widgetKey === "request-category"
                            ? "Sem dados de categoria para o filtro selecionado."
                            : widgetKey === "operational-status"
                                ? "Sem dados operacionais para o filtro selecionado."
                                : "Sem falhas por canal no periodo selecionado.";
                    const list = widget.querySelector(listId);
                    const items = payload.items;
                    if (list) {
                        if (!items.length) {
                            list.innerHTML = `<li class="text-muted">${emptyText}</li>`;
                        } else {
                            list.innerHTML = items.map(item => {
                                const title = item?.title ?? item?.Title ?? "";
                                const value = item?.value ?? item?.Value ?? "0";
                                const tone = item?.tone ?? item?.Tone ?? null;
                                return `
                                    <li class="d-flex justify-content-between align-items-center">
                                        <span class="text-muted">${escapeHtml(title)}</span>
                                        <span class="badge ${resolveWidgetBadgeClass(tone)}">${escapeHtml(value)}</span>
                                    </li>`;
                            }).join("");
                        }
                    }

                    if (widgetKey === "payment-failures-by-channel") {
                        const totalFailuresEl = document.querySelector("[data-kpi-payment-failures]");
                        if (totalFailuresEl) {
                            const totalFailures = items.reduce((sum, item) => sum + Number(item?.value ?? item?.Value ?? 0), 0);
                            totalFailuresEl.textContent = formatNumber(totalFailures);
                        }
                    }
                }

                if (widgetKey === "provider-operational-status" || widgetKey === "provider-review-ranking" || widgetKey === "client-review-ranking" || widgetKey === "payment-failures-by-provider") {
                    const tableBodyId = widgetKey === "provider-operational-status"
                        ? "#provider-operational-status-body"
                        : widgetKey === "provider-review-ranking"
                            ? "#provider-review-ranking-body"
                            : widgetKey === "client-review-ranking"
                                ? "#client-review-ranking-body"
                                : "#payment-failure-provider-body";
                    const emptyMessage = widgetKey === "provider-operational-status"
                        ? "<tr><td colspan=\"2\" class=\"text-center text-muted py-3\">Sem dados de prestadores para o filtro selecionado.</td></tr>"
                        : widgetKey === "provider-review-ranking"
                            ? "<tr><td colspan=\"3\" class=\"text-center text-muted py-3\">Sem ranking de prestadores para o periodo.</td></tr>"
                            : widgetKey === "client-review-ranking"
                                ? "<tr><td colspan=\"3\" class=\"text-center text-muted py-3\">Sem ranking de clientes para o periodo.</td></tr>"
                                : "<tr id=\"payment-failure-provider-empty\"><td colspan=\"4\" class=\"text-center text-muted py-3\">Sem falhas de pagamento no periodo selecionado.</td></tr>";

                    const body = widget.querySelector(tableBodyId);
                    const rows = payload.rows.map(normalizeWidgetRow);
                    if (body) {
                        if (!rows.length) {
                            body.innerHTML = emptyMessage;
                        } else {
                            body.innerHTML = rows.map(cells => {
                                if (widgetKey === "provider-operational-status") {
                                    return `<tr><td class="text-muted">${escapeHtml(cells[0]?.value ?? "-")}</td><td class="text-end"><span class="badge bg-info text-dark">${escapeHtml(cells[1]?.value ?? "0")}</span></td></tr>`;
                                }
                                if (widgetKey === "payment-failures-by-provider") {
                                    return `<tr><td class="fw-semibold">${escapeHtml(cells[0]?.value ?? "-")}</td><td class="text-end"><span class="badge bg-danger-subtle text-danger">${escapeHtml(cells[1]?.value ?? "0")}</span></td><td class="text-end">${escapeHtml(cells[2]?.value ?? "0")}</td><td class="text-end text-muted">${escapeHtml(cells[3]?.value ?? "-")}</td></tr>`;
                                }
                                return `<tr><td class="fw-semibold">${escapeHtml(cells[0]?.value ?? "-")}</td><td class="text-end">${escapeHtml(cells[1]?.value ?? "0")}</td><td class="text-end">${escapeHtml(cells[2]?.value ?? "0")}</td></tr>`;
                            }).join("");
                        }
                    }
                }

                if (widgetKey === "review-outliers") {
                    const list = widget.querySelector("#review-outlier-list");
                    const items = payload.items;
                    if (list) {
                        if (!items.length) {
                            list.innerHTML = "<li class=\"text-muted\">Nenhum outlier de reputacao no periodo filtrado.</li>";
                        } else {
                            list.innerHTML = items.map(item => {
                                const title = item?.title ?? item?.Title ?? "";
                                const subtitle = item?.subtitle ?? item?.Subtitle ?? "";
                                const value = item?.value ?? item?.Value ?? "";
                                return `
                                    <li class="d-flex justify-content-between align-items-center gap-2">
                                        <div>
                                            <div class="fw-semibold">${escapeHtml(title)}</div>
                                            <div class="small text-muted">${escapeHtml(subtitle)}</div>
                                        </div>
                                        <div class="text-end">
                                            <span class="badge bg-danger-subtle text-danger">${escapeHtml(value)}</span>
                                        </div>
                                    </li>`;
                            }).join("");
                        }
                    }
                }

                if (widgetKey === "recent-events") {
                    currentRecentEvents = Array.isArray(payload.recentEvents) ? payload.recentEvents.slice() : [];
                    if (payload.subtitle) {
                        const rangeLabel = document.getElementById("range-label");
                        if (rangeLabel) {
                            rangeLabel.textContent = payload.subtitle;
                        }
                    }
                    renderRecentEvents();
                }

                parts.spinner?.classList.add("d-none");
                parts.skeleton?.classList.add("d-none");
                parts.error?.classList.add("d-none");
                parts.content?.classList.remove("d-none");
                widget.dataset.loaded = "true";
            }

            async function fetchDashboardWidget(widget, options) {
                const endpoint = widget.dataset.widgetEndpoint || "";
                if (!endpoint) {
                    return;
                }

                setDashboardWidgetLoading(widget, options);
                try {
                    const query = buildQueryString();
                    const url = query ? `${endpoint}?${query}` : endpoint;
                    const response = await fetch(url, {
                        method: "GET",
                        headers: { "X-Requested-With": "XMLHttpRequest" }
                    });

                    const payload = await response.json().catch(() => null);
                    if (!response.ok || !payload || payload.success !== true || !payload.data) {
                        const fallbackMessage = `Falha ao carregar widget (${response.status}).`;
                        setDashboardWidgetError(widget, payload?.errorMessage || fallbackMessage);
                        return;
                    }

                    renderDashboardWidget(widget, payload.data);
                } catch (error) {
                    setDashboardWidgetError(widget, "Nao foi possivel atualizar este widget.");
                    console.error(error);
                }
            }

            function refreshDashboardWidgets(options) {
                if (!dashboardWidgets.length) {
                    return Promise.resolve();
                }

                return Promise.allSettled(
                    dashboardWidgets.map(widget => fetchDashboardWidget(widget, options))
                );
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

            function normalizeRecentEvent(raw) {
                if (!raw || typeof raw !== "object") {
                    return {
                        type: "",
                        title: "",
                        description: "",
                        createdAt: null
                    };
                }

                return {
                    type: raw.type ?? raw.Type ?? "",
                    title: raw.title ?? raw.Title ?? "",
                    description: raw.description ?? raw.Description ?? "",
                    createdAt: raw.createdAt ?? raw.CreatedAt ?? null
                };
            }

            function parseLocalDateTimeInput(value) {
                const normalized = String(value ?? "").trim();
                if (!normalized) {
                    return null;
                }

                const parsed = new Date(normalized);
                return Number.isNaN(parsed.getTime()) ? null : parsed;
            }

            function buildRecentEventsFilterState() {
                if (!recentEventsFiltersForm) {
                    return {
                        type: "",
                        title: "",
                        description: "",
                        from: null,
                        to: null
                    };
                }

                return {
                    type: String(document.getElementById("recentEventsFilterType")?.value ?? "").trim().toLowerCase(),
                    title: String(document.getElementById("recentEventsFilterTitle")?.value ?? "").trim().toLowerCase(),
                    description: String(document.getElementById("recentEventsFilterDescription")?.value ?? "").trim().toLowerCase(),
                    from: parseLocalDateTimeInput(document.getElementById("recentEventsFilterFrom")?.value),
                    to: parseLocalDateTimeInput(document.getElementById("recentEventsFilterTo")?.value)
                };
            }

            function applyRecentEventsFilters(events, filters) {
                return events.filter(function (eventItem) {
                    const normalizedEvent = normalizeRecentEvent(eventItem);
                    const createdAt = normalizedEvent.createdAt ? new Date(normalizedEvent.createdAt) : null;
                    const type = String(normalizedEvent.type ?? "").toLowerCase();
                    const title = String(normalizedEvent.title ?? "").toLowerCase();
                    const description = String(normalizedEvent.description ?? "").toLowerCase();

                    if (filters.type && !type.includes(filters.type)) {
                        return false;
                    }

                    if (filters.title && !title.includes(filters.title)) {
                        return false;
                    }

                    if (filters.description && !description.includes(filters.description)) {
                        return false;
                    }

                    if (filters.from && (!createdAt || createdAt < filters.from)) {
                        return false;
                    }

                    if (filters.to && (!createdAt || createdAt > filters.to)) {
                        return false;
                    }

                    return true;
                });
            }

            function normalizeSortValue(eventItem, column) {
                const normalizedEvent = normalizeRecentEvent(eventItem);

                if (column === "createdAt") {
                    const createdAt = normalizedEvent.createdAt ? new Date(normalizedEvent.createdAt) : null;
                    return createdAt && !Number.isNaN(createdAt.getTime()) ? createdAt.getTime() : 0;
                }

                if (column === "description") {
                    return String(normalizedEvent.description ?? "").toLocaleLowerCase("pt-BR");
                }

                if (column === "title") {
                    return String(normalizedEvent.title ?? "").toLocaleLowerCase("pt-BR");
                }

                return String(normalizedEvent.type ?? "").toLocaleLowerCase("pt-BR");
            }

            function sortRecentEvents(events) {
                const directionMultiplier = recentEventsSort.direction === "asc" ? 1 : -1;

                return events.slice().sort(function (left, right) {
                    const leftValue = normalizeSortValue(left, recentEventsSort.column);
                    const rightValue = normalizeSortValue(right, recentEventsSort.column);

                    if (leftValue < rightValue) {
                        return -1 * directionMultiplier;
                    }

                    if (leftValue > rightValue) {
                        return 1 * directionMultiplier;
                    }

                    return 0;
                });
            }

            function updateRecentEventsFilterSummary(filters, totalCount, filteredCount) {
                if (!recentEventsActiveFiltersLabel) {
                    return;
                }

                const chips = [];
                if (filters.type) {
                    chips.push(`tipo: ${filters.type}`);
                }

                if (filters.title) {
                    chips.push(`titulo: ${filters.title}`);
                }

                if (filters.description) {
                    chips.push(`descricao: ${filters.description}`);
                }

                if (filters.from) {
                    chips.push(`de: ${formatDateTime(filters.from.toISOString())}`);
                }

                if (filters.to) {
                    chips.push(`ate: ${formatDateTime(filters.to.toISOString())}`);
                }

                if (chips.length === 0) {
                    recentEventsActiveFiltersLabel.textContent = `Sem filtros locais aplicados. Exibindo ${formatNumber(filteredCount)} evento(s).`;
                    return;
                }

                recentEventsActiveFiltersLabel.textContent = `Filtros locais: ${chips.join(" | ")}. Exibindo ${formatNumber(filteredCount)} de ${formatNumber(totalCount)} evento(s).`;
            }

            function updateRecentEventsSortUi() {
                if (!recentEventsSortButtons.length) {
                    return;
                }

                recentEventsSortButtons.forEach(function (button) {
                    const column = button.dataset.sortColumn || "";
                    const icon = button.querySelector(".event-sort-icon");
                    const isActive = column === recentEventsSort.column;

                    button.classList.toggle("is-active", isActive);
                    button.dataset.sortDirection = isActive ? recentEventsSort.direction : "";

                    if (!icon) {
                        return;
                    }

                    icon.className = `fas ${isActive
                        ? (recentEventsSort.direction === "asc" ? "fa-sort-up" : "fa-sort-down")
                        : "fa-sort"} event-sort-icon`;
                });
            }

            function renderRecentEvents() {
                if (!recentEventsBody) {
                    return;
                }

                const filters = buildRecentEventsFilterState();
                const normalizedEvents = currentRecentEvents.map(normalizeRecentEvent);
                const filteredEvents = applyRecentEventsFilters(normalizedEvents, filters);
                const sortedEvents = sortRecentEvents(filteredEvents);

                updateRecentEventsFilterSummary(filters, normalizedEvents.length, sortedEvents.length);
                updateRecentEventsSortUi();

                if (sortedEvents.length === 0) {
                    recentEventsBody.innerHTML = "<tr id=\"events-empty-row\"><td colspan=\"4\" class=\"text-center text-muted py-4\">Nenhum evento encontrado para os filtros locais selecionados.</td></tr>";
                    emptyState.classList.remove("d-none");
                    return;
                }

                emptyState.classList.add("d-none");
                recentEventsBody.innerHTML = sortedEvents
                    .map(eventItem => `
                        <tr>
                            <td><span class="badge ${resolveEventBadge(eventItem.type)}">${escapeHtml(eventItem.type)}</span></td>
                            <td class="fw-semibold">${escapeHtml(eventItem.title)}</td>
                            <td class="text-muted">${escapeHtml(eventItem.description ?? "")}</td>
                            <td class="text-muted">${formatDateTime(eventItem.createdAt)}</td>
                        </tr>`)
                    .join("");
            }

            function clearRecentEventsFilters(options) {
                if (recentEventsFiltersForm) {
                    recentEventsFiltersForm.reset();
                }

                if (options?.keepSort !== true) {
                    recentEventsSort = {
                        column: "createdAt",
                        direction: "desc"
                    };
                }

                renderRecentEvents();
            }

            function setHomeCoverageMapState(message, tone) {
                if (!homeCoverageMapStateElement) {
                    return;
                }

                const normalizedTone = tone || "info";
                homeCoverageMapStateElement.className = `alert alert-${normalizedTone} py-2 px-3 mb-3`;
                homeCoverageMapStateElement.textContent = message;
            }

            function normalizeCityFilter(value) {
                const normalized = String(value ?? "").trim();
                return normalized.length > 0 ? normalized : null;
            }

            function getFullscreenElement() {
                return document.fullscreenElement || document.webkitFullscreenElement || null;
            }

            function isHomeCoverageMapFullscreen() {
                return getFullscreenElement() === homeCoverageMapPanel;
            }

            function updateHomeCoverageMapFullscreenUi() {
                if (!homeCoverageMapFullscreenButton) {
                    return;
                }

                const fullscreenSupported = Boolean(
                    (homeCoverageMapPanel && homeCoverageMapPanel.requestFullscreen) ||
                    (homeCoverageMapPanel && homeCoverageMapPanel.webkitRequestFullscreen) ||
                    document.exitFullscreen ||
                    document.webkitExitFullscreen
                );
                const icon = homeCoverageMapFullscreenButton.querySelector("[data-map-fullscreen-icon]");
                const label = homeCoverageMapFullscreenButton.querySelector("[data-map-fullscreen-label]");
                const fullscreen = isHomeCoverageMapFullscreen();

                homeCoverageMapFullscreenButton.disabled = !fullscreenSupported;

                if (!fullscreenSupported) {
                    if (label) {
                        label.textContent = "Indisponivel";
                    }

                    if (icon) {
                        icon.className = "fas fa-ban";
                    }

                    homeCoverageMapFullscreenButton.setAttribute("aria-label", "Tela cheia indisponivel neste navegador");
                    homeCoverageMapFullscreenButton.setAttribute("title", "Tela cheia indisponivel neste navegador");
                    return;
                }

                homeCoverageMapFullscreenButton.setAttribute("aria-pressed", fullscreen ? "true" : "false");
                homeCoverageMapFullscreenButton.setAttribute("aria-label", fullscreen ? "Sair da tela cheia" : "Expandir mapa para tela cheia");
                homeCoverageMapFullscreenButton.setAttribute("title", fullscreen ? "Sair da tela cheia" : "Ver mapa em tela cheia");

                if (icon) {
                    icon.className = `fas ${fullscreen ? "fa-compress" : "fa-expand"}`;
                }

                if (label) {
                    label.textContent = fullscreen ? "Sair da tela cheia" : "Tela cheia";
                }

                if (homeCoverageMap) {
                    window.setTimeout(function () {
                        homeCoverageMap.invalidateSize();
                    }, 150);
                }
            }

            async function toggleHomeCoverageMapFullscreen() {
                if (!homeCoverageMapPanel) {
                    return;
                }

                try {
                    if (isHomeCoverageMapFullscreen()) {
                        if (document.exitFullscreen) {
                            await document.exitFullscreen();
                        } else if (document.webkitExitFullscreen) {
                            document.webkitExitFullscreen();
                        }
                    } else if (homeCoverageMapPanel.requestFullscreen) {
                        await homeCoverageMapPanel.requestFullscreen();
                    } else if (homeCoverageMapPanel.webkitRequestFullscreen) {
                        homeCoverageMapPanel.webkitRequestFullscreen();
                    }
                } catch (error) {
                    console.error("Falha ao alternar tela cheia do mapa.", error);
                } finally {
                    updateHomeCoverageMapFullscreenUi();
                }
            }

            function buildHomeCoverageCityListFromProviders(providers) {
                return providers
                    .map(provider => normalizeCityFilter(provider?.city))
                    .filter(cityName => !!cityName)
                    .filter((cityName, index, list) => list.findIndex(item => item.localeCompare(cityName, "pt-BR", { sensitivity: "accent" }) === 0) === index)
                    .sort((left, right) => left.localeCompare(right, "pt-BR", { sensitivity: "accent" }));
            }

            function setHomeCoverageCityFilterOptions(data) {
                if (!homeCoverageMapCitySelect) {
                    return;
                }

                const providers = Array.isArray(data?.providers) ? data.providers : [];
                const fromPayload = Array.isArray(data?.availableProviderCities)
                    ? data.availableProviderCities.map(normalizeCityFilter).filter(cityName => !!cityName)
                    : [];
                const cityOptions = (fromPayload.length > 0 ? fromPayload : buildHomeCoverageCityListFromProviders(providers))
                    .filter((cityName, index, list) => list.findIndex(item => item.localeCompare(cityName, "pt-BR", { sensitivity: "accent" }) === 0) === index)
                    .sort((left, right) => left.localeCompare(right, "pt-BR", { sensitivity: "accent" }));

                const previousValue = homeCoverageMapCityFilter || "";
                homeCoverageMapCitySelect.innerHTML = "";

                const defaultOption = document.createElement("option");
                defaultOption.value = "";
                defaultOption.textContent = "Todas as cidades atendidas";
                homeCoverageMapCitySelect.appendChild(defaultOption);

                cityOptions.forEach(cityName => {
                    const option = document.createElement("option");
                    option.value = cityName;
                    option.textContent = cityName;
                    homeCoverageMapCitySelect.appendChild(option);
                });

                if (previousValue && cityOptions.some(cityName => cityName.localeCompare(previousValue, "pt-BR", { sensitivity: "accent" }) === 0)) {
                    homeCoverageMapCitySelect.value = previousValue;
                    return;
                }

                if (previousValue) {
                    homeCoverageMapCityFilter = null;
                }

                homeCoverageMapCitySelect.value = "";
            }

            function buildHomeCoverageSnapshotUrl() {
                const url = new URL(coverageMapSnapshotUrl, window.location.origin);
                if (homeCoverageMapCityFilter) {
                    url.searchParams.set("city", homeCoverageMapCityFilter);
                }
                return url.toString();
            }

            function createHomeCoveragePinIcon(type) {
                const safeType = type === "request" ? "request" : "provider";
                return window.L.divIcon({
                    className: `home-coverage-map-pin ${safeType}`,
                    html: '<i class="fas fa-map-marker-alt" aria-hidden="true"></i>',
                    iconSize: [28, 40],
                    iconAnchor: [14, 38],
                    popupAnchor: [0, -34]
                });
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

            function ensureHomeCoverageMap() {
                if (!homeCoverageMapElement || !coverageMapSnapshotUrl) {
                    return false;
                }

                if (typeof window.L === "undefined") {
                    setHomeCoverageMapState("Mapa indisponivel: biblioteca Leaflet nao carregada.", "warning");
                    return false;
                }

                if (!homeCoverageMap) {
                    homeCoverageMap = window.L.map(homeCoverageMapElement, {
                        zoomControl: true
                    }).setView([-23.5505, -46.6333], 10);

                    window.L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
                        maxZoom: 19,
                        attribution: "&copy; OpenStreetMap contributors"
                    }).addTo(homeCoverageMap);

                    homeCoverageMap.createPane("homeCoverageRadiusPane");
                    homeCoverageMap.getPane("homeCoverageRadiusPane").style.zIndex = "350";
                    homeCoverageMap.getPane("homeCoverageRadiusPane").style.pointerEvents = "none";

                    homeCoverageRadiusLayer = window.L.layerGroup().addTo(homeCoverageMap);
                    homeCoverageProviderLayer = window.L.layerGroup().addTo(homeCoverageMap);
                    homeCoverageRequestLayer = window.L.layerGroup().addTo(homeCoverageMap);
                    homeCoverageProviderPinIcon = createHomeCoveragePinIcon("provider");
                    homeCoverageRequestPinIcon = createHomeCoveragePinIcon("request");
                }

                return true;
            }

            function renderHomeCoverageMap(data) {
                if (!ensureHomeCoverageMap()) {
                    return;
                }

                const providers = Array.isArray(data?.providers) ? data.providers : [];
                const requests = Array.isArray(data?.requests) ? data.requests : [];
                const bounds = [];

                setHomeCoverageCityFilterOptions(data);

                homeCoverageProviderLayer.clearLayers();
                homeCoverageRequestLayer.clearLayers();
                homeCoverageRadiusLayer.clearLayers();

                providers.forEach(provider => {
                    const lat = Number(provider.latitude);
                    const lng = Number(provider.longitude);
                    const radiusKm = Math.max(0, Number(provider.radiusKm ?? 0));
                    if (!Number.isFinite(lat) || !Number.isFinite(lng)) {
                        return;
                    }

                    const marker = window.L.marker([lat, lng], {
                        icon: homeCoverageProviderPinIcon,
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
                    marker.addTo(homeCoverageProviderLayer);

                    if (radiusKm > 0) {
                        const radiusCircle = window.L.circle([lat, lng], {
                            radius: radiusKm * 1000,
                            pane: "homeCoverageRadiusPane",
                            color: "#2563eb",
                            weight: 1.5,
                            fillColor: "#60a5fa",
                            fillOpacity: 0.03
                        });

                        radiusCircle.addTo(homeCoverageRadiusLayer);
                        if (typeof radiusCircle.bringToBack === "function") {
                            radiusCircle.bringToBack();
                        }
                    }

                    bounds.push([lat, lng]);
                });

                requests.forEach(request => {
                    const lat = Number(request.latitude);
                    const lng = Number(request.longitude);
                    if (!Number.isFinite(lat) || !Number.isFinite(lng)) {
                        return;
                    }

                    const marker = window.L.marker([lat, lng], {
                        icon: homeCoverageRequestPinIcon,
                        keyboard: false,
                        zIndexOffset: 300
                    });

                    marker.bindPopup(
                        `<div class="small">
                            <div class="fw-semibold">${escapeHtml(request.category)}</div>
                            <div>Status: ${escapeHtml(resolveRequestStatusLabel(request.status))}</div>
                            <div class="text-muted">${escapeHtml(request.addressStreet)}, ${escapeHtml(request.addressCity)}</div>
                        </div>`
                    );
                    marker.addTo(homeCoverageRequestLayer);
                    bounds.push([lat, lng]);
                });

                if (bounds.length > 0) {
                    const latLngBounds = window.L.latLngBounds(bounds);
                    homeCoverageMap.fitBounds(latLngBounds.pad(0.16));
                } else {
                    homeCoverageMap.setView([-23.5505, -46.6333], 10);
                }

                homeCoverageMap.invalidateSize();
                const cityLabel = homeCoverageMapCityFilter ? ` | Cidade: ${homeCoverageMapCityFilter}` : "";
                setHomeCoverageMapState(
                    `Prestadores: ${formatNumber(providers.length)} | Pedidos: ${formatNumber(requests.length)}${cityLabel} | Atualizado em ${formatDateTime(data?.generatedAtUtc)}`,
                    "success"
                );
            }

            async function fetchHomeCoverageMap(options) {
                if (!coverageMapSnapshotUrl || !homeCoverageMapElement || homeCoverageMapInFlight) {
                    return;
                }

                const showLoadingState = options?.showLoadingState ?? true;
                homeCoverageMapInFlight = true;
                if (showLoadingState) {
                    setHomeCoverageMapState("Atualizando mapa operacional...", "info");
                }

                try {
                    const response = await fetch(buildHomeCoverageSnapshotUrl(), {
                        method: "GET",
                        headers: { "X-Requested-With": "XMLHttpRequest" }
                    });

                    const payload = await response.json().catch(() => null);
                    if (!response.ok || !payload || payload.success !== true || !payload.data) {
                        const fallbackMessage = `Falha ao carregar mapa operacional (${response.status}).`;
                        setHomeCoverageMapState(payload?.errorMessage || fallbackMessage, "warning");
                        return;
                    }

                    renderHomeCoverageMap(payload.data);
                    homeCoverageMapLastRefreshAt = Date.now();
                } catch (error) {
                    setHomeCoverageMapState("Nao foi possivel atualizar o mapa operacional.", "warning");
                    console.error(error);
                } finally {
                    homeCoverageMapInFlight = false;
                }
            }

            function refreshHomeCoverageMapIfNeeded(forceRefresh, showLoadingState) {
                if (!coverageMapSnapshotUrl || !homeCoverageMapElement) {
                    return;
                }

                const now = Date.now();
                if (forceRefresh || now - homeCoverageMapLastRefreshAt >= coverageMapPollIntervalMs) {
                    fetchHomeCoverageMap({ showLoadingState: showLoadingState ?? false });
                }
            }

            function resolveEventBadge(type) {
                const normalized = String(type ?? "").toLowerCase();
                if (normalized === "request") return "bg-primary";
                if (normalized === "proposal") return "bg-success";
                if (normalized === "chat") return "bg-info text-dark";
                return "bg-dark";
            }

            function resolveRiskBadgeClass(level) {
                const normalized = String(level ?? "").toLowerCase();
                if (normalized === "high") return "bg-danger";
                if (normalized === "medium") return "bg-warning text-dark";
                if (normalized === "low") return "bg-success";
                return "bg-secondary";
            }

            function updateNoShowDashboard(data, errorMessage) {
                if (!data) {
                    noShowContent.classList.add("d-none");
                    setNoShowError(errorMessage || "Falha ao carregar painel de no-show.");
                    return;
                }

                setNoShowError(null);
                noShowContent.classList.remove("d-none");

                const noShowRangeLabel = document.getElementById("no-show-range-label");
                if (noShowRangeLabel) {
                    noShowRangeLabel.textContent = `${formatDateTime(data.fromUtc)} ate ${formatDateTime(data.toUtc)}`;
                }

                const categoryList = document.getElementById("no-show-category-breakdown");
                const categoryRows = Array.isArray(data.noShowByCategory) ? data.noShowByCategory : [];
                if (categoryList) {
                    if (!categoryRows.length) {
                        categoryList.innerHTML = "<li class=\"text-muted\">Sem dados por categoria para os filtros selecionados.</li>";
                    } else {
                        categoryList.innerHTML = categoryRows
                            .map(item => `
                                <li class="d-flex justify-content-between align-items-center">
                                    <span class="text-muted">${escapeHtml(item.name)}</span>
                                    <span>
                                        <span class="badge bg-danger-subtle text-danger me-1">${formatNumber(item.noShowAppointments)}</span>
                                        <span class="badge bg-secondary">${Number(item.noShowRatePercent ?? 0).toLocaleString("pt-BR", { minimumFractionDigits: 1, maximumFractionDigits: 1 })}%</span>
                                    </span>
                                </li>`)
                            .join("");
                    }
                }

                const cityList = document.getElementById("no-show-city-breakdown");
                const cityRows = Array.isArray(data.noShowByCity) ? data.noShowByCity : [];
                if (cityList) {
                    if (!cityRows.length) {
                        cityList.innerHTML = "<li class=\"text-muted\">Sem dados por cidade para os filtros selecionados.</li>";
                    } else {
                        cityList.innerHTML = cityRows
                            .map(item => `
                                <li class="d-flex justify-content-between align-items-center">
                                    <span class="text-muted">${escapeHtml(item.name)}</span>
                                    <span>
                                        <span class="badge bg-danger-subtle text-danger me-1">${formatNumber(item.noShowAppointments)}</span>
                                        <span class="badge bg-secondary">${Number(item.noShowRatePercent ?? 0).toLocaleString("pt-BR", { minimumFractionDigits: 1, maximumFractionDigits: 1 })}%</span>
                                    </span>
                                </li>`)
                            .join("");
                    }
                }

                const queueBody = document.getElementById("no-show-queue-body");
                const queueRows = Array.isArray(data.openRiskQueue) ? data.openRiskQueue : [];
                if (queueBody) {
                    if (!queueRows.length) {
                        queueBody.innerHTML = "<tr id=\"no-show-queue-empty\"><td colspan=\"8\" class=\"text-center text-muted py-3\">Sem itens na fila de risco para os filtros selecionados.</td></tr>";
                    } else {
                        queueBody.innerHTML = queueRows
                            .map(item => `
                                <tr>
                                    <td><span class="badge ${resolveRiskBadgeClass(item.riskLevel)}">${escapeHtml(item.riskLevel)}</span></td>
                                    <td>${formatNumber(item.score)}</td>
                                    <td>${formatDateTime(item.windowStartUtc)}</td>
                                    <td>${escapeHtml(item.category)}</td>
                                    <td>${escapeHtml(item.city)}</td>
                                    <td>${escapeHtml(item.providerName)}</td>
                                    <td>${escapeHtml(item.clientName)}</td>
                                    <td class="text-muted">${escapeHtml(item.reasons ?? "")}</td>
                                </tr>`)
                            .join("");
                    }
                }

                const recurrence = data.recurrenceSummary || null;

                const recurrenceWindowLabel = document.getElementById("no-show-recurrence-window-label");
                if (recurrenceWindowLabel) {
                    const windowFrom = formatDateTime(recurrence?.windowFromUtc);
                    const windowTo = formatDateTime(recurrence?.windowToUtc);
                    recurrenceWindowLabel.textContent = recurrence
                        ? `${windowFrom} ate ${windowTo}`
                        : "-";
                }

                const topClientRecurrenceList = document.getElementById("no-show-top-client-recurrence-list");
                const topClientRows = Array.isArray(recurrence?.topRecurrentClients) ? recurrence.topRecurrentClients : [];
                if (topClientRecurrenceList) {
                    if (!topClientRows.length) {
                        topClientRecurrenceList.innerHTML = "<li class=\"text-muted\">Sem reincidencia de clientes no periodo.</li>";
                    } else {
                        topClientRecurrenceList.innerHTML = topClientRows
                            .map(item => `
                                <li class="d-flex justify-content-between align-items-center">
                                    <div>
                                        <div class="fw-semibold">${escapeHtml(item.userName)}</div>
                                        <div class="small text-muted">${escapeHtml(item.lastEventType ?? "-")}</div>
                                    </div>
                                    <span class="badge bg-danger-subtle text-danger">${formatNumber(item.criticalEvents)}</span>
                                </li>`)
                            .join("");
                    }
                }

                const topProviderRecurrenceList = document.getElementById("no-show-top-provider-recurrence-list");
                const topProviderRows = Array.isArray(recurrence?.topRecurrentProviders) ? recurrence.topRecurrentProviders : [];
                if (topProviderRecurrenceList) {
                    if (!topProviderRows.length) {
                        topProviderRecurrenceList.innerHTML = "<li class=\"text-muted\">Sem reincidencia de prestadores no periodo.</li>";
                    } else {
                        topProviderRecurrenceList.innerHTML = topProviderRows
                            .map(item => `
                                <li class="d-flex justify-content-between align-items-center">
                                    <div>
                                        <div class="fw-semibold">${escapeHtml(item.userName)}</div>
                                        <div class="small text-muted">${escapeHtml(item.lastEventType ?? "-")}</div>
                                    </div>
                                    <span class="badge bg-warning text-dark">${formatNumber(item.criticalEvents)}</span>
                                </li>`)
                            .join("");
                    }
                }

                const recurrenceTrendBody = document.getElementById("no-show-recurrence-trend-body");
                const recurrenceTrendRows = Array.isArray(recurrence?.dailyTrend) ? recurrence.dailyTrend : [];
                if (recurrenceTrendBody) {
                    if (!recurrenceTrendRows.length) {
                        recurrenceTrendBody.innerHTML = "<tr><td colspan=\"4\" class=\"text-center text-muted py-3\">Sem tendencia de reincidencia para o periodo.</td></tr>";
                    } else {
                        recurrenceTrendBody.innerHTML = recurrenceTrendRows
                            .map(item => `
                                <tr>
                                    <td class="text-muted">${new Date(item.dateUtc).toLocaleDateString("pt-BR")}</td>
                                    <td class="text-end">${formatNumber(item.clientCriticalEvents)}</td>
                                    <td class="text-end">${formatNumber(item.providerCriticalEvents)}</td>
                                    <td class="text-end fw-semibold">${formatNumber(item.totalCriticalEvents)}</td>
                                </tr>`)
                            .join("");
                    }
                }
            }

            function updateLastUpdated() {
                lastUpdatedLabel.textContent = `Atualizado em ${formatDateTime(new Date().toISOString())}`;
            }

            function updateDashboard(data) {
                updateLastUpdated();
                dashboardContent.classList.remove("d-none");
            }

            async function fetchDashboard(options) {
                if (requestInFlight) {
                    return;
                }

                const showLoading = options?.showLoading ?? true;
                const updateUrl = options?.updateUrl ?? false;
                const query = buildQueryString();

                requestInFlight = true;
                if (showLoading) {
                    setLoadingState(true);
                }
                setError(null);

                try {
                    const response = await fetch(`${snapshotUrl}?${query}`, {
                        method: "GET",
                        headers: { "X-Requested-With": "XMLHttpRequest" }
                    });

                    const payload = await response.json().catch(() => null);

                    if (!response.ok || !payload || payload.success !== true || !payload.data) {
                        const fallbackMessage = `Falha ao atualizar dashboard (${response.status}).`;
                        const message = payload?.errorMessage || fallbackMessage;
                        setError(message);
                        return;
                    }

                    updateDashboard(payload.data);
                    updateNoShowDashboard(payload.noShowData, payload.noShowErrorMessage);
                    refreshDashboardWidgets({ forceSkeleton: false });
                    refreshDashboardKpiCards({ forceSkeleton: false });
                    refreshNoShowKpiCards({ forceSkeleton: false });

                    if (updateUrl) {
                        window.history.replaceState({}, "", `${window.location.pathname}?${query}`);
                    }
                } catch (error) {
                    setError("Nao foi possivel atualizar o dashboard no momento.");
                    console.error(error);
                } finally {
                    if (showLoading) {
                        setLoadingState(false);
                    }
                    requestInFlight = false;
                }
            }

            form.addEventListener("submit", function (event) {
                event.preventDefault();
                document.getElementById("page").value = "1";
                fetchDashboard({ showLoading: true, updateUrl: true });
                refreshHomeCoverageMapIfNeeded(true, true);

                if (filtersDrawer && window.bootstrap?.Offcanvas) {
                    const offcanvasInstance = window.bootstrap.Offcanvas.getInstance(filtersDrawer);
                    if (offcanvasInstance) {
                        offcanvasInstance.hide();
                    }
                }
            });

            refreshButton.addEventListener("click", function () {
                fetchDashboard({ showLoading: true, updateUrl: false });
                refreshHomeCoverageMapIfNeeded(true, true);
            });

            if (recentEventsFiltersForm) {
                recentEventsFiltersForm.addEventListener("submit", function (event) {
                    event.preventDefault();
                    renderRecentEvents();

                    if (recentEventsFiltersDrawer && window.bootstrap?.Offcanvas) {
                        const offcanvasInstance = window.bootstrap.Offcanvas.getInstance(recentEventsFiltersDrawer);
                        if (offcanvasInstance) {
                            offcanvasInstance.hide();
                        }
                    }
                });
            }

            if (recentEventsClearFiltersButton) {
                recentEventsClearFiltersButton.addEventListener("click", function () {
                    clearRecentEventsFilters();
                });
            }

            if (recentEventsDrawerClearButton) {
                recentEventsDrawerClearButton.addEventListener("click", function () {
                    clearRecentEventsFilters();
                });
            }

            if (recentEventsSortButtons.length) {
                recentEventsSortButtons.forEach(function (button) {
                    button.addEventListener("click", function () {
                        const column = button.dataset.sortColumn || "createdAt";

                        if (recentEventsSort.column === column) {
                            recentEventsSort.direction = recentEventsSort.direction === "asc" ? "desc" : "asc";
                        } else {
                            recentEventsSort = {
                                column,
                                direction: column === "createdAt" ? "desc" : "asc"
                            };
                        }

                        renderRecentEvents();
                    });
                });
            }

            function startPolling() {
                stopPolling();
                pollHandle = setInterval(function () {
                    if (document.hidden) {
                        return;
                    }
                    fetchDashboard({ showLoading: false, updateUrl: false });
                    refreshHomeCoverageMapIfNeeded(false, false);
                }, pollIntervalMs);
            }

            function stopPolling() {
                if (pollHandle) {
                    clearInterval(pollHandle);
                    pollHandle = null;
                }
            }

            document.addEventListener("visibilitychange", function () {
                if (!document.hidden) {
                    fetchDashboard({ showLoading: false, updateUrl: false });
                    refreshHomeCoverageMapIfNeeded(true, false);
                }
            });

            if (homeCoverageMapCitySelect) {
                homeCoverageMapCitySelect.addEventListener("change", function () {
                    homeCoverageMapCityFilter = normalizeCityFilter(homeCoverageMapCitySelect.value);
                    fetchHomeCoverageMap({ showLoadingState: true });
                });
            }

            if (homeCoverageMapFullscreenButton) {
                homeCoverageMapFullscreenButton.addEventListener("click", function () {
                    toggleHomeCoverageMapFullscreen();
                });

                document.addEventListener("fullscreenchange", updateHomeCoverageMapFullscreenUi);
                document.addEventListener("webkitfullscreenchange", updateHomeCoverageMapFullscreenUi);
                updateHomeCoverageMapFullscreenUi();
            }

            try {
                const initialCityFilter = normalizeCityFilter(new URL(window.location.href).searchParams.get("city"));
                homeCoverageMapCityFilter = initialCityFilter;
                if (homeCoverageMapCitySelect && initialCityFilter) {
                    homeCoverageMapCitySelect.value = initialCityFilter;
                }
            } catch {
                homeCoverageMapCityFilter = normalizeCityFilter(homeCoverageMapCitySelect?.value);
            }

            refreshHomeCoverageMapIfNeeded(true, true);
            refreshDashboardWidgets({ forceSkeleton: true });
            refreshDashboardKpiCards({ forceSkeleton: true });
            refreshNoShowKpiCards({ forceSkeleton: true });
            renderRecentEvents();
            startPolling();
        })();
