(function () {
    const runtime = window.providerLayoutConfig || {};
    const realtimeState = window.providerRealtimeState = window.providerRealtimeState || {
        connected: false
    };

    $("#sidebarToggle").click(function (e) {
        e.preventDefault();
        $("#wrapper").toggleClass("toggled");
    });

    if (!runtime.isRealtimeEnabled) {
        return;
    }

    const apiBaseUrl = String(runtime.apiBaseUrl || "");
    const hubAccessToken = String(runtime.hubAccessToken || "");
    if (!hubAccessToken || typeof signalR === "undefined") {
        return;
    }

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

    connection.on("ReceiveNotification", function (data) {
        window.dispatchEvent(new CustomEvent("cpm:notification", { detail: data }));
        const actionUrl = toSafeNavigationUrl(data && data.actionUrl ? String(data.actionUrl) : "");
        const safeActionUrl = actionUrl.replace(/"/g, "&quot;");
        const toastId = `toast-${Date.now()}-${Math.floor(Math.random() * 100000)}`;
        const toastHtml = `
            <div id="${toastId}" class="toast-container position-fixed bottom-0 end-0 p-3">
                <div class="toast show border-0 shadow-lg rounded-4" role="alert" aria-live="assertive" aria-atomic="true" data-action-url="${safeActionUrl}">
                    <div class="toast-header bg-success text-white border-0 rounded-top-4">
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
                if ($(event.target).closest(".btn-close").length) {
                    return;
                }

                window.location.href = actionUrl;
            });
        }

        setTimeout(function () {
            $container.fadeOut(200, function () { $(this).remove(); });
        }, 10000);
    });

    connection.start().then(function () {
        realtimeState.connected = true;
        connection.invoke("JoinUserGroup");
    }).catch(function (err) {
        realtimeState.connected = false;
        return console.error(err.toString());
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

    (function setupQuickOperationalStatus() {
        if (!runtime.isProviderRole) {
            return;
        }

        const quickStatusSelect = document.getElementById("quickOperationalStatus");
        if (!quickStatusSelect || !hubAccessToken) {
            return;
        }

        const providerId = String(runtime.currentUserId || "");
        if (!providerId) {
            return;
        }

        const statusGetUrl = apiBaseUrl
            ? `${apiBaseUrl}/api/profile/provider/${providerId}/status`
            : `/api/profile/provider/${providerId}/status`;
        const statusPutUrl = apiBaseUrl
            ? `${apiBaseUrl}/api/profile/provider/status`
            : "/api/profile/provider/status";

        function statusToSelectValue(status) {
            if (status === "Ausente") {
                return "0";
            }
            if (status === "EmAtendimento") {
                return "2";
            }

            return "1";
        }

        function selectValueToStatus(value) {
            if (String(value) === "0") {
                return "Ausente";
            }
            if (String(value) === "2") {
                return "EmAtendimento";
            }

            return "Online";
        }

        async function loadCurrentStatus() {
            try {
                const response = await fetch(statusGetUrl, {
                    method: "GET",
                    headers: {
                        Accept: "application/json",
                        Authorization: `Bearer ${hubAccessToken}`
                    }
                });

                if (!response.ok) {
                    return;
                }

                const payload = await response.json();
                quickStatusSelect.value = statusToSelectValue(payload && payload.status ? payload.status : "");
            } catch (error) {
                console.error(error);
            }
        }

        quickStatusSelect.addEventListener("change", async function () {
            quickStatusSelect.disabled = true;
            try {
                const numericStatus = Number(quickStatusSelect.value);
                const response = await fetch(statusPutUrl, {
                    method: "PUT",
                    headers: {
                        "Content-Type": "application/json",
                        Accept: "application/json",
                        Authorization: `Bearer ${hubAccessToken}`
                    },
                    body: JSON.stringify({
                        operationalStatus: numericStatus
                    })
                });

                if (!response.ok) {
                    throw new Error("Falha ao atualizar status operacional.");
                }

                window.dispatchEvent(new CustomEvent("cpm:provider-status", {
                    detail: {
                        providerId: providerId,
                        status: selectValueToStatus(quickStatusSelect.value),
                        updatedAt: new Date().toISOString()
                    }
                }));
            } catch (error) {
                console.error(error);
                await loadCurrentStatus();
            } finally {
                quickStatusSelect.disabled = false;
            }
        });

        window.addEventListener("cpm:provider-status", function (event) {
            const detail = event && event.detail ? event.detail : {};
            if (String(detail.providerId || "").toLowerCase() !== String(providerId).toLowerCase()) {
                return;
            }

            quickStatusSelect.value = statusToSelectValue(detail.status);
        });

        loadCurrentStatus().catch(console.error);
    })();
})();
