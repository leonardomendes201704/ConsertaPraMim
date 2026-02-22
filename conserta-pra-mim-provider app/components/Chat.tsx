import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ProviderAuthSession, ProviderChatConversationSummary, ProviderChatMessage, ProviderChatMessageReceipt } from '../types';
import {
  fetchMobileProviderChatMessages,
  markMobileProviderChatDelivered,
  markMobileProviderChatRead,
  resolveMobileProviderChatAttachmentUrl,
  sendMobileProviderChatMessage,
  uploadMobileProviderChatAttachments
} from '../services/mobileProvider';
import { joinProviderRealtimeConversation, subscribeToProviderRealtimeChatEvents } from '../services/realtimeChat';

interface Props {
  authSession: ProviderAuthSession | null;
  conversation: ProviderChatConversationSummary;
  onBack: () => void;
}

function normalizeId(value?: string | null): string {
  return String(value || '').trim().toLowerCase();
}

function formatTime(value?: string): string {
  if (!value) {
    return '--:--';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return '--:--';
  }

  const hh = String(date.getHours()).padStart(2, '0');
  const mm = String(date.getMinutes()).padStart(2, '0');
  return `${hh}:${mm}`;
}

function formatDateChip(value?: string): string {
  if (!value) {
    return 'HOJE';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return 'HOJE';
  }

  const now = new Date();
  const isSameDay = date.getDate() === now.getDate()
    && date.getMonth() === now.getMonth()
    && date.getFullYear() === now.getFullYear();

  if (isSameDay) {
    return 'HOJE';
  }

  const dd = String(date.getDate()).padStart(2, '0');
  const mm = String(date.getMonth() + 1).padStart(2, '0');
  return `${dd}/${mm}`;
}

function resolveMessageStatus(message: ProviderChatMessage): string {
  if (message.readAt) {
    return 'Lido';
  }

  if (message.deliveredAt) {
    return 'Entregue';
  }

  return 'Enviado';
}

function sortByCreatedAt(messages: ProviderChatMessage[]): ProviderChatMessage[] {
  return [...messages].sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime());
}

