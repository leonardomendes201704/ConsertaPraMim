(function () {
    const runtime = window.providerLayoutConfig || {};
    const csrfToken = String(runtime.csrfToken || "");
    window.cpmCsrfToken = csrfToken;

    if (!window.fetch || !csrfToken) {
        return;
    }

    const originalFetch = window.fetch.bind(window);
    window.fetch = function (input, init) {
        const method = String((init && init.method) || "GET").toUpperCase();
        if (method === "GET" || method === "HEAD" || method === "OPTIONS" || method === "TRACE") {
            return originalFetch(input, init);
        }

        const requestUrl = new URL(typeof input === "string" ? input : (input && input.url ? input.url : window.location.href), window.location.origin);
        if (requestUrl.origin !== window.location.origin) {
            return originalFetch(input, init);
        }

        const headers = new Headers((init && init.headers) || {});
        if (!headers.has("RequestVerificationToken")) {
            headers.set("RequestVerificationToken", csrfToken);
        }

        const nextInit = Object.assign({}, init || {}, {
            headers: headers,
            credentials: (init && init.credentials) || "same-origin"
        });

        return originalFetch(input, nextInit);
    };
})();
