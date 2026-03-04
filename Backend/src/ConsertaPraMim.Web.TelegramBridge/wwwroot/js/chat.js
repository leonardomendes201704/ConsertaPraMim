(function () {
    const state = {
        conversations: [],
        activeChatId: null,
        messagesByChatId: new Map(),
        messageIdsByChatId: new Map(),
        pendingFiles: [],
        hub: null
    };

    const elements = {
        conversationList: document.getElementById("conversationList"),
        activeConversationTitle: document.getElementById("activeConversationTitle"),
        activeConversationSubtitle: document.getElementById("activeConversationSubtitle"),
        messagesContainer: document.getElementById("messagesContainer"),
        pendingAttachments: document.getElementById("pendingAttachments"),
        sendMessageForm: document.getElementById("sendMessageForm"),
        messageInput: document.getElementById("messageInput"),
        attachmentInput: document.getElementById("attachmentInput"),
        sendMessageButton: document.getElementById("sendMessageButton"),
        connectionBadge: document.getElementById("connectionBadge")
    };

    function escapeHtml(value) {
        return String(value || "")
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#39;");
    }

    function formatDateTime(value) {
        if (!value) {
            return "";
        }

        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return "";
        }

        return new Intl.DateTimeFormat("pt-BR", {
            day: "2-digit",
            month: "2-digit",
            hour: "2-digit",
            minute: "2-digit"
        }).format(date);
    }

    function formatConversationDate(value) {
        if (!value) {
            return "";
        }

        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return "";
        }

        return new Intl.DateTimeFormat("pt-BR", {
            hour: "2-digit",
            minute: "2-digit"
        }).format(date);
    }

    function safeUrl(raw) {
        const value = String(raw || "").trim();
        if (!value) {
            return "";
        }

        try {
            const parsed = new URL(value, window.location.origin);
            if (parsed.protocol !== "http:" && parsed.protocol !== "https:") {
                return "";
            }

            return parsed.href;
        } catch {
            return "";
        }
    }

    function showToast(message, kind) {
        const toast = document.createElement("div");
        toast.className = kind === "error" ? "wa-toast error" : "wa-toast";
        toast.textContent = message;
        document.body.appendChild(toast);

        setTimeout(function () {
            toast.remove();
        }, 2800);
    }

    async function requestJson(url, options) {
        const response = await fetch(url, options);
        let data = null;

        try {
            data = await response.json();
        } catch {
            data = null;
        }

        if (!response.ok) {
            const serverMessage = data && data.error ? data.error : `Erro HTTP ${response.status}`;
            throw new Error(serverMessage);
        }

        return data;
    }

    function sortConversations() {
        state.conversations.sort(function (left, right) {
            const leftDate = new Date(left.updatedAtUtc || 0).getTime();
            const rightDate = new Date(right.updatedAtUtc || 0).getTime();
            return rightDate - leftDate;
        });
    }

    function setConnectionBadge(text, isReady) {
        if (!elements.connectionBadge) {
            return;
        }

        elements.connectionBadge.textContent = text;
        elements.connectionBadge.style.background = isReady
            ? "rgba(37, 211, 102, 0.24)"
            : "rgba(255, 255, 255, 0.18)";
    }

    function renderConversations() {
        const listEl = elements.conversationList;
        if (!listEl) {
            return;
        }

        if (state.conversations.length === 0) {
            listEl.innerHTML = "<div class='wa-empty-state'><p>Nenhuma conversa carregada.</p></div>";
            return;
        }

        const html = state.conversations
            .map(function (conversation) {
                const chatId = String(conversation.chatId);
                const activeClass = state.activeChatId === chatId ? " active" : "";

                return `
                    <button type="button" class="wa-conversation-item${activeClass}" data-chat-id="${escapeHtml(chatId)}">
                        <div class="wa-conversation-top">
                            <span class="wa-conversation-title">${escapeHtml(conversation.title || `Chat ${chatId}`)}</span>
                            <span class="wa-conversation-date">${escapeHtml(formatConversationDate(conversation.updatedAtUtc))}</span>
                        </div>
                        <span class="wa-conversation-preview">${escapeHtml(conversation.lastMessagePreview || "Sem mensagens")}</span>
                    </button>
                `;
            })
            .join("");

        listEl.innerHTML = html;

        listEl.querySelectorAll("[data-chat-id]").forEach(function (button) {
            button.addEventListener("click", function () {
                const chatId = String(button.getAttribute("data-chat-id") || "");
                if (!chatId) {
                    return;
                }

                selectConversation(chatId, true).catch(function (error) {
                    showToast(error.message, "error");
                });
            });
        });
    }

    function renderAttachments(attachments) {
        if (!Array.isArray(attachments) || attachments.length === 0) {
            return "";
        }

        const html = attachments.map(function (attachment) {
            const mediaKind = String(attachment.mediaKind || "document").toLowerCase();
            const url = safeUrl(attachment.url);
            if (!url) {
                return "";
            }

            if (mediaKind === "image") {
                return `<a href="${url}" target="_blank" rel="noopener noreferrer"><img src="${url}" alt="${escapeHtml(attachment.fileName || "imagem")}" /></a>`;
            }

            if (mediaKind === "video") {
                return `<video controls preload="metadata" src="${url}"></video>`;
            }

            const fileName = escapeHtml(attachment.fileName || "arquivo");
            return `<a href="${url}" target="_blank" rel="noopener noreferrer">${fileName}</a>`;
        }).join("");

        if (!html) {
            return "";
        }

        return `<div class="wa-attachments">${html}</div>`;
    }

    function renderMessageBubble(message) {
        const mineClass = message.isOutgoing ? " mine" : "";
        const textBlock = message.text
            ? `<div class="wa-text">${escapeHtml(message.text)}</div>`
            : "";

        return `
            <div class="wa-message-row${mineClass}" data-message-id="${escapeHtml(message.id)}">
                <article class="wa-bubble">
                    <div class="wa-meta">
                        <span class="wa-sender">${escapeHtml(message.senderDisplayName || "Contato")}</span>
                        <time>${escapeHtml(formatDateTime(message.sentAtUtc))}</time>
                    </div>
                    ${textBlock}
                    ${renderAttachments(message.attachments)}
                </article>
            </div>
        `;
    }

    function renderMessages(chatId) {
        const container = elements.messagesContainer;
        if (!container) {
            return;
        }

        const messages = state.messagesByChatId.get(chatId) || [];
        if (messages.length === 0) {
            container.innerHTML = "<div class='wa-empty-state'><p>Nenhuma mensagem nesta conversa ainda.</p></div>";
            return;
        }

        container.innerHTML = messages.map(renderMessageBubble).join("");
        container.scrollTop = container.scrollHeight;
    }

    function upsertConversation(summary) {
        if (!summary || summary.chatId === undefined || summary.chatId === null) {
            return;
        }

        const chatId = String(summary.chatId);
        const currentIndex = state.conversations.findIndex(function (item) {
            return String(item.chatId) === chatId;
        });

        if (currentIndex >= 0) {
            state.conversations[currentIndex] = summary;
        } else {
            state.conversations.push(summary);
        }

        sortConversations();
        renderConversations();

        if (state.activeChatId === chatId) {
            elements.activeConversationTitle.textContent = summary.title || `Chat ${chatId}`;
            elements.activeConversationSubtitle.textContent = "Conversa vinculada ao seu login";
        }
    }

    function cacheMessages(chatId, messages) {
        state.messagesByChatId.set(chatId, messages);

        const ids = new Set();
        messages.forEach(function (message) {
            ids.add(String(message.id || ""));
        });

        state.messageIdsByChatId.set(chatId, ids);
    }

    function appendMessage(message) {
        if (!message || message.chatId === undefined || message.chatId === null) {
            return;
        }

        const chatId = String(message.chatId);
        const ids = state.messageIdsByChatId.get(chatId) || new Set();
        const messageId = String(message.id || "");
        if (messageId && ids.has(messageId)) {
            return;
        }

        ids.add(messageId);
        state.messageIdsByChatId.set(chatId, ids);

        const messages = state.messagesByChatId.get(chatId) || [];
        messages.push(message);
        messages.sort(function (left, right) {
            return new Date(left.sentAtUtc).getTime() - new Date(right.sentAtUtc).getTime();
        });
        state.messagesByChatId.set(chatId, messages);

        if (state.activeChatId === chatId) {
            renderMessages(chatId);
        }
    }

    async function loadConversations() {
        const conversations = await requestJson("/api/chats");
        state.conversations = Array.isArray(conversations) ? conversations : [];
        sortConversations();
        renderConversations();

        if (!state.activeChatId && state.conversations.length > 0) {
            const firstChatId = String(state.conversations[0].chatId);
            await selectConversation(firstChatId, true);
        }
    }

    async function loadMessages(chatId) {
        const messages = await requestJson(`/api/chats/${encodeURIComponent(chatId)}/messages?take=300`);
        const normalized = Array.isArray(messages) ? messages : [];
        cacheMessages(chatId, normalized);
        renderMessages(chatId);
    }

    async function switchHubGroup(previousChatId, nextChatId) {
        if (!state.hub) {
            return;
        }

        if (previousChatId) {
            await state.hub.invoke("LeaveConversation", previousChatId);
        }

        if (nextChatId) {
            await state.hub.invoke("JoinConversation", nextChatId);
        }
    }

    async function selectConversation(chatId, forceReload) {
        const normalizedChatId = String(chatId || "").trim();
        if (!normalizedChatId) {
            return;
        }

        const previousChatId = state.activeChatId;
        state.activeChatId = normalizedChatId;
        renderConversations();

        const summary = state.conversations.find(function (item) {
            return String(item.chatId) === normalizedChatId;
        });

        elements.activeConversationTitle.textContent = summary
            ? summary.title || `Chat ${normalizedChatId}`
            : `Chat ${normalizedChatId}`;
        elements.activeConversationSubtitle.textContent = "Conversa vinculada ao seu login";

        await switchHubGroup(previousChatId, normalizedChatId);

        if (forceReload || !state.messagesByChatId.has(normalizedChatId)) {
            await loadMessages(normalizedChatId);
        } else {
            renderMessages(normalizedChatId);
        }
    }

    function renderPendingFiles() {
        const target = elements.pendingAttachments;
        if (!target) {
            return;
        }

        if (state.pendingFiles.length === 0) {
            target.innerHTML = "";
            return;
        }

        target.innerHTML = state.pendingFiles
            .map(function (file) {
                const sizeMb = file.size / (1024 * 1024);
                return `<span class="wa-pending-chip">${escapeHtml(file.name)} (${sizeMb.toFixed(2)} MB)</span>`;
            })
            .join("");
    }

    async function handleSendMessage(event) {
        event.preventDefault();

        const chatId = state.activeChatId;
        if (!chatId) {
            showToast("Selecione uma conversa antes de enviar.", "error");
            return;
        }

        const text = String(elements.messageInput.value || "").trim();
        if (!text && state.pendingFiles.length === 0) {
            return;
        }

        const formData = new FormData();
        if (text) {
            formData.append("text", text);
        }

        state.pendingFiles.forEach(function (file) {
            formData.append("files", file, file.name);
        });

        elements.sendMessageButton.disabled = true;

        try {
            const message = await requestJson(`/api/chats/${encodeURIComponent(chatId)}/messages`, {
                method: "POST",
                body: formData
            });

            appendMessage(message);
            elements.messageInput.value = "";
            elements.attachmentInput.value = "";
            state.pendingFiles = [];
            renderPendingFiles();
            renderMessages(chatId);
        } finally {
            elements.sendMessageButton.disabled = false;
        }
    }

    function wireComposerAutogrow() {
        if (!elements.messageInput) {
            return;
        }

        const adjustHeight = function () {
            elements.messageInput.style.height = "auto";
            const nextHeight = Math.min(elements.messageInput.scrollHeight, 140);
            elements.messageInput.style.height = `${nextHeight}px`;
        };

        elements.messageInput.addEventListener("input", adjustHeight);
        adjustHeight();
    }

    function wireSendOnEnter() {
        if (!elements.messageInput || !elements.sendMessageForm) {
            return;
        }

        elements.messageInput.addEventListener("keydown", function (event) {
            if (event.key !== "Enter") {
                return;
            }

            if (event.shiftKey || event.ctrlKey || event.altKey || event.metaKey || event.isComposing) {
                return;
            }

            event.preventDefault();

            if (elements.sendMessageButton.disabled) {
                return;
            }

            if (typeof elements.sendMessageForm.requestSubmit === "function") {
                elements.sendMessageForm.requestSubmit(elements.sendMessageButton);
                return;
            }

            elements.sendMessageForm.dispatchEvent(new Event("submit", { bubbles: true, cancelable: true }));
        });
    }

    async function setupHub() {
        if (!window.signalR) {
            setConnectionBadge("SignalR indisponivel", false);
            return;
        }

        const hub = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/telegram-chat")
            .withAutomaticReconnect()
            .build();

        hub.on("ReceiveConversationMessage", function (message) {
            appendMessage(message);

            if (state.activeChatId === String(message.chatId)) {
                renderMessages(state.activeChatId);
            }
        });

        hub.on("ConversationUpserted", function (summary) {
            upsertConversation(summary);
        });

        hub.onreconnected(async function () {
            setConnectionBadge("Online", true);

            if (state.activeChatId) {
                await switchHubGroup(null, state.activeChatId);
            }

            await loadConversations();
        });

        hub.onclose(function () {
            setConnectionBadge("Reconectando...", false);
        });

        await hub.start();
        state.hub = hub;
        setConnectionBadge("Online", true);

        if (state.activeChatId) {
            await switchHubGroup(null, state.activeChatId);
        }
    }

    async function bootstrap() {
        try {
            wireComposerAutogrow();
            wireSendOnEnter();

            elements.sendMessageForm.addEventListener("submit", function (event) {
                handleSendMessage(event).catch(function (error) {
                    showToast(error.message, "error");
                });
            });

            elements.attachmentInput.addEventListener("change", function () {
                state.pendingFiles = Array.from(elements.attachmentInput.files || []);
                renderPendingFiles();
            });

            await loadConversations();
            await setupHub();
        } catch (error) {
            showToast(error.message || "Falha ao iniciar painel.", "error");
            console.error(error);
        }
    }

    bootstrap();
})();
