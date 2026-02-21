(function () {
        const config = window.adminLayoutConfig || {};
        if (!window.fetch) return;
        const csrfToken = config.csrfToken || '';
        const loginUrl = String(config.loginUrl || "/Account/Login");

        const originalFetch = window.fetch.bind(window);
        const isSafeMethod = function (method) {
            return method === "GET" || method === "HEAD" || method === "OPTIONS" || method === "TRACE";
        };

        const isSessionExpiredText = function (value) {
            const text = String(value || "").toLowerCase();
            if (!text) return false;

            return (
                (text.includes("sessao") && text.includes("expirad")) ||
                (text.includes("sessao") && text.includes("faca login novamente")) ||
                (text.includes("token") && text.includes("faca login novamente")) ||
                text.includes("sessao de api expirada")
            );
        };

        const hasSessionExpiredPayload = function (payload) {
            if (!payload) return false;

            if (typeof payload === "string") {
                return isSessionExpiredText(payload);
            }

            if (Array.isArray(payload)) {
                return payload.some(hasSessionExpiredPayload);
            }

            if (typeof payload === "object") {
                const keysToInspect = ["errorMessage", "message", "title", "detail", "error"];
                for (const key of keysToInspect) {
                    if (isSessionExpiredText(payload[key])) {
                        return true;
                    }
                }
            }

            return false;
        };

        const redirectToLogin = function () {
            if (window.__cpmRedirectingToLogin) return;
            const path = String(window.location.pathname || "").toLowerCase();
            if (path.startsWith("/account/login")) return;

            window.__cpmRedirectingToLogin = true;
            const returnUrl = `${window.location.pathname || "/"}${window.location.search || ""}${window.location.hash || ""}`;
            const separator = loginUrl.includes("?") ? "&" : "?";
            window.location.assign(`${loginUrl}${separator}returnUrl=${encodeURIComponent(returnUrl)}`);
        };

        const inspectSessionExpiration = async function (response) {
            if (!response) return;

            if (response.status === 401) {
                redirectToLogin();
                return;
            }

            const contentType = String(response.headers.get("content-type") || "").toLowerCase();
            try {
                if (contentType.includes("application/json")) {
                    const payload = await response.clone().json();
                    if (hasSessionExpiredPayload(payload)) {
                        redirectToLogin();
                    }
                    return;
                }

                if (contentType.includes("text/plain") || contentType.includes("text/html")) {
                    const content = await response.clone().text();
                    if (isSessionExpiredText(content)) {
                        redirectToLogin();
                    }
                }
            } catch {
                // no-op
            }
        };

        window.fetch = function (input, init) {
            const method = String((init && init.method) || "GET").toUpperCase();
            const requestUrl = new URL(typeof input === "string" ? input : (input && input.url ? input.url : window.location.href), window.location.origin);
            const isSameOrigin = requestUrl.origin === window.location.origin;

            let nextInit = init;
            if (!isSafeMethod(method) && isSameOrigin && csrfToken) {
                const headers = new Headers((init && init.headers) || {});
                if (!headers.has("RequestVerificationToken")) {
                    headers.set("RequestVerificationToken", csrfToken);
                }

                nextInit = Object.assign({}, init || {}, {
                    headers: headers,
                    credentials: (init && init.credentials) || "same-origin"
                });
            }

            return originalFetch(input, nextInit).then(function (response) {
                inspectSessionExpiration(response);
                return response;
            });
        };

        const documentText = document.body ? String(document.body.textContent || "") : "";
        if (isSessionExpiredText(documentText)) {
            redirectToLogin();
        }
    })();

    (function () {
        const adminShell = document.getElementById('adminShell');
        if (!adminShell) return;

        const sidebarToggle = document.getElementById('sidebarToggle');
        const sidebarStorageKey = 'cpm-admin-sidebar-collapsed';
        const mobileQuery = window.matchMedia('(max-width: 992px)');
        const navLinks = Array.from(adminShell.querySelectorAll('.admin-nav-link'));

        navLinks.forEach(function (link) {
            const labelElement = link.querySelector('span');
            const label = labelElement ? String(labelElement.textContent || '').trim() : '';
            if (!label) return;

            if (!link.getAttribute('data-nav-label')) {
                link.setAttribute('data-nav-label', label);
            }
        });

        function applyCollapsedState(collapsed, persistState) {
            const shouldCollapse = !mobileQuery.matches && !!collapsed;
            adminShell.classList.toggle('sidebar-collapsed', shouldCollapse);

            if (sidebarToggle) {
                sidebarToggle.setAttribute('aria-pressed', shouldCollapse ? 'true' : 'false');
                sidebarToggle.setAttribute('title', shouldCollapse ? 'Expandir menu' : 'Recolher menu');
            }

            if (persistState && !mobileQuery.matches) {
                try {
                    localStorage.setItem(sidebarStorageKey, shouldCollapse ? '1' : '0');
                } catch {
                    // no-op
                }
            }
        }

        function getSavedCollapsedState() {
            try {
                return localStorage.getItem(sidebarStorageKey) === '1';
            } catch {
                return false;
            }
        }

        applyCollapsedState(getSavedCollapsedState(), false);

        if (sidebarToggle) {
            sidebarToggle.addEventListener('click', function () {
                const isCollapsed = adminShell.classList.contains('sidebar-collapsed');
                applyCollapsedState(!isCollapsed, true);
            });
        }

        const handleViewportChange = function () {
            applyCollapsedState(getSavedCollapsedState(), false);
        };

        if (typeof mobileQuery.addEventListener === 'function') {
            mobileQuery.addEventListener('change', handleViewportChange);
        } else if (typeof mobileQuery.addListener === 'function') {
            mobileQuery.addListener(handleViewportChange);
        }
    })();

    (function () {
        const runtime = window.adminLayoutConfig || {};
        const realtimeState = window.adminRealtimeState = window.adminRealtimeState || {
            connected: false
        };

        if (!runtime.isRealtimeEnabled || !runtime.hubAccessToken || typeof signalR === "undefined") {
            return;
        }

        const apiBaseUrl = String(runtime.apiBaseUrl || "");
        const hubAccessToken = String(runtime.hubAccessToken || "");
        const hubUrl = apiBaseUrl ? `${apiBaseUrl}/notificationHub` : "/notificationHub";

        const connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl, {
                accessTokenFactory: function () { return hubAccessToken; }
            })
            .withAutomaticReconnect()
            .build();
        realtimeState.connection = connection;

        function toSafeNavigationUrl(value) {
            const raw = String(value || "").trim();
            if (!raw) {
                return "";
            }

            try {
                const parsed = new URL(raw, window.location.origin);
                if ((parsed.protocol !== "http:" && parsed.protocol !== "https:") || parsed.origin !== window.location.origin) {
                    return "";
                }

                return `${parsed.pathname}${parsed.search}${parsed.hash}`;
            } catch {
                return "";
            }
        }

        function showToast(payload) {
            const actionUrl = toSafeNavigationUrl(payload && payload.actionUrl ? String(payload.actionUrl) : "");
            const toastId = `admin-toast-${Date.now()}-${Math.floor(Math.random() * 100000)}`;
            const wrapper = document.createElement("div");
            wrapper.id = toastId;
            wrapper.className = "toast-container position-fixed bottom-0 end-0 p-3";
            wrapper.style.zIndex = "1085";
            wrapper.innerHTML = `
                <div class="toast show border-0 shadow-lg rounded-4" role="alert" aria-live="assertive" aria-atomic="true">
                    <div class="toast-header bg-primary text-white border-0 rounded-top-4">
                        <i class="fas fa-bell me-2"></i>
                        <strong class="me-auto" data-role="toast-subject"></strong>
                        <button type="button" class="btn-close btn-close-white" data-bs-dismiss="toast" aria-label="Close"></button>
                    </div>
                    <div class="toast-body bg-white rounded-bottom-4" data-role="toast-message"></div>
                </div>
            `;
            document.body.appendChild(wrapper);

            const toastElement = wrapper.querySelector(".toast");
            const subjectElement = wrapper.querySelector('[data-role="toast-subject"]');
            const messageElement = wrapper.querySelector('[data-role="toast-message"]');
            if (subjectElement) {
                subjectElement.textContent = payload && payload.subject ? String(payload.subject) : "Notificacao";
            }
            if (messageElement) {
                messageElement.textContent = payload && payload.message ? String(payload.message) : "Nova atualizacao recebida.";
            }

            if (toastElement && actionUrl) {
                toastElement.style.cursor = "pointer";
                toastElement.addEventListener("click", function (event) {
                    if (event.target && event.target.closest(".btn-close")) {
                        return;
                    }
                    window.location.href = actionUrl;
                });
            }

            window.setTimeout(function () {
                wrapper.remove();
            }, 9000);
        }

        connection.on("ReceiveNotification", function (data) {
            window.dispatchEvent(new CustomEvent("cpm:notification", { detail: data || {} }));
            showToast(data || {});
        });

        connection.start()
            .then(function () {
                realtimeState.connected = true;
                return connection.invoke("JoinUserGroup");
            })
            .catch(function (err) {
                realtimeState.connected = false;
                console.error(err && err.toString ? err.toString() : err);
            });

        connection.onreconnected(function () {
            realtimeState.connected = true;
            connection.invoke("JoinUserGroup").catch(console.error);
            window.dispatchEvent(new CustomEvent("cpm:realtime-reconnected", {
                detail: {
                    source: "notificationHub",
                    at: new Date().toISOString()
                }
            }));
        });

        connection.onclose(function () {
            realtimeState.connected = false;
        });
    })();
