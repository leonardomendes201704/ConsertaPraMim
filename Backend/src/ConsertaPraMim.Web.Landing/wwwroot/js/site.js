(function () {
    const pageConfigSource = document.body;
    const navToggle = document.querySelector("[data-nav-toggle]");
    const nav = document.querySelector("[data-nav]");
    const config = {
        leadCaptureUrl: pageConfigSource ? pageConfigSource.getAttribute("data-lead-capture-url") || "" : "",
        initialLeadOrigin: pageConfigSource ? pageConfigSource.getAttribute("data-initial-lead-origin") || "" : "",
        visitorId: pageConfigSource ? pageConfigSource.getAttribute("data-visitor-id") || "" : "",
        sessionId: pageConfigSource ? pageConfigSource.getAttribute("data-session-id") || "" : "",
        analyticsConfigUrl: pageConfigSource ? pageConfigSource.getAttribute("data-analytics-config-url") || "" : "",
        telemetryUrl: pageConfigSource ? pageConfigSource.getAttribute("data-telemetry-url") || "" : ""
    };

    const leadModalElement = document.getElementById("leadCaptureModal");
    const leadModal = leadModalElement && window.bootstrap && typeof window.bootstrap.Modal === "function"
        ? window.bootstrap.Modal.getOrCreateInstance(leadModalElement)
        : null;
    const leadShell = document.querySelector("[data-lead-shell]");
    const leadTitle = document.querySelector("[data-lead-title]");
    const leadPanels = document.querySelectorAll("[data-lead-panel]");
    const leadTriggers = document.querySelectorAll("[data-lead-trigger]");
    const leadForms = document.querySelectorAll("[data-lead-form]");
    const leadToast = document.querySelector("[data-lead-toast]");
    let leadToastTimer = 0;
    let currentLeadOrigin = config.initialLeadOrigin === "provider" ? "provider" : "client";

    const telemetryState = {
        enabled: false,
        queue: [],
        flushTimer: 0,
        sending: false,
        scrollMilestones: new Set(),
        heartbeatSeconds: 0,
        heartbeatIntervalId: 0,
        heartbeatStepSeconds: 15,
        maxHeartbeatSeconds: 1800,
        scrollEnabled: false,
        clickEnabled: false,
        trackInteractiveOnly: true,
        heatmapRows: 6,
        heatmapColumns: 6,
        telemetryBindingsRegistered: false
    };

    function clamp(value, min, max) {
        return Math.min(max, Math.max(min, value));
    }

    function normalizeLeadOrigin(origin) {
        return origin === "provider" ? "provider" : "client";
    }

    function closeMobileNavIfNeeded() {
        if (!navToggle || !nav) {
            return;
        }

        navToggle.setAttribute("aria-expanded", "false");
        nav.classList.remove("is-open");
    }

    function getLeadTitleForOrigin(origin) {
        if (!leadShell) {
            return "";
        }

        return origin === "provider"
            ? leadShell.getAttribute("data-provider-title") || ""
            : leadShell.getAttribute("data-client-title") || "";
    }

    function clearFeedback(form) {
        const feedback = form.querySelector("[data-lead-feedback]");
        if (!feedback) {
            return;
        }

        feedback.textContent = "";
        feedback.className = "lead-feedback";
    }

    function setFeedbackMessage(feedback, type, message) {
        if (!feedback) {
            return;
        }

        feedback.textContent = message;
        feedback.className = `lead-feedback is-${type}`;
    }

    function activateLeadOrigin(origin) {
        currentLeadOrigin = normalizeLeadOrigin(origin);

        if (leadShell) {
            leadShell.setAttribute("data-active-origin", currentLeadOrigin);
        }

        if (leadTitle) {
            const title = getLeadTitleForOrigin(currentLeadOrigin);
            if (title) {
                leadTitle.textContent = title;
            }
        }

        leadPanels.forEach(function (panel) {
            const isActive = panel.getAttribute("data-lead-panel") === currentLeadOrigin;
            panel.hidden = !isActive;
            panel.classList.toggle("is-active", isActive);
        });

        leadForms.forEach(function (form) {
            clearFeedback(form);
        });
    }

    function focusFirstInputInActivePanel() {
        const activePanel = document.querySelector("[data-lead-panel].is-active");
        if (!activePanel) {
            return;
        }

        const focusTarget = activePanel.querySelector("input, textarea, select, button");
        if (focusTarget instanceof HTMLElement) {
            window.setTimeout(function () {
                focusTarget.focus();
            }, 60);
        }
    }

    function hideLeadToast() {
        if (!leadToast) {
            return;
        }

        leadToast.classList.remove("is-visible");
        window.setTimeout(function () {
            leadToast.hidden = true;
        }, 220);
    }

    function showLeadToast(message, type) {
        if (!leadToast) {
            return;
        }

        if (leadToastTimer) {
            window.clearTimeout(leadToastTimer);
            leadToastTimer = 0;
        }

        leadToast.hidden = false;
        leadToast.textContent = message;
        leadToast.className = `lead-toast is-${type}`;

        window.requestAnimationFrame(function () {
            leadToast.classList.add("is-visible");
        });

        leadToastTimer = window.setTimeout(function () {
            hideLeadToast();
            leadToastTimer = 0;
        }, 3600);
    }

    function buildTelemetryEnvelope(events) {
        return {
            visitorId: config.visitorId || null,
            sessionId: config.sessionId || null,
            currentUrl: window.location.href,
            path: window.location.pathname || "/",
            host: window.location.host || "",
            scheme: window.location.protocol.replace(":", "") || "https",
            initialLeadOrigin: currentLeadOrigin || config.initialLeadOrigin || null,
            viewportWidth: window.innerWidth || null,
            viewportHeight: window.innerHeight || null,
            browserLanguage: navigator.language || (navigator.languages && navigator.languages[0]) || null,
            events: events
        };
    }

    async function sendTelemetryBatch(events, useKeepAlive) {
        if (!config.telemetryUrl || !Array.isArray(events) || events.length === 0) {
            return;
        }

        const payload = buildTelemetryEnvelope(events);

        if (useKeepAlive && navigator.sendBeacon) {
            const blob = new Blob([JSON.stringify(payload)], { type: "application/json" });
            if (navigator.sendBeacon(config.telemetryUrl, blob)) {
                return;
            }
        }

        await window.fetch(config.telemetryUrl, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "Accept": "application/json"
            },
            keepalive: Boolean(useKeepAlive),
            body: JSON.stringify(payload)
        });
    }

    async function flushTelemetry(useKeepAlive) {
        if (!telemetryState.enabled || telemetryState.sending || telemetryState.queue.length === 0) {
            return;
        }

        const events = telemetryState.queue.splice(0, telemetryState.queue.length);
        telemetryState.sending = true;

        try {
            await sendTelemetryBatch(events, useKeepAlive);
        } catch (_error) {
            telemetryState.queue.unshift.apply(telemetryState.queue, events);
        } finally {
            telemetryState.sending = false;
        }
    }

    function scheduleTelemetryFlush(delayMs) {
        if (!telemetryState.enabled) {
            return;
        }

        if (telemetryState.flushTimer) {
            window.clearTimeout(telemetryState.flushTimer);
        }

        telemetryState.flushTimer = window.setTimeout(function () {
            telemetryState.flushTimer = 0;
            flushTelemetry(false);
        }, typeof delayMs === "number" ? delayMs : 1200);
    }

    function enqueueTelemetryEvent(type, values) {
        if (!telemetryState.enabled || !config.visitorId || !config.sessionId) {
            return;
        }

        telemetryState.queue.push(Object.assign({
            type: type,
            occurredAtUtc: new Date().toISOString()
        }, values || {}));

        if (telemetryState.queue.length >= 8) {
            scheduleTelemetryFlush(0);
            return;
        }

        scheduleTelemetryFlush();
    }

    function getScrollDepthPercent() {
        const documentElement = document.documentElement;
        const scrollableHeight = Math.max((documentElement ? documentElement.scrollHeight : 0) - window.innerHeight, 1);
        const consumed = clamp(window.scrollY + window.innerHeight, window.innerHeight, scrollableHeight + window.innerHeight);
        return clamp(Math.round((consumed / Math.max(documentElement ? documentElement.scrollHeight : consumed, consumed)) * 100), 0, 100);
    }

    function recordScrollMilestones() {
        if (!telemetryState.enabled || !telemetryState.scrollEnabled) {
            return;
        }

        const depthPercent = getScrollDepthPercent();
        telemetryState.scrollMilestones.forEach(function (milestone) {
            if (typeof milestone !== "number" || depthPercent < milestone) {
                return;
            }

            if (telemetryState.scrollMilestonesTriggered.has(milestone)) {
                return;
            }

            telemetryState.scrollMilestonesTriggered.add(milestone);
            enqueueTelemetryEvent("scroll_milestone", { scrollDepthPercent: milestone });
        });
    }

    function isInteractiveTarget(element) {
        if (!(element instanceof Element)) {
            return false;
        }

        return Boolean(element.closest("a,button,input,textarea,select,label,[role='button'],[data-lead-trigger]"));
    }

    function getElementKey(element) {
        if (!(element instanceof Element)) {
            return "document";
        }

        if (element.id) {
            return `#${element.id}`;
        }

        if (element.getAttribute("data-lead-trigger")) {
            return `lead-trigger:${element.getAttribute("data-lead-trigger")}`;
        }

        if (element.getAttribute("name")) {
            return `${element.tagName.toLowerCase()}[name='${element.getAttribute("name")}']`;
        }

        return element.tagName.toLowerCase();
    }

    function getElementLabel(element) {
        if (!(element instanceof HTMLElement)) {
            return null;
        }

        const label = (element.innerText || element.getAttribute("aria-label") || element.getAttribute("title") || "").trim();
        return label ? label.slice(0, 240) : null;
    }

    function getHeatmapPosition(percent, cells) {
        if (!cells || cells <= 0) {
            return null;
        }

        return Math.min(cells, Math.max(1, Math.floor((clamp(percent, 0, 100) / 100) * cells) + 1));
    }

    function registerTelemetryBindings() {
        if (telemetryState.telemetryBindingsRegistered) {
            return;
        }

        telemetryState.telemetryBindingsRegistered = true;
        telemetryState.scrollMilestonesTriggered = new Set();

        document.addEventListener("scroll", recordScrollMilestones, { passive: true });

        document.addEventListener("click", function (event) {
            if (!telemetryState.enabled || !telemetryState.clickEnabled) {
                return;
            }

            const target = event.target instanceof Element ? event.target.closest("a,button,input,textarea,select,label,[role='button'],[data-lead-trigger],body") : null;
            if (telemetryState.trackInteractiveOnly && !isInteractiveTarget(target)) {
                return;
            }

            const clickXPercent = clamp((event.clientX / Math.max(window.innerWidth || 1, 1)) * 100, 0, 100);
            const clickYPercent = clamp((event.clientY / Math.max(window.innerHeight || 1, 1)) * 100, 0, 100);
            const anchor = target instanceof Element ? target.closest("a") : null;

            enqueueTelemetryEvent("click", {
                clickXPercent: Number(clickXPercent.toFixed(2)),
                clickYPercent: Number(clickYPercent.toFixed(2)),
                heatmapRow: getHeatmapPosition(clickYPercent, telemetryState.heatmapRows),
                heatmapColumn: getHeatmapPosition(clickXPercent, telemetryState.heatmapColumns),
                elementKey: getElementKey(target),
                elementLabel: getElementLabel(target),
                elementHref: anchor && anchor.getAttribute("href") ? anchor.getAttribute("href").slice(0, 500) : null
            });
        }, true);

        document.addEventListener("visibilitychange", function () {
            if (document.visibilityState === "hidden") {
                flushTelemetry(true);
            }
        });

        window.addEventListener("pagehide", function () {
            flushTelemetry(true);
        });
    }

    function configureHeartbeat(heartbeatConfig) {
        if (telemetryState.heartbeatIntervalId) {
            window.clearInterval(telemetryState.heartbeatIntervalId);
            telemetryState.heartbeatIntervalId = 0;
        }

        if (!heartbeatConfig || heartbeatConfig.enabled !== true) {
            return;
        }

        telemetryState.heartbeatStepSeconds = Math.max(5, Number(heartbeatConfig.intervalSeconds || 15));
        telemetryState.maxHeartbeatSeconds = Math.max(telemetryState.heartbeatStepSeconds, Number(heartbeatConfig.maxSessionDurationMinutes || 30) * 60);
        telemetryState.heartbeatSeconds = 0;

        telemetryState.heartbeatIntervalId = window.setInterval(function () {
            if (!telemetryState.enabled || document.visibilityState !== "visible") {
                return;
            }

            if (telemetryState.heartbeatSeconds >= telemetryState.maxHeartbeatSeconds) {
                window.clearInterval(telemetryState.heartbeatIntervalId);
                telemetryState.heartbeatIntervalId = 0;
                return;
            }

            telemetryState.heartbeatSeconds += telemetryState.heartbeatStepSeconds;
            enqueueTelemetryEvent("heartbeat", {
                activeSeconds: telemetryState.heartbeatStepSeconds
            });
        }, telemetryState.heartbeatStepSeconds * 1000);
    }

    async function loadAnalyticsConfig() {
        if (!config.analyticsConfigUrl || !config.telemetryUrl || !config.visitorId || !config.sessionId) {
            return;
        }

        try {
            const response = await window.fetch(config.analyticsConfigUrl, {
                method: "GET",
                headers: {
                    "Accept": "application/json"
                }
            });

            if (!response.ok) {
                return;
            }

            const body = await response.json();
            telemetryState.enabled = Boolean(body && body.enabled);
            if (!telemetryState.enabled) {
                return;
            }

            const scrollConfig = body.scroll || {};
            const clicksConfig = body.clicks || {};
            telemetryState.scrollEnabled = scrollConfig.enabled !== false;
            telemetryState.clickEnabled = clicksConfig.enabled !== false;
            telemetryState.trackInteractiveOnly = clicksConfig.trackInteractiveOnly !== false;
            telemetryState.heatmapRows = Math.max(1, Number(clicksConfig.heatmapGridRows || 6));
            telemetryState.heatmapColumns = Math.max(1, Number(clicksConfig.heatmapGridColumns || 6));
            telemetryState.scrollMilestones = new Set(
                Array.isArray(scrollConfig.milestonesPercent)
                    ? scrollConfig.milestonesPercent
                        .map(function (value) { return Number(value); })
                        .filter(function (value) { return Number.isFinite(value) && value >= 0 && value <= 100; })
                    : [25, 50, 75, 100]);

            registerTelemetryBindings();
            configureHeartbeat(body.heartbeat || {});
            recordScrollMilestones();
        } catch (_error) {
            telemetryState.enabled = false;
        }
    }

    function openLeadCapture(origin) {
        activateLeadOrigin(origin);
        closeMobileNavIfNeeded();
        enqueueTelemetryEvent("lead_modal_open", {});

        if (leadModal) {
            leadModal.show();
            return;
        }

        focusFirstInputInActivePanel();
    }

    function tryOpenLeadFromQueryString() {
        if (!leadModalElement) {
            return;
        }

        const url = new URL(window.location.href);
        const leadOrigin = url.searchParams.get("lead");
        if (leadOrigin !== "client" && leadOrigin !== "provider") {
            return;
        }

        openLeadCapture(leadOrigin);
        url.searchParams.delete("lead");
        window.history.replaceState({}, document.title, `${url.pathname}${url.search}${url.hash}`);
    }

    function readFormValue(formData, name) {
        const rawValue = formData.get(name);
        return typeof rawValue === "string" ? rawValue.trim() : "";
    }

    function buildMetadataPayload() {
        const userAgentDataPlatform = navigator.userAgentData && typeof navigator.userAgentData.platform === "string"
            ? navigator.userAgentData.platform
            : "";

        return {
            currentPageUrl: window.location.href,
            referrerUrl: document.referrer || "",
            queryString: window.location.search || "",
            browserLanguage: navigator.language || (navigator.languages && navigator.languages[0]) || "",
            screenResolution: window.screen ? `${window.screen.width}x${window.screen.height}` : "",
            devicePlatform: userAgentDataPlatform || navigator.platform || "",
            timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone || ""
        };
    }

    function buildPayload(form) {
        const origin = form.getAttribute("data-lead-form") === "provider" ? "Provider" : "Client";
        const formData = new FormData(form);
        const metadata = buildMetadataPayload();
        const requestedService = origin === "Provider" ? "" : readFormValue(formData, "requestedService");
        const yearsOfExperienceRaw = readFormValue(formData, "yearsOfExperience");

        return {
            origin,
            visitorId: config.visitorId || "",
            sessionId: config.sessionId || "",
            fullName: readFormValue(formData, "fullName"),
            phone: readFormValue(formData, "phone"),
            email: readFormValue(formData, "email"),
            city: readFormValue(formData, "city"),
            state: readFormValue(formData, "state").toUpperCase(),
            neighborhood: readFormValue(formData, "neighborhood"),
            serviceCategory: readFormValue(formData, "serviceCategory"),
            requestedService,
            companyName: readFormValue(formData, "companyName"),
            companyDocument: readFormValue(formData, "companyDocument"),
            yearsOfExperience: yearsOfExperienceRaw ? Number.parseInt(yearsOfExperienceRaw, 10) : null,
            message: readFormValue(formData, "message"),
            currentPageUrl: metadata.currentPageUrl,
            referrerUrl: metadata.referrerUrl,
            queryString: metadata.queryString,
            utmSource: "",
            utmMedium: "",
            utmCampaign: "",
            utmTerm: "",
            utmContent: "",
            browserLanguage: metadata.browserLanguage,
            screenResolution: metadata.screenResolution,
            devicePlatform: metadata.devicePlatform,
            timeZone: metadata.timeZone
        };
    }

    function readProblemDetails(payload) {
        if (!payload || typeof payload !== "object") {
            return "Não foi possível enviar seus dados agora. Tente novamente em instantes.";
        }

        if (typeof payload.detail === "string" && payload.detail.trim()) {
            return payload.detail.trim();
        }

        if (typeof payload.message === "string" && payload.message.trim()) {
            return payload.message.trim();
        }

        if (payload.errors && typeof payload.errors === "object") {
            const messages = [];
            Object.values(payload.errors).forEach(function (entries) {
                if (Array.isArray(entries)) {
                    entries.forEach(function (entry) {
                        if (typeof entry === "string" && entry.trim()) {
                            messages.push(entry.trim());
                        }
                    });
                }
            });

            if (messages.length) {
                return messages[0];
            }
        }

        return "Não foi possível enviar seus dados agora. Tente novamente em instantes.";
    }

    function getFriendlyErrorMessage(error) {
        if (error instanceof Error) {
            const message = (error.message || "").trim();
            if (!message) {
                return "Não foi possível enviar seus dados agora. Tente novamente em instantes.";
            }

            if (/failed to fetch|networkerror|load failed/i.test(message)) {
                return "Não foi possível enviar seus dados agora. Verifique sua conexão e tente novamente em instantes.";
            }

            return message;
        }

        return "Não foi possível enviar seus dados agora. Tente novamente em instantes.";
    }

    async function submitLeadForm(form) {
        const feedback = form.querySelector("[data-lead-feedback]");
        const submitButton = form.querySelector("button[type='submit']");
        const payload = buildPayload(form);
        const successMessage = "Dados enviados com sucesso!";

        if (!config.leadCaptureUrl) {
            setFeedbackMessage(feedback, "error", "O envio está temporariamente indisponível. Tente novamente em instantes.");
            return;
        }

        if (submitButton) {
            submitButton.disabled = true;
        }

        setFeedbackMessage(feedback, "loading", "Enviando seus dados...");

        try {
            const response = await window.fetch(config.leadCaptureUrl, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "Accept": "application/json"
                },
                body: JSON.stringify(payload)
            });

            const body = await response.json().catch(function () { return null; });
            if (!response.ok) {
                throw new Error(readProblemDetails(body));
            }

            form.reset();
            setFeedbackMessage(feedback, "success", successMessage);
            showLeadToast(successMessage, "success");
            enqueueTelemetryEvent("lead_submit_success", {});
            scheduleTelemetryFlush(0);

            if (leadModal) {
                window.setTimeout(function () {
                    leadModal.hide();
                }, 450);
            }
        } catch (error) {
            setFeedbackMessage(feedback, "error", getFriendlyErrorMessage(error));
        } finally {
            if (submitButton) {
                submitButton.disabled = false;
            }
        }
    }

    if (navToggle && nav) {
        navToggle.addEventListener("click", function () {
            const isExpanded = navToggle.getAttribute("aria-expanded") === "true";
            navToggle.setAttribute("aria-expanded", String(!isExpanded));
            nav.classList.toggle("is-open", !isExpanded);
        });
    }

    const revealItems = document.querySelectorAll("[data-reveal]");
    if (revealItems.length) {
        const observer = new IntersectionObserver(
            function (entries) {
                entries.forEach(function (entry) {
                    if (!entry.isIntersecting) {
                        return;
                    }

                    entry.target.classList.add("is-visible");
                    observer.unobserve(entry.target);
                });
            },
            {
                threshold: 0.15,
                rootMargin: "0px 0px -40px 0px"
            });

        revealItems.forEach(function (item, index) {
            item.style.transitionDelay = `${Math.min(index * 60, 240)}ms`;
            observer.observe(item);
        });
    }

    leadTriggers.forEach(function (trigger) {
        trigger.addEventListener("click", function (event) {
            if (!leadModalElement) {
                return;
            }

            event.preventDefault();
            openLeadCapture(trigger.getAttribute("data-lead-trigger"));
        });
    });

    if (leadModalElement) {
        leadModalElement.addEventListener("shown.bs.modal", function () {
            focusFirstInputInActivePanel();
        });

        leadModalElement.addEventListener("hidden.bs.modal", function () {
            leadForms.forEach(function (form) {
                clearFeedback(form);
            });
        });
    }

    leadForms.forEach(function (form) {
        form.addEventListener("submit", function (event) {
            event.preventDefault();
            submitLeadForm(form);
        });
    });

    activateLeadOrigin(currentLeadOrigin);
    if (config.initialLeadOrigin === "client" || config.initialLeadOrigin === "provider") {
        openLeadCapture(config.initialLeadOrigin);
    }
    tryOpenLeadFromQueryString();
    loadAnalyticsConfig();
})();
