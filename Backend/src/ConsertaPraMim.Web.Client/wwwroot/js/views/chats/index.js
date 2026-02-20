(function () {
    const cards = document.querySelectorAll("[data-chat-card]");
    cards.forEach(function (card) {
        card.addEventListener("click", function () {
            const requestId = card.getAttribute("data-request-id");
            const providerId = card.getAttribute("data-provider-id");
            const title = card.getAttribute("data-chat-title") || "Chat";
            if (!requestId || !providerId) return;

            window.dispatchEvent(new CustomEvent("cpm:open-chat", {
                detail: {
                    requestId: requestId,
                    providerId: providerId,
                    title: title,
                    minimized: false,
                    loadHistory: true
                }
            }));
        });
    });
})();
