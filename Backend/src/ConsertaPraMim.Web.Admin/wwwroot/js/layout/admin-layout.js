(function () {
        const config = window.adminLayoutConfig || {};
        if (!window.fetch) return;
        const csrfToken = config.csrfToken || '';
        if (!csrfToken) return;

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
