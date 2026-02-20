(function () {
        const config = window.providerServiceRequestsAgendaConfig || {};
        const agendaOpenRequestId = String(config.agendaOpenRequestId || "");
        const agendaModalErrorRequestId = String(config.agendaModalErrorRequestId || "");
        const agendaModalErrorMessage = config.agendaModalErrorMessage || "";
        const providerBaseLat = Number(config.providerBaseLat);
        const providerBaseLng = Number(config.providerBaseLng);
        const drivingRouteUrl = config.drivingRouteUrl || "";
        const drivingDistanceCache = new Map();
        const toggles = document.querySelectorAll(".js-calendar-toggle");
        const weekView = document.getElementById("providerCalendarWeek");
        const monthView = document.getElementById("providerCalendarMonth");

        function formatDistanceKm(km) {
            return new Intl.NumberFormat("pt-BR", {
                minimumFractionDigits: 1,
                maximumFractionDigits: 1
            }).format(km);
        }

        function renderAgendaDistance(cell, km, source) {
            if (!cell) return;
            const prefix = String(cell.dataset.distancePrefix || "Distancia da base:").trim();
            if (Number.isFinite(km) && km > 0) {
                const suffix = source === "route" ? "" : " (estimada)";
                cell.textContent = `${prefix} ${formatDistanceKm(km)} km${suffix}`;
                return;
            }

            cell.textContent = `${prefix} N/D`;
        }

        function fetchDrivingDistanceKm(requestLat, requestLng) {
            const key = `${requestLat.toFixed(6)}|${requestLng.toFixed(6)}`;
            const cachedPromise = drivingDistanceCache.get(key);
            if (cachedPromise) {
                return cachedPromise;
            }

            const queryString = new URLSearchParams({
                providerLat: String(providerBaseLat),
                providerLng: String(providerBaseLng),
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

            drivingDistanceCache.set(key, promise);
            return promise;
        }

        async function enhanceAgendaDistances(rootElement) {
            if (!Number.isFinite(providerBaseLat) || !Number.isFinite(providerBaseLng) || !drivingRouteUrl) {
                return;
            }

            const scope = rootElement || document;
            const cells = Array
                .from(scope.querySelectorAll("[data-agenda-distance-cell='1']"))
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
                        renderAgendaDistance(cell, estimatedKm, "estimated");
                        cell.dataset.distanceResolved = "1";
                        continue;
                    }

                    const routeKm = await fetchDrivingDistanceKm(requestLat, requestLng);
                    if (Number.isFinite(routeKm) && routeKm > 0) {
                        renderAgendaDistance(cell, routeKm, "route");
                    } else {
                        renderAgendaDistance(cell, estimatedKm, "estimated");
                    }
                    cell.dataset.distanceResolved = "1";
                }
            }

            const workers = Array
                .from({ length: Math.min(maxConcurrent, cells.length) }, () => worker());

            await Promise.all(workers);
        }

        toggles.forEach(button => {
            button.addEventListener("click", function () {
                const target = this.dataset.calendarTarget;
                const showWeek = target === "week";

                if (weekView) {
                    weekView.classList.toggle("d-none", !showWeek);
                }

                if (monthView) {
                    monthView.classList.toggle("d-none", showWeek);
                }

                toggles.forEach(item => {
                    const isActive = item === this;
                    item.classList.toggle("active", isActive);
                    item.classList.toggle("btn-primary", isActive);
                    item.classList.toggle("btn-outline-primary", !isActive);
                });
            });
        });

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

        const detailsTemplateContainer = document.getElementById("agendaDetailsTemplates");
        const appointmentModalElement = document.getElementById("agendaAppointmentModal");
        const appointmentModalBody = document.getElementById("agendaAppointmentModalBody");
        const appointmentModalFooter = document.getElementById("agendaAppointmentModalFooter");
        const appointmentModalTitle = document.getElementById("agendaAppointmentModalTitle");
        const appointmentModalSubtitle = document.getElementById("agendaAppointmentModalSubtitle");
        const appointmentModalCategoryIcon = document.getElementById("agendaAppointmentModalCategoryIcon");
        const appointmentModalInstance = appointmentModalElement && window.bootstrap
            ? new window.bootstrap.Modal(appointmentModalElement)
            : null;

        function normalizeCategoryText(value) {
            return String(value || "")
                .normalize("NFD")
                .replace(/[\u0300-\u036f]/g, "")
                .trim()
                .toLowerCase();
        }

        function resolveCategoryIcon(categoryName) {
            const normalized = normalizeCategoryText(categoryName);
            if (normalized.includes("hidraul") || normalized.includes("plumbing")) return "fa-faucet-drip";
            if (normalized.includes("eletric") || normalized.includes("electrical")) return "fa-bolt";
            if (normalized.includes("eletron") || normalized.includes("electronics")) return "fa-microchip";
            if (normalized.includes("eletrodomest") || normalized.includes("appliances")) return "fa-blender";
            if (normalized.includes("pintur")) return "fa-paint-roller";
            if (normalized.includes("limpez") || normalized.includes("cleaning")) return "fa-broom";
            if (normalized.includes("jardin")) return "fa-seedling";
            if (normalized.includes("alvenar") || normalized.includes("pedrei") || normalized.includes("masonry")) return "fa-hammer";
            if (normalized.includes("ar condicionado") || normalized.includes("arcond") || normalized.includes("climat")) return "fa-wind";
            if (normalized.includes("mudanc")) return "fa-truck";
            if (normalized.includes("montagem") || normalized.includes("moveis") || normalized.includes("mobili")) return "fa-screwdriver-wrench";
            if (normalized.includes("outro") || normalized.includes("other")) return "fa-circle-question";
            return "fa-screwdriver-wrench";
        }

        function resolveAppointmentStatusMeta(status) {
            const normalized = String(status || "").trim().toLowerCase();
            switch (normalized) {
                case "pendingproviderconfirmation":
                    return { label: "Aguardando confirmacao", cssClass: "bg-warning-subtle text-warning-emphasis" };
                case "confirmed":
                    return { label: "Confirmado", cssClass: "bg-success-subtle text-success-emphasis" };
                case "arrived":
                    return { label: "Chegada registrada", cssClass: "bg-info-subtle text-info-emphasis" };
                case "inprogress":
                    return { label: "Em atendimento", cssClass: "bg-primary-subtle text-primary-emphasis" };
                case "reschedulerequestedbyclient":
                    return { label: "Reagendamento solicitado pelo cliente", cssClass: "bg-info-subtle text-info-emphasis" };
                case "reschedulerequestedbyprovider":
                    return { label: "Reagendamento solicitado pelo prestador", cssClass: "bg-primary-subtle text-primary-emphasis" };
                case "rescheduleconfirmed":
                    return { label: "Reagendado confirmado", cssClass: "bg-primary-subtle text-primary-emphasis" };
                case "rejectedbyprovider":
                    return { label: "Recusado pelo prestador", cssClass: "bg-danger-subtle text-danger-emphasis" };
                case "cancelledbyclient":
                    return { label: "Cancelado pelo cliente", cssClass: "bg-secondary-subtle text-secondary-emphasis" };
                case "cancelledbyprovider":
                    return { label: "Cancelado pelo prestador", cssClass: "bg-secondary-subtle text-secondary-emphasis" };
                case "expiredwithoutprovideraction":
                    return { label: "Expirado sem confirmacao", cssClass: "bg-secondary-subtle text-secondary-emphasis" };
                case "completed":
                    return { label: "Concluido", cssClass: "bg-success-subtle text-success-emphasis" };
                default:
                    return { label: "Em processamento", cssClass: "bg-secondary-subtle text-secondary-emphasis" };
            }
        }

        function clearModalFeedback() {
            if (!appointmentModalBody) return;
            appointmentModalBody.querySelectorAll("[data-agenda-modal-feedback='1']").forEach(element => element.remove());
        }

        function showModalFeedback(message, kind) {
            if (!appointmentModalBody || !message) return;

            clearModalFeedback();
            const alert = document.createElement("div");
            alert.setAttribute("data-agenda-modal-feedback", "1");
            alert.className = kind === "success"
                ? "alert alert-success border-0 shadow-sm mb-3"
                : "alert alert-danger border-0 shadow-sm mb-3";

            const icon = document.createElement("i");
            icon.className = kind === "success"
                ? "fas fa-check-circle me-2"
                : "fas fa-triangle-exclamation me-2";
            alert.appendChild(icon);
            alert.appendChild(document.createTextNode(message));

            appointmentModalBody.insertBefore(alert, appointmentModalBody.firstChild);
        }

        function bindOperationalStatusForm(form) {
            if (!form || form.dataset.ajaxBound === "1") return;
            form.dataset.ajaxBound = "1";

            form.addEventListener("submit", async function (event) {
                event.preventDefault();

                if (form.dataset.submitting === "1") return;
                form.dataset.submitting = "1";

                const submitButton = form.querySelector("button[type='submit']");
                const originalButtonContent = submitButton ? submitButton.innerHTML : "";
                if (submitButton) {
                    submitButton.disabled = true;
                    submitButton.innerHTML = "<span class='spinner-border spinner-border-sm me-2' role='status' aria-hidden='true'></span>Atualizando...";
                }

                try {
                    const response = await fetch(form.action, {
                        method: (form.method || "POST").toUpperCase(),
                        body: new FormData(form),
                        headers: {
                            "X-Requested-With": "XMLHttpRequest",
                            "Accept": "application/json"
                        }
                    });

                    const raw = await response.text();
                    let payload = null;
                    if (raw) {
                        try {
                            payload = JSON.parse(raw);
                        } catch {
                            payload = null;
                        }
                    }

                    if (!response.ok || !payload || payload.success !== true) {
                        showModalFeedback(
                            payload && payload.message
                                ? payload.message
                                : "Nao foi possivel atualizar o status operacional.",
                            "danger");
                        return;
                    }

                    const card = form.closest(".card");
                    const statusBadge = card ? card.querySelector(".js-appointment-status-badge") : null;
                    if (statusBadge) {
                        const statusValue = payload.appointmentStatus || statusBadge.dataset.status || "";
                        const statusMeta = resolveAppointmentStatusMeta(statusValue);
                        statusBadge.className = `badge rounded-pill js-appointment-status-badge ${statusMeta.cssClass}`;
                        statusBadge.textContent = statusMeta.label;
                        statusBadge.dataset.status = statusValue;
                    }

                    showModalFeedback(payload.message || "Status operacional atualizado com sucesso.", "success");
                } catch {
                    showModalFeedback("Falha de conexao ao atualizar status operacional.", "danger");
                } finally {
                    form.dataset.submitting = "0";
                    if (submitButton) {
                        submitButton.disabled = false;
                        submitButton.innerHTML = originalButtonContent;
                    }
                }
            });
        }

        function openAppointmentModal(trigger) {
            if (!trigger) return;

            const requestId = trigger.dataset.requestId || "";
            const requestShort = (trigger.dataset.requestShort || "").trim();
            const category = trigger.dataset.appointmentCategory || "Servico";
            const href = trigger.getAttribute("href") || "";

            if (!detailsTemplateContainer || !appointmentModalInstance || !appointmentModalBody || !requestId) {
                if (href) {
                    window.location.href = href;
                }
                return;
            }

            const sourceCard = detailsTemplateContainer.querySelector(`[data-agenda-detail-card="${requestId}"]`);
            if (!sourceCard) {
                if (href) {
                    window.location.href = href;
                }
                return;
            }

            appointmentModalBody.innerHTML = "";
            clearModalFeedback();
            if (appointmentModalFooter) {
                appointmentModalFooter.innerHTML = "";
                appointmentModalFooter.classList.add("d-none");
            }
            const clonedCard = sourceCard.cloneNode(true);
            clonedCard.classList.remove("h-100");
            const clonedHeader = clonedCard.querySelector(".card-header");
            if (clonedHeader) {
                clonedHeader.remove();
            }
            const clonedTitle = clonedCard.querySelector(".card-body > h5");
            if (clonedTitle) {
                clonedTitle.remove();
            }
            const clonedDescription = clonedCard.querySelector(".card-body > p");
            if (clonedDescription) {
                clonedDescription.remove();
            }
            if (agendaModalErrorMessage &&
                agendaModalErrorRequestId &&
                agendaModalErrorRequestId.toLowerCase() === String(requestId).toLowerCase()) {
                const clonedCardBody = clonedCard.querySelector(".card-body");
                if (clonedCardBody) {
                    const validationAlert = document.createElement("div");
                    validationAlert.className = "alert alert-danger border-0 shadow-sm mb-3";
                    const icon = document.createElement("i");
                    icon.className = "fas fa-triangle-exclamation me-2";
                    validationAlert.appendChild(icon);
                    validationAlert.appendChild(document.createTextNode(agendaModalErrorMessage));
                    clonedCardBody.insertBefore(validationAlert, clonedCardBody.firstChild);
                }
            }
            const clonedCardFooter = clonedCard.querySelector(".card-footer");
            if (clonedCardFooter && appointmentModalFooter) {
                appointmentModalFooter.innerHTML = clonedCardFooter.innerHTML;
                appointmentModalFooter.classList.remove("d-none");
                clonedCardFooter.remove();
            }
            appointmentModalBody.appendChild(clonedCard);

            if (appointmentModalTitle) {
                appointmentModalTitle.textContent = category;
            }

            if (appointmentModalSubtitle) {
                const shortId = requestShort || String(requestId).substring(0, 8);
                appointmentModalSubtitle.textContent = `Pedido #${shortId}`;
            }

            if (appointmentModalCategoryIcon) {
                appointmentModalCategoryIcon.className = `fas ${resolveCategoryIcon(category)}`;
            }

            appointmentModalBody.querySelectorAll(".js-arrival-form").forEach(bindArrivalForm);
            appointmentModalBody
                .querySelectorAll("form[action*='UpdateAppointmentOperationalStatus']")
                .forEach(bindOperationalStatusForm);
            enhanceAgendaDistances(appointmentModalBody).catch(() => { });
            appointmentModalInstance.show();
        }

        document.querySelectorAll(".js-open-appointment-modal").forEach(link => {
            link.addEventListener("click", function (event) {
                event.preventDefault();
                openAppointmentModal(this);
            });
        });

        enhanceAgendaDistances(document).catch(() => { });

        if (agendaOpenRequestId) {
            const autoOpenLink = Array
                .from(document.querySelectorAll(".js-open-appointment-modal"))
                .find(link => String(link.dataset.requestId || "").toLowerCase() === agendaOpenRequestId.toLowerCase());
            if (autoOpenLink) {
                openAppointmentModal(autoOpenLink);
            }
        }

        const realtimeAlert = document.getElementById("agendaRealtimeAlert");
        const realtimeCountdown = document.getElementById("agendaRealtimeCountdown");
        const refreshNowButton = document.getElementById("agendaRealtimeRefreshNow");
        let realtimeRefreshTimeout = null;
        let realtimeCountdownInterval = null;

        function normalizeText(value) {
            return String(value || "").trim().toLowerCase();
        }

        function isPendingAppointmentNotification(payload) {
            const subject = normalizeText(payload && payload.subject);
            const message = normalizeText(payload && payload.message);
            return subject.includes("novo agendamento pendente")
                || (subject.includes("agendamento") && message.includes("pendente"));
        }

        function triggerRealtimeRefresh() {
            if (!realtimeAlert || realtimeRefreshTimeout) return;

            let countdown = 3;
            realtimeAlert.classList.remove("d-none");
            if (realtimeCountdown) {
                realtimeCountdown.textContent = String(countdown);
            }

            realtimeCountdownInterval = window.setInterval(function () {
                countdown = Math.max(0, countdown - 1);
                if (realtimeCountdown) {
                    realtimeCountdown.textContent = String(countdown);
                }
            }, 1000);

            realtimeRefreshTimeout = window.setTimeout(function () {
                window.location.reload();
            }, 3000);
        }

        if (refreshNowButton) {
            refreshNowButton.addEventListener("click", function () {
                window.location.reload();
            });
        }

        window.addEventListener("cpm:notification", function (event) {
            const payload = event && event.detail ? event.detail : {};
            if (!isPendingAppointmentNotification(payload)) return;
            triggerRealtimeRefresh();
        });
    })();