const Chat: React.FC<Props> = ({ authSession, conversation, onBack }) => {
  const [messages, setMessages] = useState<ProviderChatMessage[]>([]);
  const [inputText, setInputText] = useState('');
  const [selectedFiles, setSelectedFiles] = useState<File[]>([]);
  const [isLoadingHistory, setIsLoadingHistory] = useState(true);
  const [isSending, setIsSending] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');
  const [counterpartPresence, setCounterpartPresence] = useState<{ isOnline: boolean; status?: string }>({
    isOnline: conversation.counterpartIsOnline,
    status: conversation.providerStatus
  });

  const fileInputRef = useRef<HTMLInputElement>(null);
  const scrollRef = useRef<HTMLDivElement>(null);

  const requestId = useMemo(() => String(conversation.requestId || '').trim(), [conversation.requestId]);
  const providerId = useMemo(() => String(conversation.providerId || '').trim(), [conversation.providerId]);
  const counterpartUserId = useMemo(
    () => String(conversation.counterpartUserId || '').trim(),
    [conversation.counterpartUserId]
  );
  const counterpartName = conversation.counterpartName || 'Cliente';

  useEffect(() => {
    setCounterpartPresence({
      isOnline: conversation.counterpartIsOnline,
      status: conversation.providerStatus
    });
  }, [conversation.counterpartIsOnline, conversation.providerStatus, conversation.counterpartUserId]);

  useEffect(() => {
    if (!scrollRef.current) {
      return;
    }

    scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
  }, [messages, isLoadingHistory]);

  const mergeReceiptInMessages = useCallback((receipt: ProviderChatMessageReceipt) => {
    setMessages((previous) => previous.map((message) => {
      if (normalizeId(message.id) !== normalizeId(receipt.messageId)) {
        return message;
      }

      return {
        ...message,
        deliveredAt: receipt.deliveredAt || message.deliveredAt,
        readAt: receipt.readAt || message.readAt
      };
    }));
  }, []);

  const markSeen = useCallback(async () => {
    if (!authSession?.token || !requestId) {
      return;
    }

    try {
      await markMobileProviderChatDelivered(authSession.token, requestId);
      await markMobileProviderChatRead(authSession.token, requestId);
    } catch {
      // keep silent, next retry updates receipts
    }
  }, [authSession?.token, requestId]);

  const loadConversation = useCallback(async () => {
    if (!authSession?.token) {
      setErrorMessage('Sessao expirada. Faca login novamente.');
      setIsLoadingHistory(false);
      return;
    }

    if (!requestId || !providerId) {
      setErrorMessage('Nao foi possivel abrir o chat deste pedido.');
      setIsLoadingHistory(false);
      return;
    }

    setIsLoadingHistory(true);
    setErrorMessage('');

    try {
      const joined = await joinProviderRealtimeConversation(authSession.token, requestId, providerId);
      if (!joined) {
        setErrorMessage('Voce nao possui permissao para acessar esta conversa.');
        setMessages([]);
        return;
      }

      const history = await fetchMobileProviderChatMessages(authSession.token, requestId);
      setMessages(sortByCreatedAt(history));
      await markSeen();
    } catch {
      setErrorMessage('Nao foi possivel carregar a conversa em tempo real.');
      setMessages([]);
    } finally {
      setIsLoadingHistory(false);
    }
  }, [authSession?.token, providerId, requestId, markSeen]);

  useEffect(() => {
    void loadConversation();
  }, [loadConversation]);

  useEffect(() => {
    if (!authSession?.token || !requestId || !providerId) {
      return undefined;
    }

    const unsubscribe = subscribeToProviderRealtimeChatEvents({
      onChatMessage: (message) => {
        if (
          normalizeId(message.requestId) !== normalizeId(requestId)
          || normalizeId(message.providerId) !== normalizeId(providerId)
        ) {
          return;
        }

        setMessages((previous) => {
          const existingIndex = previous.findIndex((item) => normalizeId(item.id) === normalizeId(message.id));
          if (existingIndex >= 0) {
            const updated = [...previous];
            updated[existingIndex] = message;
            return sortByCreatedAt(updated);
          }

          return sortByCreatedAt([...previous, message]);
        });

        if (normalizeId(message.senderId) !== normalizeId(authSession.userId)) {
          void markSeen();
        }
      },
      onMessageReceiptUpdated: (receipt) => {
        if (
          normalizeId(receipt.requestId) !== normalizeId(requestId)
          || normalizeId(receipt.providerId) !== normalizeId(providerId)
        ) {
          return;
        }

        mergeReceiptInMessages(receipt);
      },
      onUserPresence: (presence) => {
        if (normalizeId(presence.userId) !== normalizeId(counterpartUserId)) {
          return;
        }

        setCounterpartPresence((previous) => ({
          ...previous,
          isOnline: presence.isOnline
        }));
      },
      onProviderStatus: (payload) => {
        if (normalizeId(payload.providerId) !== normalizeId(providerId)) {
          return;
        }

        setCounterpartPresence((previous) => ({
          ...previous,
          status: payload.status || previous.status
        }));
      }
    });

    return () => {
      unsubscribe();
    };
  }, [
    authSession?.token,
    authSession?.userId,
    counterpartUserId,
    mergeReceiptInMessages,
    markSeen,
    providerId,
    requestId
  ]);

  const handleOpenFilePicker = () => {
    fileInputRef.current?.click();
  };

  const handleFilesSelected = (event: React.ChangeEvent<HTMLInputElement>) => {
    const incoming = Array.from(event.target.files || []);
    if (incoming.length === 0) {
      return;
    }

    setSelectedFiles((previous) => [...previous, ...incoming]);
    event.target.value = '';
  };

  const removeSelectedFile = (index: number) => {
    setSelectedFiles((previous) => previous.filter((_, itemIndex) => itemIndex !== index));
  };

  const handleSend = async (event: React.FormEvent) => {
    event.preventDefault();

    if (!authSession?.token || !requestId || !providerId) {
      setErrorMessage('Nao foi possivel enviar mensagem. Chat indisponivel.');
      return;
    }

    const normalizedText = inputText.trim();
    if (!normalizedText && selectedFiles.length === 0) {
      return;
    }

    setIsSending(true);
    setErrorMessage('');

    try {
      const uploadedAttachments = selectedFiles.length > 0
        ? await uploadMobileProviderChatAttachments(authSession.token, requestId, selectedFiles)
        : [];

      const sent = await sendMobileProviderChatMessage(authSession.token, requestId, normalizedText, uploadedAttachments);
      if (sent) {
        setMessages((previous) => {
          const existingIndex = previous.findIndex((item) => normalizeId(item.id) === normalizeId(sent.id));
          if (existingIndex >= 0) {
            const updated = [...previous];
            updated[existingIndex] = sent;
            return sortByCreatedAt(updated);
          }

          return sortByCreatedAt([...previous, sent]);
        });
      }

      setInputText('');
      setSelectedFiles([]);
      await markSeen();
    } catch {
      setErrorMessage('Nao foi possivel enviar a mensagem agora.');
    } finally {
      setIsSending(false);
    }
  };

  return (
    <div className="flex flex-col h-screen bg-[#f4f7fb] overflow-hidden">
      <header className="flex items-center bg-white p-4 border-b border-[#e4e7ec] sticky top-0 z-20 shadow-sm">
        <button onClick={onBack} className="p-2 -ml-2 text-primary hover:bg-primary/5 rounded-full transition-colors">
          <span className="material-symbols-outlined">arrow_back</span>
        </button>
        <div className="flex items-center gap-3 flex-1 ml-2 min-w-0">
          <div className="relative">
            <img
              src={`https://i.pravatar.cc/120?u=${counterpartUserId || requestId}`}
              className="size-10 rounded-full object-cover border border-primary/10"
              alt={counterpartName}
            />
            <div className={`absolute bottom-0 right-0 size-3 rounded-full border-2 border-white ${
              counterpartPresence.isOnline ? 'bg-green-500' : 'bg-[#96a7a7]'
            }`}
            />
          </div>
          <div className="min-w-0">
            <h2 className="text-[#101828] text-sm font-bold truncate">{counterpartName}</h2>
            <p className="text-[10px] text-[#667085] font-bold uppercase tracking-wider truncate">
              {counterpartPresence.isOnline ? 'Online agora' : 'Offline'}
              {counterpartPresence.status ? ` - ${counterpartPresence.status}` : ''}
            </p>
          </div>
        </div>
      </header>

      {errorMessage ? (
        <div className="px-4 pt-3">
          <div className="rounded-xl border border-amber-200 bg-amber-50 p-3 text-amber-900 text-xs">
            {errorMessage}
          </div>
        </div>
      ) : null}

      <div
        ref={scrollRef}
        className="flex-1 overflow-y-auto p-4 space-y-4 no-scrollbar"
      >
        {isLoadingHistory ? (
          <div className="flex flex-col items-center justify-center h-full text-[#667085]">
            <span className="material-symbols-outlined text-5xl animate-spin">progress_activity</span>
            <p className="text-sm mt-3">Carregando conversa...</p>
          </div>
        ) : (
          <>
            <div className="flex justify-center my-4">
              <span className="text-[10px] font-bold text-[#667085] bg-white px-3 py-1 rounded-full shadow-sm border border-primary/5">
                {formatDateChip(messages[0]?.createdAt)}
              </span>
            </div>

            {messages.length === 0 ? (
              <div className="text-center py-6 text-[#667085] text-sm">
                Nenhuma mensagem ainda. Comece a conversa.
              </div>
            ) : messages.map((message) => {
              const isMine = normalizeId(message.senderId) === normalizeId(authSession?.userId);

              return (
                <div
                  key={message.id}
                  className={`flex w-full ${isMine ? 'justify-end' : 'justify-start'} animate-fadeIn`}
                >
                  <div className={`max-w-[84%] p-3 rounded-2xl text-sm shadow-sm ${
                    isMine
                      ? 'bg-primary text-white rounded-tr-none'
                      : 'bg-white text-[#101828] rounded-tl-none border border-[#e4e7ec]'
                  }`}
                  >
                    {message.text ? (
                      <p className="leading-relaxed whitespace-pre-wrap">{message.text}</p>
                    ) : null}

                    {message.attachments.length > 0 ? (
                      <div className={`mt-2 space-y-2 ${message.text ? '' : 'mt-0'}`}>
                        {message.attachments.map((attachment) => {
                          const mediaKind = String(attachment.mediaKind || '').toLowerCase();
                          const resolvedUrl = resolveMobileProviderChatAttachmentUrl(attachment.fileUrl);

                          if (mediaKind === 'image') {
                            return (
                              <a
                                key={`${message.id}-${attachment.fileUrl}`}
                                href={resolvedUrl}
                                target="_blank"
                                rel="noreferrer"
                                className="block"
                              >
                                <img
                                  src={resolvedUrl}
                                  alt={attachment.fileName}
                                  className="max-h-52 w-full object-cover rounded-xl border border-white/20"
                                />
                              </a>
                            );
                          }

                          if (mediaKind === 'video') {
                            return (
                              <video
                                key={`${message.id}-${attachment.fileUrl}`}
                                src={resolvedUrl}
                                controls
                                className="max-h-52 w-full rounded-xl border border-white/20 bg-black"
                              />
                            );
                          }

                          return (
                            <a
                              key={`${message.id}-${attachment.fileUrl}`}
                              href={resolvedUrl}
                              target="_blank"
                              rel="noreferrer"
                              className={`flex items-center gap-2 rounded-xl px-3 py-2 text-xs font-semibold ${
                                isMine ? 'bg-white/15 text-white' : 'bg-primary/5 text-primary'
                              }`}
                            >
                              <span className="material-symbols-outlined text-base">attach_file</span>
                              <span className="truncate">{attachment.fileName}</span>
                            </a>
                          );
                        })}
                      </div>
                    ) : null}

                    <div className={`text-[9px] mt-2 font-medium ${isMine ? 'text-white/70' : 'text-[#667085]'}`}>
                      {formatTime(message.createdAt)}
                      {isMine ? ` - ${resolveMessageStatus(message)}` : ''}
                    </div>
                  </div>
                </div>
              );
            })}
          </>
        )}
      </div>

      <div className="p-4 bg-white border-t border-[#e4e7ec] pb-8">
        {selectedFiles.length > 0 ? (
          <div className="mb-3 flex flex-wrap gap-2">
            {selectedFiles.map((file, index) => (
              <div
                key={`${file.name}-${file.size}-${index}`}
                className="inline-flex items-center gap-1 rounded-full border border-primary/20 bg-primary/5 px-3 py-1 text-xs text-primary"
              >
                <span className="truncate max-w-[140px]">{file.name}</span>
                <button
                  type="button"
                  onClick={() => removeSelectedFile(index)}
                  className="inline-flex h-4 w-4 items-center justify-center rounded-full hover:bg-primary/10"
                >
                  <span className="material-symbols-outlined text-sm">close</span>
                </button>
              </div>
            ))}
          </div>
        ) : null}

        <form onSubmit={handleSend} className="flex items-center gap-2">
          <button
            type="button"
            onClick={handleOpenFilePicker}
            className="p-2 text-[#667085] hover:text-primary transition-colors"
            title="Adicionar anexo"
          >
            <span className="material-symbols-outlined">add_circle</span>
          </button>
          <input
            ref={fileInputRef}
            type="file"
            multiple
            accept=".jpg,.jpeg,.png,.webp,.mp4,.webm,.mov"
            className="hidden"
            onChange={handleFilesSelected}
          />
          <div className="flex-1 relative">
            <input
              type="text"
              value={inputText}
              onChange={(event) => setInputText(event.target.value)}
              placeholder="Digite sua mensagem..."
              className="w-full h-12 bg-[#f5f8f8] border-none rounded-full px-4 pr-10 text-sm focus:ring-2 focus:ring-primary/20 placeholder:text-[#667085]"
              disabled={isSending}
            />
            <button
              type="button"
              onClick={handleOpenFilePicker}
              className="absolute right-3 top-1/2 -translate-y-1/2 text-[#667085]"
            >
              <span className="material-symbols-outlined text-xl">image</span>
            </button>
          </div>
          <button
            type="submit"
            disabled={(!inputText.trim() && selectedFiles.length === 0) || isSending}
            className="size-12 bg-primary text-white rounded-full flex items-center justify-center shadow-lg shadow-primary/20 disabled:opacity-50 active:scale-90 transition-all"
          >
            <span className="material-symbols-outlined">{isSending ? 'hourglass_top' : 'send'}</span>
          </button>
        </form>
      </div>
    </div>
  );
};

export default Chat;
