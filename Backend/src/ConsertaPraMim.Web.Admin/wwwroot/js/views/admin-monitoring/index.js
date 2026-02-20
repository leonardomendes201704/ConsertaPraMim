(function () {
        const config = window.adminMonitoringConfig || {};
        const monitoringApiBaseUrl = config.monitoringApiBaseUrl || "";
        const monitoringHubToken = config.monitoringHubToken || "";
        const endpoints = config.endpoints || {};
        const monitoringHubUrl = monitoringApiBaseUrl ? `${monitoringApiBaseUrl}/adminMonitoringHub` : "";

        const state = {
            page: Number(config.page || 1),
            pageSize: Number(config.pageSize || 20),
            total: 0,
            telemetryEnabled: true,
            runtimeConfigLoaded: false,
            runtimeConfigFetchedAtUtcMs: 0,
            runtimeConfigCacheTtlMs: 3000,
            telemetryTogglePending: false,
            autoRefreshPaused: false,
            incrementalRunning: false,
            pendingIncremental: false,
            incrementalDebounceHandle: null,
            signalRDebounceMs: 800,
            refreshIntervalMs: 5000,
            refreshTimerHandle: null,
            refreshStorageKey: 'cpm-admin-monitoring-refresh-ms',
            refreshPausedStorageKey: 'cpm-admin-monitoring-refresh-paused',
            topEndpointsSortField: 'hits',
            topEndpointsSortDir: 'desc',
            topErrorsSortField: 'count',
            topErrorsSortDir: 'desc',
            requestsSortField: 'timestampUtc',
            requestsSortDir: 'desc'
        };

        const errorAlert = document.getElementById('monitoringErrorAlert');
        const loadingAlert = document.getElementById('monitoringLoadingAlert');
        const detailsModal = new bootstrap.Modal(document.getElementById('requestDetailsModal'));
        const refreshIntervalSelect = document.getElementById('refreshInterval');
        const pauseAutoRefreshButton = document.getElementById('btnPauseMonitoringAutoRefresh');
        const autoRefreshStatusBadge = document.getElementById('monitoringAutoRefreshStatus');
        const telemetryEnabledSwitch = document.getElementById('telemetryEnabledSwitch');
        const telemetryRuntimeStatusBadge = document.getElementById('telemetryRuntimeStatusBadge');
        const monitoringDashboardContent = document.getElementById('monitoringDashboardContent');
        const monitoringOfflineState = document.getElementById('monitoringOfflineState');
        const monitoringFiltersDrawerElement = document.getElementById('monitoringFiltersDrawer');
        const monitoringFiltersDrawer = monitoringFiltersDrawerElement
            ? bootstrap.Offcanvas.getOrCreateInstance(monitoringFiltersDrawerElement)
            : null;

        if (!monitoringApiBaseUrl || !endpoints.overviewUrl || !endpoints.topEndpointsUrl || !endpoints.errorsUrl || !endpoints.requestsUrl || !endpoints.requestDetailsUrl || !endpoints.exportRequestsCsvUrl || !endpoints.configUrl || !endpoints.toggleTelemetryUrl || !errorAlert || !loadingAlert || !refreshIntervalSelect || !pauseAutoRefreshButton || !autoRefreshStatusBadge || !telemetryEnabledSwitch || !telemetryRuntimeStatusBadge || !monitoringDashboardContent || !monitoringOfflineState) {
            return;
        }

        attachJsonCopyButton('btnCopyRequestDetailsMeta', 'requestDetailsMetaJson');
        attachJsonCopyButton('btnCopyRequestDetailsRequest', 'requestDetailsRequestJson');
        attachJsonCopyButton('btnCopyRequestDetailsResponse', 'requestDetailsResponseJson');
        attachJsonCopyButton('btnCopyRequestDetailsContext', 'requestDetailsContextJson');
        attachJsonCopyButton('btnCopyRequestDetailsDiagnostics', 'requestDetailsDiagnosticsJson');

        document.getElementById('monitoringFiltersForm').addEventListener('submit', function (e) {
            e.preventDefault();
            state.page = 1;
            monitoringFiltersDrawer?.hide();
            loadAll();
        });
        document.getElementById('btnClearMonitoringFilters').addEventListener('click', function () {
            document.getElementById('range').value = '1h';
            document.getElementById('endpoint').value = '';
            document.getElementById('statusCode').value = '';
            document.getElementById('severity').value = '';
            document.getElementById('tenantId').value = '';
            state.page = 1;
            monitoringFiltersDrawer?.hide();
            loadAll();
        });
        document.getElementById('btnRefreshMonitoring').addEventListener('click', loadAll);
        document.getElementById('btnSearch').addEventListener('click', function () { state.page = 1; loadRequests(); });
        document.getElementById('btnExportRequestsCsv').addEventListener('click', exportRequestsCsv);
        document.getElementById('groupBy').addEventListener('change', loadErrors);
        document.getElementById('btnFirstPage').addEventListener('click', function () {
            if (state.page !== 1) {
                state.page = 1;
                loadRequests();
            }
        });
        document.getElementById('btnPrevPage').addEventListener('click', function () {
            if (state.page > 1) {
                state.page--;
                loadRequests();
            }
        });
        document.getElementById('btnNextPage').addEventListener('click', function () {
            const totalPages = Math.max(1, Math.ceil(state.total / state.pageSize));
            if (state.page < totalPages) {
                state.page++;
                loadRequests();
            }
        });
        document.getElementById('btnLastPage').addEventListener('click', function () {
            const totalPages = Math.max(1, Math.ceil(state.total / state.pageSize));
            if (state.page !== totalPages) {
                state.page = totalPages;
                loadRequests();
            }
        });
        refreshIntervalSelect.addEventListener('change', function () {
            applyRefreshInterval(refreshIntervalSelect.value);
            if (!state.autoRefreshPaused) {
                refreshIncremental('interval-changed').catch(function (err) {
                    showError(err.message || 'Falha ao atualizar monitoramento.');
                });
            }
        });
        pauseAutoRefreshButton.addEventListener('click', function () {
            setAutoRefreshPaused(!state.autoRefreshPaused);
        });
        telemetryEnabledSwitch.addEventListener('change', function () {
            const requestedEnabled = telemetryEnabledSwitch.checked;
            setTelemetryEnabled(requestedEnabled).catch(function (err) {
                showError(err.message || 'Falha ao atualizar estado da telemetria.');
                telemetryEnabledSwitch.checked = state.telemetryEnabled;
            });
        });
        bindGridSorting();

        async function loadAll() {
            setLoading(true);
            clearError();
            try {
                await loadRuntimeConfig(true);
                if (!state.telemetryEnabled) {
                    return;
                }

                await loadAllSections();
            } catch (err) {
                showError(err.message || 'Falha ao carregar monitoramento.');
            } finally {
                setLoading(false);
            }
        }

        async function loadAllSections() {
            await Promise.all([loadOverview(), loadTopEndpoints(), loadErrors(), loadRequests()]);
            syncMonitoringBadgeWidths();
        }

        function scheduleIncrementalRefresh(reason, debounceMs) {
            if (state.autoRefreshPaused) {
                return;
            }

            const delay = Number.isFinite(debounceMs) ? debounceMs : state.signalRDebounceMs;
            if (state.incrementalDebounceHandle) {
                clearTimeout(state.incrementalDebounceHandle);
                state.incrementalDebounceHandle = null;
            }

            if (delay <= 0) {
                refreshIncremental(reason).catch(function (err) {
                    showError(err.message || 'Falha ao atualizar monitoramento em tempo real.');
                });
                return;
            }

            state.incrementalDebounceHandle = setTimeout(function () {
                state.incrementalDebounceHandle = null;
                refreshIncremental(reason).catch(function (err) {
                    showError(err.message || 'Falha ao atualizar monitoramento em tempo real.');
                });
            }, delay);
        }

        function applyRefreshInterval(rawValue) {
            const parsed = Number.parseInt(String(rawValue || ''), 10);
            const allowed = [1000, 5000, 10000, 15000, 30000, 60000, 300000];
            const selected = allowed.includes(parsed) ? parsed : 5000;

            state.refreshIntervalMs = selected;
            refreshIntervalSelect.value = String(selected);

            try {
                localStorage.setItem(state.refreshStorageKey, String(selected));
            } catch {
                // no-op
            }

            restartPeriodicRefreshTimer();
        }

        function restoreRefreshInterval() {
            try {
                const saved = localStorage.getItem(state.refreshStorageKey);
                if (saved) {
                    applyRefreshInterval(saved);
                    return;
                }
            } catch {
                // no-op
            }

            applyRefreshInterval(refreshIntervalSelect.value);
        }

        function setAutoRefreshPaused(isPaused) {
            state.autoRefreshPaused = !!isPaused;
            try {
                localStorage.setItem(state.refreshPausedStorageKey, state.autoRefreshPaused ? '1' : '0');
            } catch {
                // no-op
            }

            if (state.autoRefreshPaused) {
                if (state.incrementalDebounceHandle) {
                    clearTimeout(state.incrementalDebounceHandle);
                    state.incrementalDebounceHandle = null;
                }
                state.pendingIncremental = false;
                pauseAutoRefreshButton.classList.remove('btn-outline-secondary');
                pauseAutoRefreshButton.classList.add('btn-warning');
                pauseAutoRefreshButton.innerHTML = '<i class="fas fa-play me-1"></i>Retomar';
                autoRefreshStatusBadge.classList.remove('text-bg-light', 'text-bg-success');
                autoRefreshStatusBadge.classList.add('text-bg-warning');
                autoRefreshStatusBadge.textContent = 'Pausado';
            } else {
                pauseAutoRefreshButton.classList.remove('btn-warning');
                pauseAutoRefreshButton.classList.add('btn-outline-secondary');
                pauseAutoRefreshButton.innerHTML = '<i class="fas fa-pause me-1"></i>Pausar';
                autoRefreshStatusBadge.classList.remove('text-bg-light', 'text-bg-warning');
                autoRefreshStatusBadge.classList.add('text-bg-success');
                autoRefreshStatusBadge.textContent = 'Atualizando';
            }
        }

        function restoreAutoRefreshPaused() {
            let paused = false;
            try {
                paused = localStorage.getItem(state.refreshPausedStorageKey) === '1';
            } catch {
                paused = false;
            }

            setAutoRefreshPaused(paused);
        }

        function restartPeriodicRefreshTimer() {
            if (state.refreshTimerHandle) {
                clearInterval(state.refreshTimerHandle);
            }

            state.refreshTimerHandle = setInterval(function () {
                if (state.autoRefreshPaused) {
                    return;
                }

                refreshIncremental('timer').catch(function (err) {
                    showError(err.message || 'Falha ao atualizar monitoramento em tempo real.');
                });
            }, state.refreshIntervalMs);
        }

        async function refreshIncremental(reason) {
            if (state.autoRefreshPaused) {
                return;
            }

            if (state.incrementalRunning) {
                state.pendingIncremental = true;
                return;
            }

            state.incrementalRunning = true;
            clearError();
            try {
                await loadRuntimeConfig(false);
                if (!state.telemetryEnabled) {
                    return;
                }

                const results = await Promise.allSettled([
                    loadOverview(),
                    loadTopEndpoints(),
                    loadErrors(),
                    loadRequests()
                ]);
                syncMonitoringBadgeWidths();

                const rejected = results.find(r => r.status === 'rejected');
                if (rejected && rejected.reason) {
                    showError(rejected.reason.message || `Falha no refresh incremental (${reason || 'signalr'}).`);
                }
            } finally {
                state.incrementalRunning = false;
                if (state.pendingIncremental) {
                    state.pendingIncremental = false;
                    scheduleIncrementalRefresh('pending', 0);
                }
            }
        }

        async function loadOverview() {
            const data = await apiGet(endpoints.overviewUrl, buildCommonQuery());
            const selectedRange = document.getElementById('range').value || '1h';
            debugPayload('overview', data);
            document.getElementById('kpiTotalRequests').textContent = formatNumber(data.totalRequests);
            document.getElementById('kpiErrorRate').textContent = `${data.errorRatePercent.toFixed(2)}%`;
            document.getElementById('kpiP95').textContent = `${data.p95LatencyMs} ms`;
            document.getElementById('kpiRpm').textContent = data.requestsPerMinute.toFixed(2);
            document.getElementById('kpiTopEndpoint').textContent = data.topEndpoint || '-';
            document.getElementById('kpiApiUptime').textContent = formatUptimeSeconds(data.apiUptimeSeconds);
            setHealthKpi('kpiApiHealth', data.apiHealthStatus);
            setHealthKpi('kpiDbHealth', data.databaseHealthStatus);
            setHealthKpi('kpiClientPortalHealth', data.clientPortalHealthStatus);
            setHealthKpi('kpiProviderPortalHealth', data.providerPortalHealthStatus);
            const combinedRequestsErrorsLatencySeries = buildRequestsErrorsLatencySeries(
                data.requestsSeries,
                data.errorsSeries,
                data.latencySeries
            );
            drawLineChart(
                'requestsErrorsChart',
                combinedRequestsErrorsLatencySeries,
                [
                    { key: 'requestsValue', color: '#0d6efd', axis: 'left', axisFormat: 'count' },
                    { key: 'errorsValue', color: '#dc3545', axis: 'left', axisFormat: 'count' },
                    { key: 'p50LatencyMs', color: '#198754', axis: 'right', axisFormat: 'ms' },
                    { key: 'p95LatencyMs', color: '#fd7e14', axis: 'right', axisFormat: 'ms' }
                ],
                selectedRange
            );
            renderStatusDistribution(data.statusDistribution);
        }

        async function loadTopEndpoints() {
            const query = buildCommonQuery();
            query.set('take', '20');
            const data = await apiGet(endpoints.topEndpointsUrl, query);
            const body = document.getElementById('topEndpointsBody');
            body.innerHTML = '';
            const sortedItems = sortCollection(
                data.items || [],
                state.topEndpointsSortField,
                state.topEndpointsSortDir,
                getTopEndpointsSortValue
            );
            sortedItems.forEach(item => {
                const tr = document.createElement('tr');
                const methodBadge = renderMethodBadge(item.method);
                tr.innerHTML = `<td class=\"text-center\">${methodBadge}</td><td class=\"text-start\"><button class=\"btn btn-link btn-sm p-0 endpoint-link\">${item.endpointTemplate}</button></td><td class=\"text-center\">${formatNumber(item.hits)}</td><td class=\"text-center\">${item.p95LatencyMs} ms</td><td class=\"text-center\">${item.errorRatePercent.toFixed(2)}%</td><td class=\"text-center\">${formatNumber(item.warningCount)}</td>`;
                tr.querySelector('.endpoint-link').addEventListener('click', function () {
                    document.getElementById('endpoint').value = item.endpointTemplate;
                    state.page = 1;
                    loadAll();
                });
                body.appendChild(tr);
            });
        }

        async function loadErrors() {
            const query = buildCommonQuery();
            query.set('groupBy', document.getElementById('groupBy').value);
            const data = await apiGet(endpoints.errorsUrl, query);
            debugPayload('errors', data);
            const body = document.getElementById('topErrorsBody');
            body.innerHTML = '';
            const sortedItems = sortCollection(
                data.items || [],
                state.topErrorsSortField,
                state.topErrorsSortDir,
                getTopErrorsSortValue
            );
            sortedItems.forEach(item => {
                const tr = document.createElement('tr');
                tr.innerHTML = `<td class=\"text-start\"><div class=\"fw-semibold\">${item.errorType}</div><div class=\"small text-muted text-break\">${item.errorKey}</div></td><td class=\"text-center\">${formatNumber(item.count)}</td>`;
                body.appendChild(tr);
            });
        }

        async function loadRequests() {
            const query = buildCommonQuery();
            query.set('search', document.getElementById('search').value || '');
            query.set('page', String(state.page));
            query.set('pageSize', String(state.pageSize));
            const data = await apiGet(endpoints.requestsUrl, query);
            state.total = data.total || 0;
            const body = document.getElementById('requestsBody');
            body.innerHTML = '';
            const sortedItems = sortCollection(
                data.items || [],
                state.requestsSortField,
                state.requestsSortDir,
                getRequestsSortValue
            );
            sortedItems.forEach(item => {
                const tr = document.createElement('tr');
                const methodBadge = renderMethodBadge(item.method);
                const statusBadge = renderStatusBadge(item.statusCode);
                const severityBadge = renderSeverityBadge(item.severity);
                const requestOrigin = formatRequestOrigin(item);
                const requestEnvironment = item.environmentName
                    ? String(item.environmentName)
                    : '-';
                tr.innerHTML = `<td class=\"text-center\">${methodBadge}</td><td class=\"text-center\">${toLocal(item.timestampUtc)}</td><td class=\"text-start\"><span class=\"small\">${item.endpointTemplate}</span></td><td class=\"text-start\"><span class=\"small\">${requestOrigin}</span></td><td class=\"text-center\"><span class=\"small\">${requestEnvironment}</span></td><td class=\"text-center\">${statusBadge}</td><td class=\"text-center\">${item.durationMs} ms</td><td class=\"text-center\">${severityBadge}</td><td class=\"text-center\"><button type=\"button\" class=\"btn btn-outline-primary btn-sm request-details-btn\">Detalhes</button></td>`;
                if (isErrorRow(item)) {
                    tr.classList.add('monitoring-row-error');
                } else if (isWarnRow(item)) {
                    tr.classList.add('monitoring-row-warn');
                }
                tr.querySelector('.request-details-btn').addEventListener('click', () => showRequestDetails(item.correlationId));
                body.appendChild(tr);
            });

            const totalPages = Math.max(1, Math.ceil(state.total / state.pageSize));
            document.getElementById('requestsPaginationText').textContent = 'Pagina ' + state.page + ' de ' + totalPages + ' - ' + formatNumber(state.total) + ' registros';
            renderRequestsPagination(totalPages);
        }
        function renderRequestsPagination(totalPages) {
            const pageNumbersRoot = document.getElementById('requestsPageNumbers');
            const firstButton = document.getElementById('btnFirstPage');
            const prevButton = document.getElementById('btnPrevPage');
            const nextButton = document.getElementById('btnNextPage');
            const lastButton = document.getElementById('btnLastPage');

            if (!pageNumbersRoot || !firstButton || !prevButton || !nextButton || !lastButton) {
                return;
            }

            const safeTotalPages = Math.max(1, Number(totalPages) || 1);
            const currentPage = Math.min(Math.max(1, state.page), safeTotalPages);
            const windowSize = 5;
            const halfWindow = Math.floor(windowSize / 2);

            let startPage = Math.max(1, currentPage - halfWindow);
            let endPage = Math.min(safeTotalPages, startPage + windowSize - 1);
            startPage = Math.max(1, endPage - windowSize + 1);

            firstButton.disabled = currentPage <= 1;
            prevButton.disabled = currentPage <= 1;
            nextButton.disabled = currentPage >= safeTotalPages;
            lastButton.disabled = currentPage >= safeTotalPages;

            pageNumbersRoot.innerHTML = '';
            for (let pageNumber = startPage; pageNumber <= endPage; pageNumber++) {
                const button = document.createElement('button');
                button.type = 'button';
                button.className = pageNumber === currentPage
                    ? 'btn btn-sm btn-primary'
                    : 'btn btn-sm btn-outline-secondary';
                button.textContent = String(pageNumber);
                button.disabled = pageNumber === currentPage;
                button.addEventListener('click', function () {
                    if (state.page === pageNumber) {
                        return;
                    }

                    state.page = pageNumber;
                    loadRequests();
                });
                pageNumbersRoot.appendChild(button);
            }
        }

        function bindGridSorting() {
            const sortButtons = Array.from(document.querySelectorAll('.monitoring-sort-btn'));
            sortButtons.forEach(button => {
                button.addEventListener('click', function () {
                    const table = String(button.dataset.sortTable || '').trim();
                    const field = String(button.dataset.sortField || '').trim();
                    if (!table || !field) {
                        return;
                    }

                    toggleGridSort(table, field);
                });
            });

            updateSortHeaderIndicators();
        }

        function toggleGridSort(table, field) {
            const sortFieldKey = `${table}SortField`;
            const sortDirKey = `${table}SortDir`;
            if (!(sortFieldKey in state) || !(sortDirKey in state)) {
                return;
            }

            if (state[sortFieldKey] === field) {
                state[sortDirKey] = state[sortDirKey] === 'asc' ? 'desc' : 'asc';
            } else {
                state[sortFieldKey] = field;
                state[sortDirKey] = getDefaultSortDirection(field);
            }

            updateSortHeaderIndicators();

            if (table === 'topEndpoints') {
                loadTopEndpoints();
                return;
            }

            if (table === 'topErrors') {
                loadErrors();
                return;
            }

            if (table === 'requests') {
                state.page = 1;
                loadRequests();
            }
        }

        function getDefaultSortDirection(field) {
            const descendingFields = new Set([
                'timestampUtc',
                'hits',
                'p95LatencyMs',
                'errorRatePercent',
                'warningCount',
                'count',
                'statusCode',
                'durationMs',
                'severity'
            ]);

            return descendingFields.has(String(field || '')) ? 'desc' : 'asc';
        }

        function updateSortHeaderIndicators() {
            const sortButtons = Array.from(document.querySelectorAll('.monitoring-sort-btn'));
            sortButtons.forEach(button => {
                const table = String(button.dataset.sortTable || '').trim();
                const field = String(button.dataset.sortField || '').trim();
                const sortField = state[`${table}SortField`];
                const sortDir = state[`${table}SortDir`];
                const icon = button.querySelector('.sort-icon');
                const th = button.closest('th');
                const isActive = sortField === field;

                button.classList.toggle('is-active', isActive);
                if (th) {
                    th.setAttribute('aria-sort', isActive
                        ? (sortDir === 'asc' ? 'ascending' : 'descending')
                        : 'none');
                }

                if (!icon) {
                    return;
                }

                icon.classList.remove('fa-sort', 'fa-sort-up', 'fa-sort-down');
                if (!isActive) {
                    icon.classList.add('fa-sort');
                    return;
                }

                icon.classList.add(sortDir === 'asc' ? 'fa-sort-up' : 'fa-sort-down');
            });
        }

        function sortCollection(items, field, direction, valueSelector) {
            const source = Array.isArray(items) ? [...items] : [];
            const dir = String(direction || 'desc').toLowerCase() === 'asc' ? 1 : -1;
            return source.sort((leftItem, rightItem) => {
                const leftValue = valueSelector(leftItem, field);
                const rightValue = valueSelector(rightItem, field);
                return compareSortValues(leftValue, rightValue, dir);
            });
        }

        function compareSortValues(leftValue, rightValue, dir) {
            const left = normalizeSortValue(leftValue);
            const right = normalizeSortValue(rightValue);
            const leftEmpty = left === '';
            const rightEmpty = right === '';

            if (leftEmpty && rightEmpty) {
                return 0;
            }

            if (leftEmpty) {
                return 1;
            }

            if (rightEmpty) {
                return -1;
            }

            if (typeof left === 'number' && typeof right === 'number') {
                return (left - right) * dir;
            }

            return String(left).localeCompare(String(right), 'pt-BR', { numeric: true, sensitivity: 'base' }) * dir;
        }

        function normalizeSortValue(value) {
            if (value === undefined || value === null) {
                return '';
            }

            if (value instanceof Date) {
                const timestamp = value.getTime();
                return Number.isFinite(timestamp) ? timestamp : '';
            }

            if (typeof value === 'number') {
                return Number.isFinite(value) ? value : '';
            }

            if (typeof value === 'boolean') {
                return value ? 1 : 0;
            }

            if (typeof value === 'string') {
                const trimmed = value.trim();
                return trimmed;
            }

            return String(value);
        }

        function getTopEndpointsSortValue(item, field) {
            if (!item) {
                return '';
            }

            switch (field) {
                case 'method':
                    return item.method;
                case 'endpointTemplate':
                    return item.endpointTemplate;
                case 'hits':
                    return Number(item.hits || 0);
                case 'p95LatencyMs':
                    return Number(item.p95LatencyMs || 0);
                case 'errorRatePercent':
                    return Number(item.errorRatePercent || 0);
                case 'warningCount':
                    return Number(item.warningCount || 0);
                default:
                    return '';
            }
        }

        function getTopErrorsSortValue(item, field) {
            if (!item) {
                return '';
            }

            switch (field) {
                case 'count':
                    return Number(item.count || 0);
                case 'errorType':
                    return `${String(item.errorType || '')} ${String(item.errorKey || '')}`.trim();
                default:
                    return '';
            }
        }

        function getSeveritySortWeight(severity) {
            const normalized = String(severity || '').toLowerCase();
            if (normalized === 'error') {
                return 3;
            }

            if (normalized === 'warn') {
                return 2;
            }

            if (normalized === 'info') {
                return 1;
            }

            return 0;
        }

        function getRequestsSortValue(item, field) {
            if (!item) {
                return '';
            }

            switch (field) {
                case 'method':
                    return item.method;
                case 'timestampUtc':
                    return parseUtcDate(item.timestampUtc);
                case 'endpointTemplate':
                    return item.endpointTemplate;
                case 'origin':
                    return formatRequestOrigin(item);
                case 'environmentName':
                    return item.environmentName;
                case 'statusCode':
                    return Number(item.statusCode || 0);
                case 'durationMs':
                    return Number(item.durationMs || 0);
                case 'severity':
                    return getSeveritySortWeight(item.severity);
                default:
                    return '';
            }
        }

        async function showRequestDetails(correlationId) {
            document.getElementById('requestDetailsMetaJson').textContent = 'Carregando...';
            document.getElementById('requestDetailsRequestJson').textContent = 'Carregando...';
            document.getElementById('requestDetailsResponseJson').textContent = 'Carregando...';
            document.getElementById('requestDetailsContextJson').textContent = 'Carregando...';
            document.getElementById('requestDetailsDiagnosticsJson').textContent = 'Carregando...';
            detailsModal.show();

            const query = new URLSearchParams();
            query.set('correlationId', correlationId);
            const data = await apiGet(endpoints.requestDetailsUrl, query);

            const metadata = {
                id: data.id,
                timestampUtc: data.timestampUtc,
                correlationId: data.correlationId,
                traceId: data.traceId,
                method: data.method,
                endpointTemplate: data.endpointTemplate,
                path: data.path,
                statusCode: data.statusCode,
                durationMs: data.durationMs,
                severity: data.severity,
                isError: data.isError,
                warningCount: data.warningCount,
                warningCodes: parseJsonSafely(data.warningCodesJson, []),
                errorType: data.errorType,
                normalizedErrorMessage: data.normalizedErrorMessage,
                normalizedErrorKey: data.normalizedErrorKey,
                ipHash: data.ipHash,
                userAgent: data.userAgent,
                userId: data.userId,
                tenantId: data.tenantId,
                requestSizeBytes: data.requestSizeBytes,
                responseSizeBytes: data.responseSizeBytes,
                scheme: data.scheme,
                host: data.host,
                environmentName: data.environmentName
            };

            const requestBodyRaw = firstDefined(
                data.requestBodyJson,
                data.requestBody,
                data.requestPayload,
                data.bodyRequest);
            const responseBodyRaw = firstDefined(
                data.responseBodyJson,
                data.responseBody,
                data.responsePayload,
                data.bodyResponse);

            const requestBodyPayload = normalizePayloadAsJsonObject(
                requestBodyRaw,
                buildRequestBodyFallback(data));
            const responseBodyPayload = normalizePayloadAsJsonObject(
                responseBodyRaw,
                buildResponseBodyFallback(data));

            const requestHeadersPayload = normalizePayloadAsJsonObject(
                firstDefined(data.requestHeadersJson, data.requestHeaders, data.headers),
                {});
            const queryStringPayload = normalizePayloadAsJsonObject(
                firstDefined(data.queryStringJson, data.queryString, data.query),
                {});
            const routeValuesPayload = normalizePayloadAsJsonObject(
                firstDefined(data.routeValuesJson, data.routeValues, data.route),
                {});

            const contextPayload = {
                requestHeaders: requestHeadersPayload,
                queryString: queryStringPayload,
                routeValues: routeValuesPayload
            };

            const diagnosticsPayload = buildCaptureDiagnostics(
                data,
                requestBodyRaw,
                responseBodyRaw,
                contextPayload);

            document.getElementById('requestDetailsMetaJson').textContent = JSON.stringify(metadata, null, 2);
            document.getElementById('requestDetailsRequestJson').textContent = JSON.stringify(requestBodyPayload, null, 2);
            document.getElementById('requestDetailsResponseJson').textContent = JSON.stringify(responseBodyPayload, null, 2);
            document.getElementById('requestDetailsContextJson').textContent = JSON.stringify(contextPayload, null, 2);
            document.getElementById('requestDetailsDiagnosticsJson').textContent = JSON.stringify(diagnosticsPayload, null, 2);
        }

        async function exportRequestsCsv() {
            const button = document.getElementById('btnExportRequestsCsv');
            const originalLabel = button.textContent;
            button.disabled = true;
            button.textContent = 'Exportando...';

            try {
                const query = buildCommonQuery();
                query.set('search', document.getElementById('search').value || '');

                const data = await apiGet(endpoints.exportRequestsCsvUrl, query);
                const base64Content = String(data.base64Content || '').trim();
                if (!base64Content) {
                    throw new Error('Exportacao retornou vazia.');
                }

                const fileName = data.fileName || `admin-monitoring-requests-${new Date().toISOString().replace(/[:.]/g, '-')}.csv`;
                const contentType = data.contentType || 'text/csv; charset=utf-8';
                downloadBase64File(base64Content, fileName, contentType);
            } catch (err) {
                showError(err.message || 'Falha ao exportar requests monitorados.');
            } finally {
                button.disabled = false;
                button.textContent = originalLabel;
            }
        }

        function downloadBase64File(base64Content, fileName, contentType) {
            const normalizedBase64 = String(base64Content || '').replace(/\s/g, '');
            const binary = window.atob(normalizedBase64);
            const bytes = new Uint8Array(binary.length);
            for (let i = 0; i < binary.length; i++) {
                bytes[i] = binary.charCodeAt(i);
            }

            const blob = new Blob([bytes], { type: contentType || 'application/octet-stream' });
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = fileName || 'export.csv';
            document.body.appendChild(link);
            link.click();
            link.remove();
            URL.revokeObjectURL(url);
        }

        async function loadRuntimeConfig(forceReload) {
            const nowUtcMs = Date.now();
            if (!forceReload &&
                state.runtimeConfigLoaded &&
                (nowUtcMs - state.runtimeConfigFetchedAtUtcMs) < state.runtimeConfigCacheTtlMs) {
                return;
            }

            const data = await apiGet(endpoints.configUrl, new URLSearchParams());
            applyTelemetryRuntimeConfig(data);
            state.runtimeConfigLoaded = true;
            state.runtimeConfigFetchedAtUtcMs = nowUtcMs;
        }

        async function setTelemetryEnabled(enabled) {
            if (state.telemetryTogglePending) {
                return;
            }

            state.telemetryTogglePending = true;
            telemetryEnabledSwitch.disabled = true;
            clearError();

            try {
                const data = await apiPostJson(endpoints.toggleTelemetryUrl, { enabled: !!enabled });
                applyTelemetryRuntimeConfig(data);
                state.runtimeConfigLoaded = true;
                state.runtimeConfigFetchedAtUtcMs = Date.now();

                if (state.telemetryEnabled) {
                    await loadAll();
                } else {
                    setLoading(false);
                }
            } finally {
                state.telemetryTogglePending = false;
                telemetryEnabledSwitch.disabled = false;
            }
        }

        function applyTelemetryRuntimeConfig(config) {
            const enabled = !!(config && config.telemetryEnabled);
            state.telemetryEnabled = enabled;
            telemetryEnabledSwitch.checked = enabled;
            telemetryEnabledSwitch.disabled = !!state.telemetryTogglePending;

            telemetryRuntimeStatusBadge.classList.remove('text-bg-secondary', 'text-bg-success', 'text-bg-danger');
            telemetryRuntimeStatusBadge.classList.add(enabled ? 'text-bg-success' : 'text-bg-danger');
            telemetryRuntimeStatusBadge.textContent = enabled ? 'Ligada' : 'Desligada';

            monitoringDashboardContent.classList.toggle('d-none', !enabled);
            monitoringOfflineState.classList.toggle('d-none', enabled);

            const refreshButton = document.getElementById('btnRefreshMonitoring');
            refreshButton.disabled = !enabled;
            refreshIntervalSelect.disabled = !enabled;
            pauseAutoRefreshButton.disabled = !enabled;
            if (!enabled) {
                autoRefreshStatusBadge.classList.remove('text-bg-light', 'text-bg-success', 'text-bg-warning');
                autoRefreshStatusBadge.classList.add('text-bg-secondary');
                autoRefreshStatusBadge.textContent = 'Desligada';
            } else {
                autoRefreshStatusBadge.classList.remove('text-bg-secondary');
                setAutoRefreshPaused(state.autoRefreshPaused);
            }
        }

        async function apiGet(url, query) {
            const response = await fetch(`${url}?${query.toString()}`, { credentials: 'same-origin' });
            const payload = await response.json();
            if (!response.ok || !payload.success) {
                throw new Error(payload.errorMessage || 'Erro de comunicação com o backend.');
            }
            return payload.data;
        }

        async function apiPostJson(url, body) {
            const response = await fetch(url, {
                method: 'POST',
                credentials: 'same-origin',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(body || {})
            });

            const payload = await response.json();
            if (!response.ok || !payload.success) {
                throw new Error(payload.errorMessage || 'Erro de comunicação com o backend.');
            }

            return payload.data;
        }

        function buildCommonQuery() {
            const query = new URLSearchParams();
            query.set('range', document.getElementById('range').value || '1h');
            appendOptional(query, 'endpoint', document.getElementById('endpoint').value);
            appendOptional(query, 'statusCode', document.getElementById('statusCode').value);
            appendOptional(query, 'severity', document.getElementById('severity').value);
            appendOptional(query, 'tenantId', document.getElementById('tenantId').value);
            return query;
        }

        function appendOptional(query, key, value) {
            if (value && String(value).trim().length > 0) query.set(key, String(value).trim());
        }

        function buildRequestsErrorsLatencySeries(requestsSeries, errorsSeries, latencySeries) {
            const map = new Map();

            (requestsSeries || []).forEach(function (point) {
                const timestamp =
                    point.bucketUtc ||
                    point.BucketUtc ||
                    point.bucketStartUtc ||
                    point.BucketStartUtc ||
                    point.timestampUtc ||
                    point.TimestampUtc ||
                    point.time ||
                    point.Time;
                if (!timestamp) return;

                const key = String(timestamp);
                const current = map.get(key) || {
                    timestampUtc: timestamp,
                    requestsValue: 0,
                    errorsValue: 0,
                    p50LatencyMs: 0,
                    p95LatencyMs: 0
                };
                current.requestsValue = Number(point.value || 0);
                map.set(key, current);
            });

            (errorsSeries || []).forEach(function (point) {
                const timestamp =
                    point.bucketUtc ||
                    point.BucketUtc ||
                    point.bucketStartUtc ||
                    point.BucketStartUtc ||
                    point.timestampUtc ||
                    point.TimestampUtc ||
                    point.time ||
                    point.Time;
                if (!timestamp) return;

                const key = String(timestamp);
                const current = map.get(key) || {
                    timestampUtc: timestamp,
                    requestsValue: 0,
                    errorsValue: 0,
                    p50LatencyMs: 0,
                    p95LatencyMs: 0
                };
                current.errorsValue = Number(point.value || 0);
                map.set(key, current);
            });

            (latencySeries || []).forEach(function (point) {
                const timestamp =
                    point.bucketUtc ||
                    point.BucketUtc ||
                    point.bucketStartUtc ||
                    point.BucketStartUtc ||
                    point.timestampUtc ||
                    point.TimestampUtc ||
                    point.time ||
                    point.Time;
                if (!timestamp) return;

                const key = String(timestamp);
                const current = map.get(key) || {
                    timestampUtc: timestamp,
                    requestsValue: 0,
                    errorsValue: 0,
                    p50LatencyMs: 0,
                    p95LatencyMs: 0
                };
                current.p50LatencyMs = Number(point.p50Ms || 0);
                current.p95LatencyMs = Number(point.p95Ms || 0);
                map.set(key, current);
            });

            return Array.from(map.values()).sort(function (a, b) {
                return new Date(a.timestampUtc).getTime() - new Date(b.timestampUtc).getTime();
            });
        }

        function drawLineChart(canvasId, points, seriesConfig, range) {
            const canvas = document.getElementById(canvasId);
            const ctx = canvas.getContext('2d');
            const dpr = window.devicePixelRatio || 1;
            const containerWidth = canvas.parentElement ? canvas.parentElement.clientWidth : 0;
            const rect = canvas.getBoundingClientRect();
            const width = Math.max(320, Math.floor(containerWidth || rect.width || canvas.clientWidth || 480));
            const height = 180;
            canvas.style.display = 'block';
            canvas.style.width = '100%';
            canvas.style.maxWidth = '100%';
            canvas.style.height = `${height}px`;
            canvas.width = Math.floor(width * dpr);
            canvas.height = Math.floor(height * dpr);
            ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
            ctx.clearRect(0, 0, width, height);

            const normalizedPoints = normalizeTimeseriesPoints(points);
            if (normalizedPoints.length === 0) return;

            const hasRightAxis = seriesConfig.some(function (series) { return resolveSeriesAxis(series) === 'right'; });
            const leftValues = [];
            const rightValues = [];

            seriesConfig.forEach(function (series) {
                const axis = resolveSeriesAxis(series);
                normalizedPoints.forEach(function (point) {
                    const value = Number(point.raw[series.key] || 0);
                    if (!Number.isFinite(value)) {
                        return;
                    }

                    if (axis === 'right') {
                        rightValues.push(value);
                    } else {
                        leftValues.push(value);
                    }
                });
            });

            const maxLeftValue = Math.max(1, ...(leftValues.length > 0 ? leftValues : [0]));
            const maxRightValue = hasRightAxis
                ? Math.max(1, ...(rightValues.length > 0 ? rightValues : [0]))
                : maxLeftValue;

            const leftPad = 52;
            const rightPad = hasRightAxis ? 52 : 12;
            const topPad = 10;
            const bottomPad = 30;
            const chartWidth = Math.max(1, width - leftPad - rightPad);
            const chartHeight = Math.max(1, height - topPad - bottomPad);

            ctx.strokeStyle = '#e5e7eb';
            ctx.lineWidth = 1;
            ctx.fillStyle = '#6b7280';
            ctx.font = '11px Inter, sans-serif';
            ctx.textAlign = 'right';
            ctx.textBaseline = 'middle';
            const yTicks = 4;
            for (let i = 0; i <= yTicks; i++) {
                const y = topPad + (chartHeight * i / yTicks);
                const yValue = maxLeftValue - ((maxLeftValue * i) / yTicks);
                ctx.beginPath();
                ctx.moveTo(leftPad, y);
                ctx.lineTo(leftPad + chartWidth, y);
                ctx.stroke();
                ctx.fillText(formatAxisValue(yValue, resolveAxisFormat(seriesConfig, 'left')), leftPad - 6, y);
            }

            if (hasRightAxis) {
                ctx.textAlign = 'left';
                for (let i = 0; i <= yTicks; i++) {
                    const y = topPad + (chartHeight * i / yTicks);
                    const yValue = maxRightValue - ((maxRightValue * i) / yTicks);
                    ctx.fillText(
                        formatAxisValue(yValue, resolveAxisFormat(seriesConfig, 'right')),
                        leftPad + chartWidth + 6,
                        y
                    );
                }
            }

            const xFromIndex = function (index) {
                if (normalizedPoints.length === 1) {
                    return leftPad + (chartWidth / 2);
                }
                return leftPad + ((index * chartWidth) / (normalizedPoints.length - 1));
            };

            const xTickIndexes = buildTickIndexes(normalizedPoints.length, 6);
            ctx.textAlign = 'center';
            ctx.textBaseline = 'top';
            xTickIndexes.forEach(function (pointIndex) {
                const x = xFromIndex(pointIndex);
                const label = formatTimelineLabel(normalizedPoints[pointIndex].timestampUtc, range);
                ctx.fillText(label, x, topPad + chartHeight + 6);
            });

            seriesConfig.forEach(series => {
                ctx.save();
                ctx.beginPath();
                ctx.rect(leftPad, topPad, chartWidth, chartHeight);
                ctx.clip();

                ctx.strokeStyle = series.color;
                ctx.lineWidth = 2;
                ctx.beginPath();
                let started = false;
                normalizedPoints.forEach((point, idx) => {
                    const value = Number(point.raw[series.key] || 0);
                    if (!Number.isFinite(value)) {
                        return;
                    }

                    const x = clamp(xFromIndex(idx), leftPad, leftPad + chartWidth);
                    const axisMax = resolveSeriesAxis(series) === 'right' ? maxRightValue : maxLeftValue;
                    const yRaw = topPad + chartHeight - ((value / axisMax) * chartHeight);
                    const y = clamp(yRaw, topPad, topPad + chartHeight);
                    if (!started) {
                        ctx.moveTo(x, y);
                        started = true;
                    } else {
                        ctx.lineTo(x, y);
                    }
                });
                if (started) {
                    ctx.stroke();
                }

                ctx.fillStyle = series.color;
                normalizedPoints.forEach((point, idx) => {
                    const value = Number(point.raw[series.key] || 0);
                    if (!Number.isFinite(value)) {
                        return;
                    }

                    const x = clamp(xFromIndex(idx), leftPad, leftPad + chartWidth);
                    const axisMax = resolveSeriesAxis(series) === 'right' ? maxRightValue : maxLeftValue;
                    const yRaw = topPad + chartHeight - ((value / axisMax) * chartHeight);
                    const y = clamp(yRaw, topPad, topPad + chartHeight);
                    ctx.beginPath();
                    ctx.arc(x, y, 2.2, 0, Math.PI * 2);
                    ctx.fill();
                });

                ctx.restore();
            });
        }

        function resolveSeriesAxis(series) {
            return series && series.axis === 'right' ? 'right' : 'left';
        }

        function resolveAxisFormat(seriesConfig, axis) {
            const matched = (seriesConfig || []).find(function (series) {
                return resolveSeriesAxis(series) === axis && series.axisFormat;
            });
            return matched && matched.axisFormat ? matched.axisFormat : 'count';
        }

        function clamp(value, min, max) {
            return Math.min(max, Math.max(min, value));
        }

        function normalizeTimeseriesPoints(points) {
            return (points || [])
                .map(function (point) {
                    const timestampRaw =
                        point.bucketUtc ||
                        point.BucketUtc ||
                        point.bucketStartUtc ||
                        point.BucketStartUtc ||
                        point.timestampUtc ||
                        point.TimestampUtc ||
                        point.time ||
                        point.Time;
                    const timestampUtc = parseUtcDate(timestampRaw);
                    return {
                        timestampUtc: timestampUtc,
                        raw: point || {}
                    };
                })
                .filter(function (point) { return !Number.isNaN(point.timestampUtc.getTime()); })
                .sort(function (a, b) { return a.timestampUtc.getTime() - b.timestampUtc.getTime(); });
        }

        function buildTickIndexes(totalPoints, maxTicks) {
            if (totalPoints <= 0) return [];
            if (totalPoints === 1) return [0];

            const ticks = Math.min(maxTicks, totalPoints);
            const indexes = new Set();
            for (let i = 0; i < ticks; i++) {
                const index = Math.round((i * (totalPoints - 1)) / Math.max(1, ticks - 1));
                indexes.add(index);
            }
            return Array.from(indexes).sort(function (a, b) { return a - b; });
        }

        function formatTimelineLabel(timestampUtc, range) {
            const date = timestampUtc instanceof Date ? timestampUtc : new Date(timestampUtc);
            if (Number.isNaN(date.getTime())) return '';

            if (range === '1h' || range === '2h' || range === '4h' || range === '6h' || range === '8h' || range === '12h') {
                return date.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
            }

            if (range === '24h') {
                return date.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
            }

            if (range === '7d') {
                const dayMonth = date.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' });
                const hour = date.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
                return `${dayMonth} ${hour}`;
            }

            return date.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' });
        }

        function formatCompactNumber(value) {
            const numeric = Number(value || 0);
            if (numeric >= 1000) {
                return `${(numeric / 1000).toFixed(1)}k`;
            }
            return Math.round(numeric).toString();
        }

        function formatAxisValue(value, axisFormat) {
            const normalizedFormat = String(axisFormat || 'count').toLowerCase();
            if (normalizedFormat === 'ms') {
                const numeric = Number(value || 0);
                if (numeric >= 1000) {
                    return `${(numeric / 1000).toFixed(1)}k ms`;
                }
                return `${Math.round(numeric)} ms`;
            }

            return formatCompactNumber(value);
        }

        function renderStatusDistribution(items) {
            const root = document.getElementById('statusDistribution');
            root.innerHTML = '';
            const total = (items || []).reduce((acc, item) => acc + item.count, 0);
            (items || []).forEach(item => {
                const pct = total > 0 ? (item.count * 100 / total) : 0;
                const div = document.createElement('div');
                div.className = 'mb-2';
                div.innerHTML = `<div class=\"d-flex justify-content-between small\"><span>${item.statusCode}</span><span>${formatNumber(item.count)} (${pct.toFixed(1)}%)</span></div><div class=\"progress\" style=\"height:8px;\"><div class=\"progress-bar\" role=\"progressbar\" style=\"width:${pct.toFixed(1)}%\"></div></div>`;
                root.appendChild(div);
            });
        }

        function toLocal(value) {
            return parseUtcDate(value).toLocaleString('pt-BR');
        }

        function escapeHtml(value) {
            return String(value ?? '')
                .replaceAll('&', '&amp;')
                .replaceAll('<', '&lt;')
                .replaceAll('>', '&gt;')
                .replaceAll('"', '&quot;')
                .replaceAll("'", '&#39;');
        }

        function isErrorRow(item) {
            const statusCode = Number(item?.statusCode || 0);
            const severity = String(item?.severity || '').toLowerCase();
            return statusCode >= 400 || severity === 'error';
        }

        function isWarnRow(item) {
            const statusCode = Number(item?.statusCode || 0);
            const severity = String(item?.severity || '').toLowerCase();
            return statusCode < 400 && severity === 'warn';
        }

        function renderStatusBadge(statusCode) {
            const code = Number(statusCode || 0);
            let className = 'bg-secondary';
            if (code >= 500) {
                className = 'bg-danger';
            } else if (code >= 400) {
                className = 'bg-warning text-dark';
            } else if (code >= 300) {
                className = 'bg-info text-dark';
            } else if (code >= 200) {
                className = 'bg-success';
            }

            return `<span class="badge ${className} monitoring-uniform-badge">${code}</span>`;
        }

        function renderSeverityBadge(severity) {
            const normalized = String(severity || 'info').toLowerCase();
            const className = normalized === 'error'
                ? 'bg-danger'
                : normalized === 'warn'
                    ? 'bg-warning text-dark'
                    : 'bg-secondary';
            return `<span class="badge ${className} monitoring-uniform-badge">${normalized}</span>`;
        }

        function renderMethodBadge(method) {
            const normalized = String(method || 'UNK').trim().toUpperCase();
            const className = normalized === 'GET'
                ? 'bg-success'
                : normalized === 'POST'
                    ? 'bg-primary'
                    : normalized === 'PUT'
                        ? 'bg-warning text-dark'
                        : normalized === 'DELETE'
                            ? 'bg-danger'
                            : normalized === 'PATCH'
                                ? 'bg-info text-dark'
                                : normalized === 'HEAD'
                                    ? 'bg-dark'
                                    : 'bg-secondary';

            return `<span class="badge ${className} monitoring-uniform-badge">${normalized}</span>`;
        }

        function syncMonitoringBadgeWidths() {
            const badges = Array.from(document.querySelectorAll('.monitoring-uniform-badge'));
            if (!badges.length) {
                return;
            }

            badges.forEach(badge => {
                badge.style.width = 'auto';
            });

            let maxWidth = 0;
            badges.forEach(badge => {
                maxWidth = Math.max(maxWidth, Math.ceil(badge.getBoundingClientRect().width));
            });

            if (maxWidth <= 0) {
                return;
            }

            const width = `${maxWidth}px`;
            badges.forEach(badge => {
                badge.style.width = width;
            });
        }

        function setHealthKpi(elementId, rawStatus) {
            const element = document.getElementById(elementId);
            if (!element) {
                return;
            }

            const normalizedStatus = String(rawStatus || 'unknown').trim().toLowerCase();
            const label = normalizedStatus === 'healthy'
                ? 'Saudavel'
                : normalizedStatus === 'unhealthy'
                    ? 'Indisponivel'
                    : 'Desconhecido';

            element.classList.remove('kpi-health-healthy', 'kpi-health-unhealthy', 'kpi-health-unknown');
            if (normalizedStatus === 'healthy') {
                element.classList.add('kpi-health-healthy');
            } else if (normalizedStatus === 'unhealthy') {
                element.classList.add('kpi-health-unhealthy');
            } else {
                element.classList.add('kpi-health-unknown');
            }

            element.textContent = label;
        }

        function formatRequestOrigin(item) {
            if (!item) {
                return '-';
            }

            const host = item.host ? String(item.host).trim() : '';
            const scheme = item.scheme ? String(item.scheme).trim() : '';
            if (host && scheme) {
                return `${scheme}://${host}`;
            }

            if (host) {
                return host;
            }

            return '-';
        }

        function firstDefined() {
            for (let i = 0; i < arguments.length; i++) {
                const value = arguments[i];
                if (value !== undefined && value !== null) {
                    return value;
                }
            }

            return null;
        }

        function normalizePayloadAsJsonObject(raw, fallbackObject) {
            if (raw === undefined || raw === null) {
                return fallbackObject;
            }

            if (typeof raw === 'object') {
                return raw;
            }

            if (typeof raw === 'string') {
                const trimmed = raw.trim();
                if (!trimmed) {
                    return fallbackObject;
                }

                const parsed = parseJsonSafely(trimmed, null);
                if (parsed !== null) {
                    return parsed;
                }

                return { raw: trimmed };
            }

            return { value: raw };
        }

        function buildRequestBodyFallback(data) {
            const method = String(data?.method || '').toUpperCase();
            const requestSizeBytes = Number(data?.requestSizeBytes || 0);
            const methodUsuallyWithoutBody = method === 'GET' || method === 'HEAD' || method === 'OPTIONS' || method === 'TRACE';

            if (methodUsuallyWithoutBody) {
                return {
                    message: `Metodo ${method || 'N/A'} normalmente nao envia request body.`,
                    hint: 'Isso e esperado para requests de leitura.'
                };
            }

            if (!Number.isFinite(requestSizeBytes) || requestSizeBytes <= 0) {
                return {
                    message: 'Request sem payload para capturar.',
                    hint: 'Content-Length do request veio vazio ou igual a zero.'
                };
            }

            return {
                message: 'Request body nao foi persistido para este registro.',
                hint: 'Valide CaptureRequestBody=true, reinicie a API e confira o Content-Type do payload.'
            };
        }

        function buildResponseBodyFallback(data) {
            const responseSizeBytes = Number(data?.responseSizeBytes || 0);
            const statusCode = Number(data?.statusCode || 0);

            if (!Number.isFinite(responseSizeBytes) || responseSizeBytes <= 0) {
                return {
                    message: 'Response sem payload para capturar.',
                    hint: `Status ${statusCode || 'N/A'} pode ter retornado corpo vazio (ou Content-Length ausente).`
                };
            }

            return {
                message: 'Response body nao foi persistido para este registro.',
                hint: 'Valide CaptureResponseBody=true e se o Content-Type da resposta e textual/json.'
            };
        }

        function buildCaptureDiagnostics(data, requestBodyRaw, responseBodyRaw, contextPayload) {
            const requestBodyCaptured = hasCapturedPayload(requestBodyRaw);
            const responseBodyCaptured = hasCapturedPayload(responseBodyRaw);
            const headersCaptured = hasContextValues(contextPayload?.requestHeaders);
            const queryCaptured = hasContextValues(contextPayload?.queryString);
            const routeCaptured = hasContextValues(contextPayload?.routeValues);

            return {
                requestBodyCaptured: requestBodyCaptured,
                responseBodyCaptured: responseBodyCaptured,
                requestHeadersCaptured: headersCaptured,
                queryStringCaptured: queryCaptured,
                routeValuesCaptured: routeCaptured,
                requestSizeBytes: data?.requestSizeBytes ?? null,
                responseSizeBytes: data?.responseSizeBytes ?? null,
                method: data?.method ?? null,
                statusCode: data?.statusCode ?? null,
                environmentName: data?.environmentName ?? null,
                origin: formatRequestOrigin(data),
                notes: [
                    'GET/HEAD/OPTIONS/TRACE geralmente nao possuem request body.',
                    'Para response body, apenas conteudo textual/json e persistido.',
                    'Bodies grandes podem ser truncados conforme Monitoring:BodyCapture:MaxBodyChars.'
                ]
            };
        }

        function hasCapturedPayload(raw) {
            if (raw === undefined || raw === null) {
                return false;
            }

            if (typeof raw === 'string') {
                return raw.trim().length > 0;
            }

            if (typeof raw === 'object') {
                return Object.keys(raw).length > 0;
            }

            return true;
        }

        function hasContextValues(raw) {
            if (raw === undefined || raw === null) {
                return false;
            }

            if (typeof raw === 'string') {
                const parsed = parseJsonSafely(raw, null);
                if (parsed && typeof parsed === 'object') {
                    return Object.keys(parsed).length > 0;
                }

                return raw.trim().length > 0;
            }

            if (typeof raw === 'object') {
                return Object.keys(raw).length > 0;
            }

            return false;
        }

        function parseJsonSafely(raw, fallbackValue) {
            if (raw === undefined || raw === null) {
                return fallbackValue;
            }

            if (typeof raw !== 'string') {
                return raw;
            }

            const trimmed = raw.trim();
            if (!trimmed) {
                return fallbackValue;
            }

            try {
                return JSON.parse(trimmed);
            } catch {
                return fallbackValue;
            }
        }

        function parseUtcDate(value) {
            if (!value) {
                return new Date(NaN);
            }

            if (value instanceof Date) {
                return value;
            }

            const raw = String(value).trim();
            if (!raw) {
                return new Date(NaN);
            }

            // Quando a API envia "yyyy-MM-ddTHH:mm:ss" (sem offset), interpretamos como UTC.
            const hasTimezone = /([zZ]|[+\-]\d{2}:\d{2})$/.test(raw);
            return new Date(hasTimezone ? raw : `${raw}Z`);
        }

        function formatNumber(value) {
            return Number(value || 0).toLocaleString('pt-BR');
        }

        function formatUptimeSeconds(value) {
            const totalSeconds = Math.max(0, Math.floor(Number(value || 0)));
            if (!Number.isFinite(totalSeconds)) {
                return '-';
            }

            const days = Math.floor(totalSeconds / 86400);
            const hours = Math.floor((totalSeconds % 86400) / 3600);
            const minutes = Math.floor((totalSeconds % 3600) / 60);
            const seconds = totalSeconds % 60;

            if (days > 0) {
                return `${days}d ${hours}h ${minutes}m`;
            }

            if (hours > 0) {
                return `${hours}h ${minutes}m ${seconds}s`;
            }

            if (minutes > 0) {
                return `${minutes}m ${seconds}s`;
            }

            return `${seconds}s`;
        }

        function attachJsonCopyButton(buttonId, contentElementId) {
            const button = document.getElementById(buttonId);
            const contentElement = document.getElementById(contentElementId);
            if (!button || !contentElement) {
                return;
            }

            button.addEventListener('click', async function () {
                const payload = contentElement.textContent || '';
                const originalLabel = button.textContent;
                try {
                    await copyTextToClipboard(payload);
                    button.textContent = 'Copiado';
                    setTimeout(function () {
                        button.textContent = originalLabel;
                    }, 1200);
                } catch {
                    button.textContent = 'Falhou';
                    setTimeout(function () {
                        button.textContent = originalLabel;
                    }, 1200);
                }
            });
        }

        async function copyTextToClipboard(text) {
            const content = String(text || '');
            if (navigator.clipboard && navigator.clipboard.writeText) {
                await navigator.clipboard.writeText(content);
                return;
            }

            const textarea = document.createElement('textarea');
            textarea.value = content;
            textarea.setAttribute('readonly', 'readonly');
            textarea.style.position = 'fixed';
            textarea.style.opacity = '0';
            textarea.style.pointerEvents = 'none';
            document.body.appendChild(textarea);
            textarea.select();
            document.execCommand('copy');
            document.body.removeChild(textarea);
        }

        function setLoading(isLoading) {
            loadingAlert.classList.toggle('d-none', !isLoading);
        }

        function showError(message) {
            errorAlert.textContent = message;
            errorAlert.classList.remove('d-none');
        }

        function clearError() {
            errorAlert.textContent = '';
            errorAlert.classList.add('d-none');
        }

        function isDebugEnabled() {
            try {
                const query = new URLSearchParams(window.location.search);
                if (query.get('monitoringDebug') === '1') return true;
                return localStorage.getItem('cpm-admin-monitoring-debug') === '1';
            } catch {
                return false;
            }
        }

        function debugPayload(name, payload) {
            if (!isDebugEnabled()) return;
            try {
                console.groupCollapsed(`[AdminMonitoring][${name}]`);
                console.log(payload);
                if (payload && Array.isArray(payload.requestsSeries)) {
                    const values = payload.requestsSeries.map(x => Number(x.value || 0));
                    console.log('requestsSeries stats', {
                        points: values.length,
                        min: values.length ? Math.min(...values) : 0,
                        max: values.length ? Math.max(...values) : 0
                    });
                }
                if (payload && Array.isArray(payload.series)) {
                    const p95 = payload.series.map(x => Number(x.p95Ms || 0));
                    console.log('latencySeries p95 stats', {
                        points: p95.length,
                        min: p95.length ? Math.min(...p95) : 0,
                        max: p95.length ? Math.max(...p95) : 0
                    });
                }
                console.groupEnd();
            } catch {
                // no-op
            }
        }

        async function initRealtime() {
            if (!monitoringHubUrl || !monitoringHubToken || !window.signalR) {
                return;
            }

            const connection = new signalR.HubConnectionBuilder()
                .withUrl(monitoringHubUrl, {
                    accessTokenFactory: function () { return monitoringHubToken; }
                })
                .withAutomaticReconnect()
                .build();

            connection.on('MonitoringUpdated', function (payload) {
                const source = payload && payload.source ? String(payload.source) : 'signalr';
                scheduleIncrementalRefresh(source);
            });

            connection.onreconnected(function () {
                connection.invoke('JoinAdminMonitoringGroup').catch(console.error);
                scheduleIncrementalRefresh('reconnected', 0);
            });

            try {
                await connection.start();
                await connection.invoke('JoinAdminMonitoringGroup');
            } catch (error) {
                console.warn('Falha ao conectar no hub de monitoramento em tempo real.', error);
            }
        }

        restoreRefreshInterval();
        restoreAutoRefreshPaused();
        loadAll();
        initRealtime().catch(console.error);
    })();
