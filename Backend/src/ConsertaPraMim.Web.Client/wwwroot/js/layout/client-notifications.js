(function () {
            const config = window.clientLayoutConfig || {};
            const isAuthenticated = config.isAuthenticated === true;
            const apiBaseUrl = config.browserApiBaseUrl || "";
            const hubAccessToken = config.apiToken || "";
            if (!isAuthenticated || !hubAccessToken) return;
            const hubUrl = apiBaseUrl ? `${apiBaseUrl}/notificationHub` : "/notificationHub";
            const connection = new signalR.HubConnectionBuilder()
                .withUrl(hubUrl, {
                    accessTokenFactory: function () { return hubAccessToken; }
                })
                .build();

            function toSafeNavigationUrl(value) {
                const raw = String(value || "").trim();
                if (!raw) return "";

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

            connection.on("ReceiveNotification", function (data) {
                window.dispatchEvent(new CustomEvent("cpm:notification", { detail: data }));
                const actionUrl = toSafeNavigationUrl(data && data.actionUrl ? String(data.actionUrl) : "");
                const safeActionUrl = actionUrl.replace(/"/g, "&quot;");
                const toastId = `toast-${Date.now()}-${Math.floor(Math.random() * 100000)}`;

                // simple toast alert
                const toastHtml = `
                    <div id="${toastId}" class="toast-container position-fixed bottom-0 end-0 p-3">
                        <div class="toast show border-0 shadow-lg rounded-4" role="alert" aria-live="assertive" aria-atomic="true" data-action-url="${safeActionUrl}">
                            <div class="toast-header bg-primary text-white border-0 rounded-top-4">
                                <i class="fas fa-bell me-2"></i>
                                <strong class="me-auto">${data.subject}</strong>
                                <button type="button" class="btn-close btn-close-white" data-bs-dismiss="toast" aria-label="Close"></button>
                            </div>
                            <div class="toast-body bg-white rounded-bottom-4">
                                ${data.message}
                            </div>
                        </div>
                    </div>`;
                $("body").append(toastHtml);

                const $container = $(`#${toastId}`);
                const $toast = $container.find(".toast");
                if (actionUrl) {
                    $toast.css("cursor", "pointer");
                    $toast.on("click", function (event) {
                        if ($(event.target).closest(".btn-close").length) return;
                        window.location.href = actionUrl;
                    });
                }

                setTimeout(() => {
                    $container.fadeOut(200, function () { $(this).remove(); });
                }, 10000);
            });

            connection.start().then(function () {
                connection.invoke("JoinUserGroup");
            }).catch(function (err) {
                return console.error(err.toString());
            });
})();
