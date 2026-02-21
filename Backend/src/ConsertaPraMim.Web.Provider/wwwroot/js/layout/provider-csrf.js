(function () {
    const runtime = window.providerLayoutConfig || {};
    const csrfToken = String(runtime.csrfToken || "");
    const loginUrl = String(runtime.loginUrl || "/Account/Login");
    window.cpmCsrfToken = csrfToken;

    if (!window.fetch) {
        return;
    }

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
        if (typeof payload === "string") return isSessionExpiredText(payload);
        if (Array.isArray(payload)) return payload.some(hasSessionExpiredPayload);

        if (typeof payload === "object") {
            const keys = ["errorMessage", "message", "title", "detail", "error"];
            for (const key of keys) {
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
