(function () {
    const config = window.cpmAdminLoadTests || {};
    const runsUrl = config.runsUrl || "";
    const runDetailsUrl = config.runDetailsUrl || "";
    const initialError = config.initialError || "";

    const state = {
        page: Number(config.initialPage || 1),
        pageSize: Number(config.initialPageSize || 20),
        total: 0
    };

    const errorAlert = document.getElementById("loadTestsErrorAlert");
    const loadingAlert = document.getElementById("loadTestsLoadingAlert");
    const detailsModalElement = document.getElementById("loadTestDetailsModal");
    const filtersForm = document.getElementById("loadTestFiltersForm");
    const refreshButton = document.getElementById("btnRefreshLoadTests");
    const prevPageButton = document.getElementById("btnPrevPage");
    const nextPageButton = document.getElementById("btnNextPage");
    const pageSizeSelect = document.getElementById("pageSize");

    if (!runsUrl || !runDetailsUrl || !errorAlert || !loadingAlert || !detailsModalElement || !filtersForm || !refreshButton || !prevPageButton || !nextPageButton || !pageSizeSelect || !window.bootstrap) {
        return;
    }

    const detailsModal = new bootstrap.Modal(detailsModalElement);

    filtersForm.addEventListener("submit", function (e) {
        e.preventDefault();
        state.page = 1;
        loadRuns();
    });
    refreshButton.addEventListener("click", loadRuns);
    prevPageButton.addEventListener("click", function () {
        if (state.page > 1) {
            state.page--;
            loadRuns();
        }
    });
    nextPageButton.addEventListener("click", function () {
        const totalPages = Math.max(1, Math.ceil(state.total / state.pageSize));
        if (state.page < totalPages) {
            state.page++;
            loadRuns();
        }
    });
    pageSizeSelect.addEventListener("change", function () {
        state.pageSize = Number(pageSizeSelect.value || 20);
        state.page = 1;
        loadRuns();
    });

    async function loadRuns() {
        setLoading(true);
        clearError();
        try {
            const query = new URLSearchParams();
            appendOptional(query, "scenario", document.getElementById("scenario").value);
            appendOptional(query, "fromUtc", normalizeDateTimeLocalInput(document.getElementById("fromUtc").value));
            appendOptional(query, "toUtc", normalizeDateTimeLocalInput(document.getElementById("toUtc").value));
            appendOptional(query, "search", document.getElementById("search").value);
            query.set("page", String(state.page));
            query.set("pageSize", String(state.pageSize));

            const data = await apiGet(runsUrl, query);
            state.total = Number(data.total || 0);
            renderRuns(data.items || []);
            const totalPages = Math.max(1, Math.ceil(state.total / state.pageSize));
            document.getElementById("loadTestsPaginationText").textContent = `Pagina ${state.page} de ${totalPages} | Total ${state.total}`;
        } catch (err) {
            showError(err.message || "Falha ao carregar runs de carga.");
        } finally {
            setLoading(false);
        }
    }

    function renderRuns(items) {
        const tbody = document.getElementById("loadTestsBody");
        tbody.innerHTML = "";

        if (!items.length) {
            tbody.innerHTML = '<tr><td colspan="10" class="text-center text-muted py-3">Nenhum run encontrado para os filtros informados.</td></tr>';
            return;
        }

        items.forEach(function (item) {
            const tr = document.createElement("tr");
            tr.innerHTML = `
                <td>${toLocal(item.startedAtUtc)}</td>
                <td><span class="badge bg-primary-subtle text-primary">${escapeHtml(item.scenario || "-")}</span></td>
                <td><code>${escapeHtml(item.externalRunId || "-")}</code></td>
                <td class="text-truncate" style="max-width:260px;" title="${escapeHtml(item.baseUrl || "-")}">${escapeHtml(item.baseUrl || "-")}</td>
                <td class="text-end">${formatNumber(item.totalRequests)}</td>
                <td class="text-end">${formatPercent(item.errorRatePercent)}</td>
                <td class="text-end">${formatMs(item.p95LatencyMs)}</td>
                <td class="text-end">${formatNumber(item.rpsAvg)}</td>
                <td>${escapeHtml(item.source || "-")}</td>
                <td><button type="button" class="btn btn-sm btn-outline-primary">Detalhes</button></td>`;

            tr.querySelector("button").addEventListener("click", function () {
                loadRunDetails(item.id);
            });
            tbody.appendChild(tr);
        });
    }

    async function loadRunDetails(runId) {
        clearError();
        try {
            const query = new URLSearchParams();
            query.set("id", runId);
            const data = await apiGet(runDetailsUrl, query);
            renderRunDetails(data);
            detailsModal.show();
        } catch (err) {
            showError(err.message || "Falha ao carregar detalhe do run.");
        }
    }

    function renderRunDetails(data) {
        document.getElementById("detailTotalRequests").textContent = formatNumber(data.totalRequests);
        document.getElementById("detailFailedRequests").textContent = formatNumber(data.failedRequests);
        document.getElementById("detailErrorRate").textContent = formatPercent(data.errorRatePercent);
        document.getElementById("detailRps").textContent = `${formatNumber(data.rpsAvg)} / ${formatNumber(data.rpsPeak)}`;
        document.getElementById("detailP95").textContent = formatMs(data.p95LatencyMs);
        document.getElementById("detailDuration").textContent = `${formatSeconds(data.durationSeconds)}s`;

        document.getElementById("detailHeaderText").textContent =
            `${data.scenario} | runId=${data.externalRunId} | ${toLocal(data.startedAtUtc)} -> ${toLocal(data.finishedAtUtc)} | source=${data.source}`;

        const topEndpointsBody = document.getElementById("detailTopEndpointsBody");
        topEndpointsBody.innerHTML = "";
        const topEndpoints = data.topEndpointsByHits || [];
        if (!topEndpoints.length) {
            topEndpointsBody.innerHTML = '<tr><td colspan="4" class="text-center text-muted">Sem dados.</td></tr>';
        } else {
            topEndpoints.forEach(function (item) {
                const tr = document.createElement("tr");
                tr.innerHTML = `
                    <td>${escapeHtml(item.endpoint || "-")}</td>
                    <td class="text-end">${formatNumber(item.hits)}</td>
                    <td class="text-end">${formatMs(item.p95LatencyMs)}</td>
                    <td class="text-end">${formatPercent(item.errorRatePercent)}</td>`;
                topEndpointsBody.appendChild(tr);
            });
        }

        const topErrorsBody = document.getElementById("detailTopErrorsBody");
        topErrorsBody.innerHTML = "";
        const topErrors = data.topErrors || [];
        if (!topErrors.length) {
            topErrorsBody.innerHTML = '<tr><td colspan="2" class="text-center text-muted">Sem erros catalogados.</td></tr>';
        } else {
            topErrors.forEach(function (item) {
                const endpoints = (item.endpoints || []).join(", ");
                const tr = document.createElement("tr");
                tr.innerHTML = `
                    <td>${escapeHtml(item.message || "-")}<div class="small text-muted">${escapeHtml(endpoints)}</div></td>
                    <td class="text-end">${formatNumber(item.count)}</td>`;
                topErrorsBody.appendChild(tr);
            });
        }

        const failuresBody = document.getElementById("detailFailureSamplesBody");
        failuresBody.innerHTML = "";
        const failures = data.failureSamples || [];
        if (!failures.length) {
            failuresBody.innerHTML = '<tr><td colspan="6" class="text-center text-muted">Sem amostras de falha.</td></tr>';
        } else {
            failures.forEach(function (item) {
                const tr = document.createElement("tr");
                tr.innerHTML = `
                    <td>${toLocal(item.timestampUtc)}</td>
                    <td>${escapeHtml(item.method || "-")}</td>
                    <td class="text-truncate" style="max-width:280px;" title="${escapeHtml(item.path || "-")}">${escapeHtml(item.path || "-")}</td>
                    <td>${item.statusCode ?? "-"}</td>
                    <td><code>${escapeHtml(item.correlationId || "-")}</code></td>
                    <td>${escapeHtml(item.errorMessage || item.errorType || "-")}</td>`;
                failuresBody.appendChild(tr);
            });
        }

        const rawJson = safePrettyJson(data.rawReportJson);
        document.getElementById("detailRawJson").textContent = rawJson;
    }

    async function apiGet(url, query) {
        const response = await fetch(`${url}?${query.toString()}`, { credentials: "same-origin" });
        const payload = await response.json();
        if (!response.ok || !payload.success) {
            throw new Error(payload.errorMessage || "Erro ao consultar API admin.");
        }
        return payload.data;
    }

    function appendOptional(query, key, value) {
        if (value && String(value).trim().length > 0) {
            query.set(key, String(value).trim());
        }
    }

    function normalizeDateTimeLocalInput(value) {
        if (!value) {
            return null;
        }
        return `${value}:00Z`;
    }

    function toLocal(value) {
        if (!value) {
            return "-";
        }

        const raw = String(value).trim();
        const hasTimezone = /([zZ]|[+\-]\d{2}:\d{2})$/.test(raw);
        const parsed = new Date(hasTimezone ? raw : `${raw}Z`);
        if (Number.isNaN(parsed.getTime())) {
            return "-";
        }
        return parsed.toLocaleString("pt-BR");
    }

    function formatNumber(value) {
        return Number(value || 0).toLocaleString("pt-BR", { maximumFractionDigits: 2 });
    }

    function formatPercent(value) {
        return `${Number(value || 0).toLocaleString("pt-BR", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}%`;
    }

    function formatMs(value) {
        return `${Number(value || 0).toLocaleString("pt-BR", { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ms`;
    }

    function formatSeconds(value) {
        return Number(value || 0).toLocaleString("pt-BR", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    function safePrettyJson(raw) {
        if (!raw) {
            return "{}";
        }
        if (typeof raw === "object") {
            return JSON.stringify(raw, null, 2);
        }
        try {
            return JSON.stringify(JSON.parse(String(raw)), null, 2);
        } catch {
            return String(raw);
        }
    }

    function escapeHtml(value) {
        return String(value ?? "")
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll("\"", "&quot;")
            .replaceAll("'", "&#39;");
    }

    function setLoading(isLoading) {
        loadingAlert.classList.toggle("d-none", !isLoading);
    }

    function showError(message) {
        errorAlert.textContent = message;
        errorAlert.classList.remove("d-none");
    }

    function clearError() {
        errorAlert.textContent = "";
        errorAlert.classList.add("d-none");
    }

    if (initialError) {
        showError(initialError);
    }

    loadRuns();
})();
