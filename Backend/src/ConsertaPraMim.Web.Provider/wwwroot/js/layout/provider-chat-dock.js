(function () {
                const runtime = window.providerLayoutConfig || {};
                if (!runtime.isRealtimeEnabled) return;

                const currentUserId = String(runtime.currentUserId || "");
                const chatAccessToken = String(runtime.chatAccessToken || runtime.hubAccessToken || "");
                const chatApiBaseUrl = String(runtime.apiBaseUrl || "");
                const chatHubUrl = chatApiBaseUrl ? `${chatApiBaseUrl}/chatHub` : "/chatHub";
                const uploadUrl = chatApiBaseUrl ? `${chatApiBaseUrl}/api/chat-attachments/upload` : "/api/chat-attachments/upload";
                const allowedFileOrigins = new Set([window.location.origin]);
                if (chatApiBaseUrl) {
                    try {
                        allowedFileOrigins.add(new URL(chatApiBaseUrl, window.location.origin).origin);
                    } catch {
                        // no-op
                    }
                }

                const dockEl = document.getElementById("global-chat-dock");
                if (!dockEl || !currentUserId || !chatAccessToken) return;

                const chatConnection = new signalR.HubConnectionBuilder()
                    .withUrl(chatHubUrl, {
                        accessTokenFactory: function () { return chatAccessToken; }
                    })
                    .withAutomaticReconnect()
                    .build();

                const conversations = new Map();
                const providerStatusById = new Map();
                const userPresenceById = new Map();
                const seenMessageIds = new Set();
                let startPromise = null;
                let bootstrapActiveConversationsPromise = null;

                function normalizeId(value) {
                    return String(value || "").toLowerCase();
                }

                function conversationKey(requestId, providerId) {
                    return `${normalizeId(requestId)}:${normalizeId(providerId)}`;
                }

                function normalizeProviderStatus(value) {
                    const raw = String(value || "").trim();
                    if (raw === "Online" || raw === "Ausente" || raw === "EmAtendimento") {
                        return raw;
                    }

                    return "";
                }

                function normalizeCounterpartRole(value) {
                    const raw = String(value || "").trim();
                    if (raw === "Provider" || raw === "Client") {
                        return raw;
                    }

                    return "";
                }

                function providerStatusInfo(status) {
                    if (status === "Online") return { label: "Status: Online", dotClass: "status-online" };
                    if (status === "Ausente") return { label: "Status: Ausente", dotClass: "status-ausente" };
                    if (status === "EmAtendimento") return { label: "Status: Em atendimento", dotClass: "status-atendimento" };

                    return { label: "Status indisponivel", dotClass: "" };
                }

                function participantStatusInfo(conversation) {
                    const counterpartId = normalizeId(conversation?.counterpartUserId);
                    const counterpartRole = normalizeCounterpartRole(conversation?.counterpartRole);
                    const isOnline = counterpartId && userPresenceById.has(counterpartId)
                        ? userPresenceById.get(counterpartId)
                        : null;

                    if (counterpartRole === "Provider") {
                        if (isOnline === false) return { label: "Status: Offline", dotClass: "status-ausente" };

                        const providerStatus = normalizeProviderStatus(conversation?.providerStatus);
                        if (providerStatus) return providerStatusInfo(providerStatus);

                        if (isOnline === true) return { label: "Status: Online", dotClass: "status-online" };
                        return { label: "Status indisponivel", dotClass: "" };
                    }

                    if (counterpartRole === "Client") {
                        if (isOnline === true) return { label: "Status: Online", dotClass: "status-online" };
                        if (isOnline === false) return { label: "Status: Offline", dotClass: "status-ausente" };
                        return { label: "Status indisponivel", dotClass: "" };
                    }

                    const fallbackProviderStatus = normalizeProviderStatus(conversation?.providerStatus);
                    if (fallbackProviderStatus) return providerStatusInfo(fallbackProviderStatus);

                    return { label: "Status indisponivel", dotClass: "" };
                }

                function messageReceiptInfo(deliveredAt, readAt) {
                    if (readAt) return { icon: "fa-solid fa-check-double", cssClass: "read", label: "Lida" };
                    if (deliveredAt) return { icon: "fa-solid fa-check-double", cssClass: "delivered", label: "Entregue" };
                    return { icon: "fa-solid fa-check", cssClass: "sent", label: "Enviada" };
                }

                function escapeHtml(value) {
                    return String(value || "")
                        .replaceAll("&", "&amp;")
                        .replaceAll("<", "&lt;")
                        .replaceAll(">", "&gt;")
                        .replaceAll('"', "&quot;")
                        .replaceAll("'", "&#39;");
                }

                function rememberMessageId(messageId) {
                    const key = normalizeId(messageId);
                    if (!key) return false;
                    if (seenMessageIds.has(key)) return false;
                    seenMessageIds.add(key);

                    if (seenMessageIds.size > 800) {
                        const first = seenMessageIds.values().next().value;
                        seenMessageIds.delete(first);
                    }

                    return true;
                }

                function renderAttachment(attachment) {
                    const safeFileUrl = toSafeFileUrl(attachment.fileUrl);
                    if (!safeFileUrl) {
                        return `<div class="global-chat-attachment"><span class="text-muted small">Anexo bloqueado por seguranca.</span></div>`;
                    }

                    if (attachment.mediaKind === "image") {
                        return `<div class="global-chat-attachment"><a href="${safeFileUrl}" target="_blank" rel="noopener noreferrer"><img src="${safeFileUrl}" alt="Anexo" /></a></div>`;
                    }

                    if (attachment.mediaKind === "video") {
                        return `<div class="global-chat-attachment"><video controls src="${safeFileUrl}"></video></div>`;
                    }

                    return `<div class="global-chat-attachment"><a href="${safeFileUrl}" target="_blank" rel="noopener noreferrer">${escapeHtml(attachment.fileName || "Arquivo")}</a></div>`;
                }

                function toSafeFileUrl(value) {
                    const raw = String(value || "").trim();
                    if (!raw) return "";

                    try {
                        const parsed = new URL(raw, window.location.origin);
                        if ((parsed.protocol !== "http:" && parsed.protocol !== "https:") || !allowedFileOrigins.has(parsed.origin)) {
                            return "";
                        }

                        if (!parsed.pathname.toLowerCase().startsWith("/uploads/chat/")) {
                            return "";
                        }

                        return parsed.href;
                    } catch {
                        return "";
                    }
                }

                function renderMessage(message) {
                    const mine = normalizeId(message.senderId) === normalizeId(currentUserId);
                    const messageId = normalizeId(message.id);
                    const attachments = (message.attachments || []).map(renderAttachment).join("");
                    const text = message.text ? `<div class="global-chat-text">${escapeHtml(message.text)}</div>` : "";
                    const createdAt = message.createdAt ? new Date(message.createdAt).toLocaleString("pt-BR") : "";
                    const receipt = mine ? messageReceiptInfo(message.deliveredAt, message.readAt) : null;
                    const receiptHtml = receipt
                        ? `<span class="global-chat-receipt ${receipt.cssClass}" title="${receipt.label}" aria-label="${receipt.label}"><i class="${receipt.icon}"></i></span>`
                        : "";

                    return `
                        <div class="global-chat-row ${mine ? "mine" : ""}" data-message-id="${messageId}">
                            <div class="global-chat-bubble">
                                <div class="global-chat-meta"><strong>${escapeHtml(message.senderName || "Contato")}</strong> &bull; ${createdAt}${receiptHtml}</div>
                                ${text}
                                ${attachments}
                            </div>
                        </div>`;
                }

                function updateConversationStatus(conversation, status) {
                    if (status !== undefined) {
                        conversation.providerStatus = status || null;
                    }

                    const info = participantStatusInfo(conversation);

                    if (conversation.statusTextEl) {
                        conversation.statusTextEl.textContent = info.label;
                    }

                    if (conversation.statusDotEl) {
                        conversation.statusDotEl.className = `global-chat-status-dot${info.dotClass ? ` ${info.dotClass}` : ""}`;
                    }
                }

                function updateMessageReceipt(conversation, receiptPayload) {
                    if (!conversation || !conversation.bodyEl || !receiptPayload) return;

                    const messageId = normalizeId(receiptPayload.messageId);
                    if (!messageId) return;

                    const row = conversation.bodyEl.querySelector(`.global-chat-row.mine[data-message-id="${messageId}"]`);
                    if (!row) return;

                    const meta = row.querySelector(".global-chat-meta");
                    if (!meta) return;

                    const receipt = messageReceiptInfo(receiptPayload.deliveredAt, receiptPayload.readAt);
                    let receiptEl = meta.querySelector(".global-chat-receipt");
                    if (!receiptEl) {
                        receiptEl = document.createElement("span");
                        meta.appendChild(receiptEl);
                    }

                    receiptEl.className = `global-chat-receipt ${receipt.cssClass}`;
                    receiptEl.setAttribute("title", receipt.label);
                    receiptEl.setAttribute("aria-label", receipt.label);
                    receiptEl.innerHTML = `<i class="${receipt.icon}"></i>`;
                }

                function setMinimized(conversation, minimized) {
                    conversation.minimized = minimized;
                    conversation.widgetEl.classList.toggle("minimized", minimized);
                    if (!minimized) {
                        requestReceiptSync(conversation);
                    }
                }

                function setMaximized(conversation, maximized) {
                    conversation.maximized = maximized;
                    conversation.widgetEl.classList.toggle("maximized", maximized);
                    if (conversation.maximizeBtn) {
                        conversation.maximizeBtn.innerHTML = maximized
                            ? '<i class="fa-solid fa-compress"></i>'
                            : '<i class="fa-solid fa-expand"></i>';
                        conversation.maximizeBtn.title = maximized ? "Restaurar" : "Maximizar";
                    }
                    if (maximized) {
                        setMinimized(conversation, false);
                    }
                }

                function scrollBottom(conversation) {
                    if (!conversation || !conversation.bodyEl) return;
                    conversation.bodyEl.scrollTop = conversation.bodyEl.scrollHeight;
                }

                function closeConversation(key) {
                    const conversation = conversations.get(key);
                    if (!conversation) return;
                    conversation.widgetEl.remove();
                    conversations.delete(key);
                }

                function buildWidget(conversation) {
                    const widgetEl = document.createElement("div");
                    widgetEl.className = "global-chat-widget";
                    widgetEl.innerHTML = `
                        <div class="global-chat-header">
                            <div>
                                <div class="fw-semibold global-chat-title"></div>
                                <div class="global-chat-status">
                                    <span class="global-chat-status-dot"></span>
                                    <span class="global-chat-status-text">Status indisponivel</span>
                                </div>
                            </div>
                            <div class="global-chat-actions">
                                <button type="button" class="global-chat-minimize" title="Minimizar" aria-label="Minimizar"><i class="fa-solid fa-minus"></i></button>
                                <button type="button" class="global-chat-maximize" title="Maximizar" aria-label="Maximizar"><i class="fa-solid fa-expand"></i></button>
                                <button type="button" class="global-chat-close" title="Fechar" aria-label="Fechar"><i class="fa-solid fa-xmark"></i></button>
                            </div>
                        </div>
                        <div class="global-chat-body"></div>
                        <div class="global-chat-footer">
                            <div class="input-group input-group-sm global-chat-inputbar">
                                <button class="btn global-chat-attach" type="button" title="Anexar arquivos" aria-label="Anexar arquivos"><i class="fa-solid fa-paperclip"></i></button>
                                <input class="global-chat-files d-none" type="file" multiple accept="image/*,video/*" />
                                <textarea class="global-chat-text form-control" rows="2" placeholder="Digite uma mensagem..."></textarea>
                                <button class="global-chat-send btn btn-primary" type="button" title="Enviar" aria-label="Enviar"><i class="fa-solid fa-paper-plane"></i></button>
                            </div>
                        </div>`;

                    conversation.widgetEl = widgetEl;
                    conversation.titleEl = widgetEl.querySelector(".global-chat-title");
                    conversation.statusDotEl = widgetEl.querySelector(".global-chat-status-dot");
                    conversation.statusTextEl = widgetEl.querySelector(".global-chat-status-text");
                    conversation.bodyEl = widgetEl.querySelector(".global-chat-body");
                    conversation.filesEl = widgetEl.querySelector(".global-chat-files");
                    conversation.attachBtn = widgetEl.querySelector(".global-chat-attach");
                    conversation.textEl = widgetEl.querySelector(".global-chat-text");
                    conversation.sendBtn = widgetEl.querySelector(".global-chat-send");

                    const minimizeBtn = widgetEl.querySelector(".global-chat-minimize");
                    const maximizeBtn = widgetEl.querySelector(".global-chat-maximize");
                    const closeBtn = widgetEl.querySelector(".global-chat-close");
                    conversation.maximizeBtn = maximizeBtn;

                    minimizeBtn.addEventListener("click", function () {
                        setMinimized(conversation, !conversation.minimized);
                    });

                    maximizeBtn.addEventListener("click", function () {
                        setMaximized(conversation, !conversation.maximized);
                    });

                    closeBtn.addEventListener("click", function () {
                        closeConversation(conversation.key);
                    });

                    conversation.attachBtn.addEventListener("click", function () {
                        conversation.filesEl.click();
                    });

                    conversation.sendBtn.addEventListener("click", function () {
                        sendMessage(conversation);
                    });

                    conversation.textEl.addEventListener("keydown", function (event) {
                        if (event.key === "Enter" && !event.shiftKey) {
                            event.preventDefault();
                            sendMessage(conversation);
                        }
                    });

                    conversation.textEl.addEventListener("focus", function () {
                        requestReceiptSync(conversation);
                    });

                    dockEl.append(widgetEl);
                }

                function getOrCreateConversation(detail) {
                    const requestId = String(detail.requestId || "");
                    const providerId = String(detail.providerId || "");
                    if (!requestId || !providerId) return null;

                    const key = conversationKey(requestId, providerId);
                    let conversation = conversations.get(key);
                    if (!conversation) {
                        const providerStatus = providerStatusById.get(normalizeId(providerId)) || null;
                        conversation = {
                            key: key,
                            requestId: requestId,
                            providerId: providerId,
                            providerStatus: providerStatus,
                            counterpartUserId: null,
                            counterpartRole: "",
                            title: detail.title || "Chat",
                            minimized: false,
                            maximized: false,
                            joined: false,
                            historyLoaded: false,
                            historyLoading: null,
                            receiptSyncInFlight: false,
                            receiptSyncPending: false
                        };
                        buildWidget(conversation);
                        conversations.set(key, conversation);
                    }

                    if (detail.title) {
                        conversation.title = detail.title;
                    }

                    conversation.titleEl.textContent = conversation.title || "Chat";
                    updateConversationStatus(conversation, conversation.providerStatus);

                    const shouldMaximize = detail.maximized === true;
                    const shouldMinimize = !shouldMaximize && detail.minimized === true;

                    if (shouldMaximize) {
                        setMaximized(conversation, true);
                    } else {
                        setMaximized(conversation, false);
                        setMinimized(conversation, shouldMinimize);
                    }

                    return conversation;
                }

                async function ensureConnected() {
                    if (chatConnection.state === signalR.HubConnectionState.Connected) return;
                    if (!startPromise) {
                        startPromise = chatConnection.start()
                            .then(function () {
                                return chatConnection.invoke("JoinPersonalGroup");
                            })
                            .finally(function () {
                                startPromise = null;
                            });
                    }
                    await startPromise;
                }

                async function bootstrapActiveConversations() {
                    if (bootstrapActiveConversationsPromise) {
                        await bootstrapActiveConversationsPromise;
                        return;
                    }

                    bootstrapActiveConversationsPromise = (async function () {
                        await ensureConnected();
                        const activeConversations = await chatConnection.invoke("GetMyActiveConversations");
                        const list = Array.isArray(activeConversations) ? activeConversations : [];

                        list.forEach(function (item) {
                            if (!item || !item.requestId || !item.providerId) return;

                            const conversation = getOrCreateConversation({
                                requestId: item.requestId,
                                providerId: item.providerId,
                                title: item.title || "Chat",
                                minimized: true
                            });

                            if (!conversation) return;

                            if (item.counterpartUserId) {
                                conversation.counterpartUserId = item.counterpartUserId;
                            }

                            conversation.counterpartRole = normalizeCounterpartRole(item.counterpartRole);

                            const counterpartId = normalizeId(item.counterpartUserId);
                            if (counterpartId && typeof item.counterpartIsOnline === "boolean") {
                                userPresenceById.set(counterpartId, !!item.counterpartIsOnline);
                            }

                            const normalizedStatus = normalizeProviderStatus(item.providerStatus);
                            if (normalizedStatus && conversation.counterpartRole === "Provider") {
                                providerStatusById.set(normalizeId(item.counterpartUserId || item.providerId), normalizedStatus);
                                conversation.providerStatus = normalizedStatus;
                            }

                            updateConversationStatus(conversation);
                        });
                    })()
                        .catch(console.error)
                        .finally(function () {
                            bootstrapActiveConversationsPromise = null;
                        });

                    await bootstrapActiveConversationsPromise;
                }

                async function ensureConversationJoined(conversation) {
                    await ensureConnected();
                    if (conversation.joined) return;
                    await chatConnection.invoke(
                        "JoinRequestChat",
                        conversation.requestId,
                        conversation.providerId
                    );
                    conversation.joined = true;

                    try {
                        const participant = await chatConnection.invoke(
                            "GetConversationParticipantPresence",
                            conversation.requestId,
                            conversation.providerId
                        );

                        if (participant && participant.userId) {
                            const participantId = normalizeId(participant.userId);
                            const participantRole = normalizeCounterpartRole(participant.role);

                            conversation.counterpartUserId = participant.userId;
                            conversation.counterpartRole = participantRole;

                            if (typeof participant.isOnline === "boolean") {
                                userPresenceById.set(participantId, participant.isOnline);
                            }

                            if (participantRole === "Provider") {
                                const normalizedStatus = normalizeProviderStatus(participant.operationalStatus);
                                if (normalizedStatus) {
                                    providerStatusById.set(participantId, normalizedStatus);
                                    conversation.providerStatus = normalizedStatus;
                                }
                            }

                            updateConversationStatus(conversation);
                        }
                    } catch (error) {
                        console.error(error);
                    }
                }

                function shouldMarkAsRead(conversation) {
                    return !!conversation && !conversation.minimized && !document.hidden;
                }

                async function syncConversationReceipts(conversation) {
                    await ensureConversationJoined(conversation);
                    await chatConnection.invoke("MarkConversationDelivered", conversation.requestId, conversation.providerId);
                    if (shouldMarkAsRead(conversation)) {
                        await chatConnection.invoke("MarkConversationRead", conversation.requestId, conversation.providerId);
                    }
                }

                function requestReceiptSync(conversation) {
                    if (!conversation || !conversation.historyLoaded) return;

                    if (conversation.receiptSyncInFlight) {
                        conversation.receiptSyncPending = true;
                        return;
                    }

                    conversation.receiptSyncInFlight = true;
                    syncConversationReceipts(conversation)
                        .catch(console.error)
                        .finally(function () {
                            conversation.receiptSyncInFlight = false;
                            if (conversation.receiptSyncPending) {
                                conversation.receiptSyncPending = false;
                                requestReceiptSync(conversation);
                            }
                        });
                }

                async function loadHistory(conversation) {
                    if (!conversation) return;
                    if (conversation.historyLoaded) return;
                    if (conversation.historyLoading) {
                        await conversation.historyLoading;
                        return;
                    }

                    conversation.bodyEl.innerHTML = "<div class='text-muted small'>Carregando historico...</div>";
                    conversation.historyLoading = (async function () {
                        await ensureConversationJoined(conversation);
                        const history = await chatConnection.invoke(
                            "GetHistory",
                            conversation.requestId,
                            conversation.providerId
                        );

                        const list = history || [];
                        conversation.bodyEl.innerHTML = list.map(renderMessage).join("");
                        list.forEach(function (item) { rememberMessageId(item.id); });
                        conversation.historyLoaded = true;
                        scrollBottom(conversation);
                        requestReceiptSync(conversation);
                    })().catch(function (error) {
                        console.error(error);
                        conversation.bodyEl.innerHTML = "<div class='text-danger small'>Nao foi possivel carregar o historico.</div>";
                        throw error;
                    }).finally(function () {
                        conversation.historyLoading = null;
                    });

                    await conversation.historyLoading;
                }

                async function uploadAttachments(conversation, files) {
                    const uploaded = [];
                    for (const file of files) {
                        const formData = new FormData();
                        formData.append("requestId", conversation.requestId);
                        formData.append("providerId", conversation.providerId);
                        formData.append("file", file);

                        const response = await fetch(uploadUrl, {
                            method: "POST",
                            headers: {
                                Authorization: `Bearer ${chatAccessToken}`
                            },
                            body: formData
                        });
                        if (!response.ok) throw new Error("Falha ao enviar anexo.");

                        const data = await response.json();
                        uploaded.push({
                            fileUrl: data.fileUrl,
                            fileName: data.fileName,
                            contentType: data.contentType,
                            sizeBytes: data.sizeBytes
                        });
                    }

                    return uploaded;
                }

                async function sendMessage(conversation) {
                    if (!conversation) return;

                    const text = conversation.textEl.value.trim();
                    const files = Array.from(conversation.filesEl.files || []);
                    if (!text && files.length === 0) return;

                    conversation.sendBtn.disabled = true;
                    try {
                        await ensureConversationJoined(conversation);
                        const attachments = await uploadAttachments(conversation, files);
                        await chatConnection.invoke(
                            "SendMessage",
                            conversation.requestId,
                            conversation.providerId,
                            text,
                            attachments
                        );
                        conversation.textEl.value = "";
                        conversation.filesEl.value = "";
                    } catch (error) {
                        console.error(error);
                        alert("Nao foi possivel enviar a mensagem.");
                    } finally {
                        conversation.sendBtn.disabled = false;
                    }
                }

                chatConnection.on("ReceiveProviderStatus", function (payload) {
                    if (!payload || !payload.providerId) return;

                    const providerId = normalizeId(payload.providerId);
                    const status = normalizeProviderStatus(payload.status);
                    if (!providerId || !status) return;

                    providerStatusById.set(providerId, status);
                    conversations.forEach(function (conversation) {
                        if (normalizeId(conversation.counterpartUserId || conversation.providerId) === providerId) {
                            updateConversationStatus(conversation, status);
                        }
                    });

                    window.dispatchEvent(new CustomEvent("cpm:provider-status", {
                        detail: {
                            providerId: providerId,
                            status: status,
                            updatedAt: payload.updatedAt || new Date().toISOString()
                        }
                    }));
                });

                chatConnection.on("ReceiveUserPresence", function (payload) {
                    if (!payload || !payload.userId) return;

                    const userId = normalizeId(payload.userId);
                    if (!userId) return;

                    userPresenceById.set(userId, !!payload.isOnline);
                    conversations.forEach(function (conversation) {
                        if (normalizeId(conversation.counterpartUserId) === userId) {
                            updateConversationStatus(conversation);
                        }
                    });
                });

                chatConnection.on("ReceiveMessageReceiptUpdated", function (receipt) {
                    if (!receipt || !receipt.requestId || !receipt.providerId) return;
                    const key = conversationKey(receipt.requestId, receipt.providerId);
                    const conversation = conversations.get(key);
                    if (!conversation) return;

                    updateMessageReceipt(conversation, receipt);
                });

                chatConnection.on("ReceiveChatMessage", async function (message) {
                    if (!message || !message.requestId || !message.providerId) return;
                    if (!rememberMessageId(message.id)) return;

                    const key = conversationKey(message.requestId, message.providerId);
                    const existingConversation = conversations.get(key);
                    const conversation = getOrCreateConversation({
                        requestId: message.requestId,
                        providerId: message.providerId,
                        title: existingConversation ? null : `Chat com ${message.senderName || "Contato"}`
                    });
                    if (!conversation) return;

                    if (!conversation.historyLoaded) {
                        await loadHistory(conversation);
                        return;
                    }

                    if (normalizeId(message.senderId) !== normalizeId(currentUserId) && !conversation.counterpartUserId) {
                        conversation.counterpartUserId = message.senderId;
                        conversation.counterpartRole = normalizeCounterpartRole(message.senderRole);
                        userPresenceById.set(normalizeId(message.senderId), true);
                        updateConversationStatus(conversation);
                    }

                    conversation.bodyEl.insertAdjacentHTML("beforeend", renderMessage(message));
                    scrollBottom(conversation);

                    if (normalizeId(message.senderId) !== normalizeId(currentUserId)) {
                        requestReceiptSync(conversation);
                    }
                });

                window.addEventListener("cpm:open-chat", function (event) {
                    const detail = event.detail || {};
                    if (!detail.requestId || !detail.providerId) return;

                    const conversation = getOrCreateConversation({
                        requestId: detail.requestId,
                        providerId: detail.providerId,
                        title: detail.title || "Chat",
                        minimized: detail.minimized === true,
                        maximized: detail.maximized === true
                    });
                    if (!conversation) return;

                    const shouldLoadHistory = detail.loadHistory !== false;
                    if (!shouldLoadHistory) {
                        return;
                    }

                    loadHistory(conversation)
                        .then(function () {
                            requestReceiptSync(conversation);
                        })
                        .catch(console.error);
                });

                document.addEventListener("visibilitychange", function () {
                    if (document.hidden) return;
                    conversations.forEach(function (conversation) {
                        if (!conversation.minimized) {
                            requestReceiptSync(conversation);
                        }
                    });
                });

                chatConnection.onreconnected(function () {
                    const reopenPromises = [];
                    conversations.forEach(function (conversation) {
                        conversation.joined = false;
                        reopenPromises.push(ensureConversationJoined(conversation));
                    });
                    Promise.all(reopenPromises)
                        .then(function () {
                            conversations.forEach(function (conversation) {
                                requestReceiptSync(conversation);
                            });
                            window.dispatchEvent(new CustomEvent("cpm:realtime-reconnected", {
                                detail: {
                                    source: "chatHub",
                                    at: new Date().toISOString()
                                }
                            }));
                        })
                        .catch(console.error);
                });

                ensureConnected()
                    .then(function () {
                        return bootstrapActiveConversations();
                    })
                    .catch(console.error);
            })();
