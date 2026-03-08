(function () {
    const pageConfigSource = document.body;
    const navToggle = document.querySelector("[data-nav-toggle]");
    const nav = document.querySelector("[data-nav]");
    const config = {
        leadCaptureUrl: pageConfigSource ? pageConfigSource.getAttribute("data-lead-capture-url") || "" : ""
    };
    const leadShell = document.querySelector("[data-lead-shell]");
    const leadPanels = document.querySelectorAll("[data-lead-panel]");
    const leadTriggers = document.querySelectorAll("[data-lead-trigger]");
    const leadForms = document.querySelectorAll("[data-lead-form]");
    const leadSection = document.getElementById("captacao");

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

    function activateLeadOrigin(origin) {
        const normalizedOrigin = origin === "provider" ? "provider" : "client";

        if (leadShell) {
            leadShell.hidden = false;
            leadShell.classList.add("is-open");
            leadShell.setAttribute("data-active-origin", normalizedOrigin);
        }

        leadPanels.forEach(function (panel) {
            const isActive = panel.getAttribute("data-lead-panel") === normalizedOrigin;
            panel.hidden = !isActive;
            panel.classList.toggle("is-active", isActive);
        });
    }

    function scrollToLeadSection() {
        if (!leadSection) {
            return;
        }

        leadSection.scrollIntoView({ behavior: "smooth", block: "start" });
    }

    leadTriggers.forEach(function (trigger) {
        trigger.addEventListener("click", function () {
            activateLeadOrigin(trigger.getAttribute("data-lead-trigger"));
            scrollToLeadSection();
        });
    });

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
            return "Não foi possível enviar o formulário agora.";
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

        return "Não foi possível enviar o formulário agora.";
    }

    async function submitLeadForm(form) {
        const feedback = form.querySelector("[data-lead-feedback]");
        const submitButton = form.querySelector("button[type='submit']");
        const payload = buildPayload(form);

        if (!config.leadCaptureUrl) {
            if (feedback) {
                feedback.textContent = "Configuração de envio indisponível no momento.";
                feedback.className = "lead-feedback is-error";
            }
            return;
        }

        if (submitButton) {
            submitButton.disabled = true;
        }

        if (feedback) {
            feedback.textContent = "Enviando...";
            feedback.className = "lead-feedback is-loading";
        }

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
            if (feedback) {
                feedback.textContent = body && typeof body.message === "string"
                    ? body.message
                    : "Recebemos seu contato com sucesso.";
                feedback.className = "lead-feedback is-success";
            }
        } catch (error) {
            if (feedback) {
                feedback.textContent = error instanceof Error && error.message
                    ? error.message
                    : "Não foi possível enviar o formulário agora.";
                feedback.className = "lead-feedback is-error";
            }
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
})();
