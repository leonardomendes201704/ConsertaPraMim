(function () {
    const pageConfigSource = document.body;
    const navToggle = document.querySelector("[data-nav-toggle]");
    const nav = document.querySelector("[data-nav]");
    const config = {
        leadCaptureUrl: pageConfigSource ? pageConfigSource.getAttribute("data-lead-capture-url") || "" : "",
        initialLeadOrigin: pageConfigSource ? pageConfigSource.getAttribute("data-initial-lead-origin") || "" : "",
        visitorId: pageConfigSource ? pageConfigSource.getAttribute("data-visitor-id") || "" : ""
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
        const normalizedOrigin = origin === "provider" ? "provider" : "client";

        if (leadShell) {
            leadShell.setAttribute("data-active-origin", normalizedOrigin);
        }

        if (leadTitle) {
            const title = getLeadTitleForOrigin(normalizedOrigin);
            if (title) {
                leadTitle.textContent = title;
            }
        }

        leadPanels.forEach(function (panel) {
            const isActive = panel.getAttribute("data-lead-panel") === normalizedOrigin;
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

    function openLeadCapture(origin) {
        activateLeadOrigin(origin);
        closeMobileNavIfNeeded();

        if (leadModal) {
            leadModal.show();
            return;
        }

        focusFirstInputInActivePanel();
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

    leadForms.forEach(function (form) {
        form.addEventListener("submit", function (event) {
            event.preventDefault();
            submitLeadForm(form);
        });
    });

    activateLeadOrigin("client");
    if (config.initialLeadOrigin === "client" || config.initialLeadOrigin === "provider") {
        openLeadCapture(config.initialLeadOrigin);
    }
    tryOpenLeadFromQueryString();
})();
