(function () {
    const config = window.adminSupportTicketDetailsConfig || {};
    const timeline = document.getElementById("supportTimeline");
    if (timeline) {
        timeline.scrollTop = timeline.scrollHeight;
    }

    let isSubmitting = false;
    let reloadTimer = null;
    let knownSnapshot = {
        status: String(config.snapshot && config.snapshot.status ? config.snapshot.status : ""),
        messageCount: Number(config.snapshot && config.snapshot.messageCount ? config.snapshot.messageCount : 0),
        lastInteractionAtUtc: String(config.snapshot && config.snapshot.lastInteractionAtUtc ? config.snapshot.lastInteractionAtUtc : ""),
        lastMessageId: String(config.snapshot && config.snapshot.lastMessageId ? config.snapshot.lastMessageId : "")
    };
    const ticketId = String(config.ticketId || "").toLowerCase();
    const pollUrl = String(config.pollUrl || "");
    const pollIntervalMs = Number(config.pollIntervalMs || 10000);
    let pollHandle = null;

    function bindLoadingState(formId, buttonId, loadingText) {
        const form = document.getElementById(formId);
        const button = document.getElementById(buttonId);
        if (!form || !button) {
            return;
        }

        form.addEventListener("submit", function () {
            isSubmitting = true;
            button.disabled = true;
            button.innerHTML = `<span class="spinner-border spinner-border-sm me-1" role="status"></span>${loadingText}`;
        });
    }

    function toSafePath(value) {
        const raw = String(value || "").trim();
        if (!raw) {
            return "";
        }

        try {
            const parsed = new URL(raw, window.location.origin);
            if ((parsed.protocol !== "http:" && parsed.protocol !== "https:") || parsed.origin !== window.location.origin) {
                return "";
            }

            return `${parsed.pathname}${parsed.search}${parsed.hash}`.toLowerCase();
        } catch {
            return "";
        }
    }

    function isTicketPath(path) {
        if (!path || !ticketId) {
            return false;
        }

        return path.indexOf(`/adminsupporttickets/details/${ticketId}`) >= 0;
    }

    function scheduleReload(reason) {
        if (isSubmitting || reloadTimer) {
            return;
        }

        reloadTimer = window.setTimeout(function () {
            window.location.reload();
        }, reason === "polling" ? 250 : 120);
    }

    function hasSnapshotChanged(nextSnapshot) {
        return nextSnapshot.status !== knownSnapshot.status ||
            nextSnapshot.messageCount !== knownSnapshot.messageCount ||
            nextSnapshot.lastInteractionAtUtc !== knownSnapshot.lastInteractionAtUtc ||
            nextSnapshot.lastMessageId !== knownSnapshot.lastMessageId;
    }

    async function pollForUpdates() {
        if (!pollUrl || isSubmitting) {
            return;
        }

        try {
            const response = await fetch(pollUrl, {
                method: "GET",
                headers: {
                    Accept: "application/json"
                },
                credentials: "same-origin",
                cache: "no-store"
            });

            if (!response.ok) {
                return;
            }

            const payload = await response.json();
            if (!payload || !payload.success || !payload.snapshot) {
                return;
            }

            const snapshot = payload.snapshot;
            const nextSnapshot = {
                status: String(snapshot.status || ""),
                messageCount: Number(snapshot.messageCount || 0),
                lastInteractionAtUtc: String(snapshot.lastInteractionAtUtc || ""),
                lastMessageId: String(snapshot.lastMessageId || "")
            };

            if (!hasSnapshotChanged(nextSnapshot)) {
                return;
            }

            knownSnapshot = nextSnapshot;
            scheduleReload("polling");
        } catch {
            // no-op: polling fallback should never break the page.
        }
    }

    bindLoadingState("adminSupportMessageForm", "adminSupportSendMessageBtn", "Enviando...");
    bindLoadingState("adminSupportStatusForm", "adminSupportStatusBtn", "Salvando...");
    bindLoadingState("adminSupportAssignForm", "adminSupportAssignBtn", "Salvando...");

    window.addEventListener("cpm:notification", function (event) {
        if (isSubmitting) {
            return;
        }

        const detail = event && event.detail ? event.detail : {};
        const path = toSafePath(detail.actionUrl || "");
        if (isTicketPath(path)) {
            scheduleReload("realtime");
        }
    });

    if (pollUrl && pollIntervalMs >= 1000) {
        pollHandle = window.setInterval(pollForUpdates, pollIntervalMs);
        window.addEventListener("beforeunload", function () {
            if (pollHandle) {
                window.clearInterval(pollHandle);
                pollHandle = null;
            }
        });
    }
})();
