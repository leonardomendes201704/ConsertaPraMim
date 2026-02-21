(function () {
    var clientShell = document.getElementById('clientShell');
    if (!clientShell) {
        return;
    }

    var sidebarToggle = document.getElementById('sidebarToggle');
    var sidebarStorageKey = 'cpm-client-sidebar-collapsed';
    var mobileQuery = window.matchMedia('(max-width: 992px)');
    var navLinks = Array.prototype.slice.call(clientShell.querySelectorAll('.client-nav-link'));

    navLinks.forEach(function (link) {
        var labelElement = link.querySelector('span');
        var label = labelElement ? String(labelElement.textContent || '').trim() : '';
        if (!label) {
            return;
        }

        if (!link.getAttribute('data-nav-label')) {
            link.setAttribute('data-nav-label', label);
        }
    });

    function applyCollapsedState(collapsed, persistState) {
        var shouldCollapse = !mobileQuery.matches && !!collapsed;
        clientShell.classList.toggle('sidebar-collapsed', shouldCollapse);

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
            var isCollapsed = clientShell.classList.contains('sidebar-collapsed');
            applyCollapsedState(!isCollapsed, true);
        });
    }

    var handleViewportChange = function () {
        applyCollapsedState(getSavedCollapsedState(), false);
    };

    if (typeof mobileQuery.addEventListener === 'function') {
        mobileQuery.addEventListener('change', handleViewportChange);
    } else if (typeof mobileQuery.addListener === 'function') {
        mobileQuery.addListener(handleViewportChange);
    }
})();
