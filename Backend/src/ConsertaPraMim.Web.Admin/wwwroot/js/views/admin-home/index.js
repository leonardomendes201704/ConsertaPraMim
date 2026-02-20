(function () {
    const config = window.adminHomeConfig || {};
    const snapshotUrl = config.snapshotUrl || "";
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
    const pollIntervalMs = 30000;

    if (!snapshotUrl || !form || !refreshButton || !loadingState || !errorState || !dashboardContent || !noShowContent || !noShowErrorState || !emptyState || !lastUpdatedLabel) {
        return;
    }

            let requestInFlight = false;
            let pollHandle = null;

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

            function resolveEventBadge(type) {
                const normalized = String(type ?? "").toLowerCase();
                if (normalized === "request") return "bg-primary";
                if (normalized === "proposal") return "bg-success";
                if (normalized === "chat") return "bg-info text-dark";
                return "bg-dark";
            }

            function updateKpis(data) {
                const onlineClients = Number(data.onlineClients ?? 0);
                const onlineProviders = Number(data.onlineProviders ?? 0);

                document.querySelector("[data-kpi-total-users]").textContent = formatNumber(data.totalUsers);
                document.querySelector("[data-kpi-active-users]").textContent = formatNumber(data.activeUsers);
                document.querySelector("[data-kpi-online-clients]").textContent = formatNumber(onlineClients);
                document.querySelector("[data-kpi-online-providers]").textContent = formatNumber(onlineProviders);
                document.querySelector("[data-kpi-online-total]").textContent = formatNumber(onlineClients + onlineProviders);
                document.querySelector("[data-kpi-active-requests]").textContent = formatNumber(data.activeRequests);
                document.querySelector("[data-kpi-requests-period]").textContent = formatNumber(data.requestsInPeriod);
                document.querySelector("[data-kpi-accepted-proposals]").textContent = formatNumber(data.acceptedProposalsInPeriod);
                document.querySelector("[data-kpi-proposals-period]").textContent = formatNumber(data.proposalsInPeriod);
                document.querySelector("[data-kpi-active-chat]").textContent = formatNumber(data.activeChatConversationsLast24h);
                document.querySelector("[data-kpi-credits-granted]").textContent = formatCurrency(data.creditsGrantedInPeriod);
                document.querySelector("[data-kpi-credits-consumed]").textContent = formatCurrency(data.creditsConsumedInPeriod);
                document.querySelector("[data-kpi-credits-open]").textContent = formatCurrency(data.creditsOpenBalance);
                document.querySelector("[data-kpi-credits-expiring]").textContent = formatCurrency(data.creditsExpiringInNext30Days);
                document.querySelector("[data-kpi-appointment-confirmation-sla]").textContent = formatPercent(data.appointmentConfirmationInSlaRatePercent);
                document.querySelector("[data-kpi-appointment-reschedule-rate]").textContent = formatPercent(data.appointmentRescheduleRatePercent);
                document.querySelector("[data-kpi-appointment-cancellation-rate]").textContent = formatPercent(data.appointmentCancellationRatePercent);
                document.querySelector("[data-kpi-reminder-failure-rate]").textContent = formatPercent(data.reminderFailureRatePercent);
                document.querySelector("[data-kpi-reminder-attempts]").textContent = formatNumber(data.reminderAttemptsInPeriod);
                document.querySelector("[data-kpi-reminder-failures]").textContent = formatNumber(data.reminderFailuresInPeriod);
            }

            function updateStatusWidget(data) {
                const list = document.getElementById("request-status-list");
                const statuses = Array.isArray(data.requestsByStatus) ? data.requestsByStatus : [];

                if (statuses.length === 0) {
                    list.innerHTML = "<li class=\"text-muted\">Sem dados de status para o filtro selecionado.</li>";
                    return;
                }

                list.innerHTML = statuses
                    .map(status => `
                        <li class="d-flex justify-content-between align-items-center">
                            <span class="text-muted">${escapeHtml(status.status)}</span>
                            <span class="badge bg-secondary">${formatNumber(status.count)}</span>
                        </li>`)
                    .join("");
            }

            function updateCategoryWidget(data) {
                const list = document.getElementById("request-category-list");
                const categories = Array.isArray(data.requestsByCategory) ? data.requestsByCategory : [];

                if (categories.length === 0) {
                    list.innerHTML = "<li class=\"text-muted\">Sem dados de categoria para o filtro selecionado.</li>";
                    return;
                }

                list.innerHTML = categories
                    .map(category => `
                        <li class="d-flex justify-content-between align-items-center">
                            <span class="text-muted">${escapeHtml(category.category)}</span>
                            <span class="badge bg-primary">${formatNumber(category.count)}</span>
                        </li>`)
                    .join("");
            }

            function updateOperationalWidget(data) {
                const list = document.getElementById("operational-status-list");
                const items = Array.isArray(data.appointmentsByOperationalStatus) ? data.appointmentsByOperationalStatus : [];

                if (items.length === 0) {
                    list.innerHTML = "<li class=\"text-muted\">Sem dados operacionais para o filtro selecionado.</li>";
                    return;
                }

                list.innerHTML = items
                    .map(item => `
                        <li class="d-flex justify-content-between align-items-center">
                            <span class="text-muted">${escapeHtml(item.status)}</span>
                            <span class="badge bg-dark">${formatNumber(item.count)}</span>
                        </li>`)
                    .join("");
            }

            function formatRating(value) {
                return Number(value ?? 0).toLocaleString("pt-BR", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
            }

            function updateReviewReputationWidgets(data) {
                const providerBody = document.getElementById("provider-review-ranking-body");
                const providerRows = Array.isArray(data.providerReviewRanking) ? data.providerReviewRanking : [];
                if (providerBody) {
                    if (!providerRows.length) {
                        providerBody.innerHTML = "<tr><td colspan=\"3\" class=\"text-center text-muted py-3\">Sem ranking de prestadores para o periodo.</td></tr>";
                    } else {
                        providerBody.innerHTML = providerRows
                            .map(item => `
                                <tr>
                                    <td class="fw-semibold">${escapeHtml(item.userName)}</td>
                                    <td class="text-end">${formatRating(item.averageRating)}</td>
                                    <td class="text-end">${formatNumber(item.totalReviews)}</td>
                                </tr>`)
                            .join("");
                    }
                }

                const clientBody = document.getElementById("client-review-ranking-body");
                const clientRows = Array.isArray(data.clientReviewRanking) ? data.clientReviewRanking : [];
                if (clientBody) {
                    if (!clientRows.length) {
                        clientBody.innerHTML = "<tr><td colspan=\"3\" class=\"text-center text-muted py-3\">Sem ranking de clientes para o periodo.</td></tr>";
                    } else {
                        clientBody.innerHTML = clientRows
                            .map(item => `
                                <tr>
                                    <td class="fw-semibold">${escapeHtml(item.userName)}</td>
                                    <td class="text-end">${formatRating(item.averageRating)}</td>
                                    <td class="text-end">${formatNumber(item.totalReviews)}</td>
                                </tr>`)
                            .join("");
                    }
                }

                const outlierList = document.getElementById("review-outlier-list");
                const outlierRows = Array.isArray(data.reviewOutliers) ? data.reviewOutliers : [];
                if (outlierList) {
                    if (!outlierRows.length) {
                        outlierList.innerHTML = "<li class=\"text-muted\">Nenhum outlier de reputacao no periodo filtrado.</li>";
                    } else {
                        outlierList.innerHTML = outlierRows
                            .map(item => `
                                <li class="d-flex justify-content-between align-items-center gap-2">
                                    <div>
                                        <div class="fw-semibold">${escapeHtml(item.userName)} <span class="text-muted small">(${escapeHtml(item.userRole)})</span></div>
                                        <div class="small text-muted">${escapeHtml(item.reason)}</div>
                                    </div>
                                    <div class="text-end">
                                        <span class="badge bg-danger-subtle text-danger">${Number(item.oneStarRatePercent ?? 0).toLocaleString("pt-BR", { minimumFractionDigits: 1, maximumFractionDigits: 1 })}% 1 estrela</span>
                                    </div>
                                </li>`)
                            .join("");
                    }
                }
            }

            function updatePaymentFailureWidgets(data) {
                const providerBody = document.getElementById("payment-failure-provider-body");
                const providerRows = Array.isArray(data.paymentFailuresByProvider) ? data.paymentFailuresByProvider : [];

                if (providerBody) {
                    if (providerRows.length === 0) {
                        providerBody.innerHTML = "<tr id=\"payment-failure-provider-empty\"><td colspan=\"4\" class=\"text-center text-muted py-3\">Sem falhas de pagamento no periodo selecionado.</td></tr>";
                    } else {
                        providerBody.innerHTML = providerRows
                            .map(item => `
                                <tr>
                                    <td class="fw-semibold">${escapeHtml(item.providerName ?? "Prestador")}</td>
                                    <td class="text-end"><span class="badge bg-danger-subtle text-danger">${formatNumber(item.failedTransactions)}</span></td>
                                    <td class="text-end">${formatNumber(item.affectedRequests)}</td>
                                    <td class="text-end text-muted">${formatDateTime(item.lastFailureAtUtc)}</td>
                                </tr>`)
                            .join("");
                    }
                }

                const channelList = document.getElementById("payment-failure-channel-list");
                const channelRows = Array.isArray(data.paymentFailuresByChannel) ? data.paymentFailuresByChannel : [];
                if (channelList) {
                    if (channelRows.length === 0) {
                        channelList.innerHTML = "<li class=\"text-muted\">Sem falhas por canal no periodo selecionado.</li>";
                    } else {
                        channelList.innerHTML = channelRows
                            .map(item => `
                                <li class="d-flex justify-content-between align-items-center">
                                    <span class="text-muted">${escapeHtml(item.status)}</span>
                                    <span class="badge bg-danger">${formatNumber(item.count)}</span>
                                </li>`)
                            .join("");
                    }
                }

                const totalFailuresEl = document.querySelector("[data-kpi-payment-failures]");
                if (totalFailuresEl) {
                    const totalFailures = channelRows.reduce((sum, item) => sum + Number(item.count ?? 0), 0);
                    totalFailuresEl.textContent = formatNumber(totalFailures);
                }
            }

            function updateRevenueWidget(data) {
                const revenueBody = document.getElementById("subscription-revenue-body");
                const rows = Array.isArray(data.revenueByPlan) ? data.revenueByPlan : [];
                const payingProviders = Number(data.payingProviders ?? 0);

                document.querySelector("[data-kpi-revenue-total]").textContent = formatCurrency(data.monthlySubscriptionRevenue);
                document.querySelector("[data-kpi-paying-providers]").textContent = formatNumber(payingProviders);

                if (rows.length === 0) {
                    revenueBody.innerHTML = "<tr><td colspan=\"4\" class=\"text-center text-muted py-3\">Sem assinaturas pagantes no periodo atual.</td></tr>";
                    return;
                }

                revenueBody.innerHTML = rows
                    .map(row => `
                        <tr>
                            <td class="fw-semibold">${escapeHtml(row.plan)}</td>
                            <td class="text-end">${formatNumber(row.providers)}</td>
                            <td class="text-end">${formatCurrency(row.unitMonthlyPrice)}</td>
                            <td class="text-end fw-semibold">${formatCurrency(row.totalMonthlyRevenue)}</td>
                        </tr>`)
                    .join("");
            }

            function updateEvents(data) {
                const body = document.getElementById("recent-events-body");
                const events = Array.isArray(data.recentEvents) ? data.recentEvents : [];

                if (events.length === 0) {
                    body.innerHTML = "<tr id=\"events-empty-row\"><td colspan=\"4\" class=\"text-center text-muted py-4\">Nenhum evento encontrado para os filtros selecionados.</td></tr>";
                    emptyState.classList.remove("d-none");
                    return;
                }

                emptyState.classList.add("d-none");
                body.innerHTML = events
                    .map(eventItem => `
                        <tr>
                            <td><span class="badge ${resolveEventBadge(eventItem.type)}">${escapeHtml(eventItem.type)}</span></td>
                            <td class="fw-semibold">${escapeHtml(eventItem.title)}</td>
                            <td class="text-muted">${escapeHtml(eventItem.description ?? "")}</td>
                            <td class="text-muted">${formatDateTime(eventItem.createdAt)}</td>
                        </tr>`)
                    .join("");
            }

            function updateRangeLabel(data) {
                const rangeLabel = document.getElementById("range-label");
                rangeLabel.textContent = `${formatDateTime(data.fromUtc)} ate ${formatDateTime(data.toUtc)}`;
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

                const setText = (selector, value) => {
                    const element = document.querySelector(selector);
                    if (element) {
                        element.textContent = value;
                    }
                };

                setText("[data-no-show-rate]", `${Number(data.noShowRatePercent ?? 0).toLocaleString("pt-BR", { minimumFractionDigits: 1, maximumFractionDigits: 1 })}%`);
                setText("[data-no-show-count]", formatNumber(data.noShowAppointments));
                setText("[data-attendance-rate]", `${Number(data.attendanceRatePercent ?? 0).toLocaleString("pt-BR", { minimumFractionDigits: 1, maximumFractionDigits: 1 })}%`);
                setText("[data-attendance-count]", formatNumber(data.attendanceAppointments));
                setText("[data-dual-presence-rate]", `${Number(data.dualPresenceConfirmationRatePercent ?? 0).toLocaleString("pt-BR", { minimumFractionDigits: 1, maximumFractionDigits: 1 })}%`);
                setText("[data-dual-presence-count]", formatNumber(data.dualPresenceConfirmedAppointments));
                setText("[data-high-risk-count]", formatNumber(data.highRiskAppointments));
                setText("[data-high-risk-conversion]", `${Number(data.highRiskConversionRatePercent ?? 0).toLocaleString("pt-BR", { minimumFractionDigits: 1, maximumFractionDigits: 1 })}%`);
                setText("[data-open-queue-count]", formatNumber(data.openQueueItems));
                setText("[data-open-queue-age]", Number(data.averageQueueAgeMinutes ?? 0).toLocaleString("pt-BR", { minimumFractionDigits: 1, maximumFractionDigits: 1 }));

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
            }

            function updateLastUpdated() {
                lastUpdatedLabel.textContent = `Atualizado em ${formatDateTime(new Date().toISOString())}`;
            }

            function updateDashboard(data) {
                updateKpis(data);
                updateRevenueWidget(data);
                updateStatusWidget(data);
                updateCategoryWidget(data);
                updateOperationalWidget(data);
                updateReviewReputationWidgets(data);
                updatePaymentFailureWidgets(data);
                updateEvents(data);
                updateRangeLabel(data);
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

                if (filtersDrawer && window.bootstrap?.Offcanvas) {
                    const offcanvasInstance = window.bootstrap.Offcanvas.getInstance(filtersDrawer);
                    if (offcanvasInstance) {
                        offcanvasInstance.hide();
                    }
                }
            });

            refreshButton.addEventListener("click", function () {
                fetchDashboard({ showLoading: true, updateUrl: false });
            });

            function startPolling() {
                stopPolling();
                pollHandle = setInterval(function () {
                    if (document.hidden) {
                        return;
                    }
                    fetchDashboard({ showLoading: false, updateUrl: false });
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
                }
            });

            startPolling();
        })();
