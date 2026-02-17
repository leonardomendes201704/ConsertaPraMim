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
import {
  joinProviderRealtimeConversation,
  subscribeToProviderRealtimeChatEvents
} from '../services/realtimeChat';

interface Props {
  authSession: ProviderAuthSession | null;
  conversation: ProviderChatConversationSummary;
  onBack: () => void;
}

function normalizeId(value?: string | null): string {
  return String(value || '').trim().toLowerCase();
}

function formatMessageTime(value?: string): string {
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

function resolveMessageStatus(message: ProviderChatMessage): string {
  if (message.readAt) {
    return 'Lido';
  }

  if (message.deliveredAt) {
    return 'Entregue';
  }

  return 'Enviado';
}

function sortByDate(messages: ProviderChatMessage[]): ProviderChatMessage[] {
  return [...messages].sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime());
}

const Chat: React.FC<Props> = ({ authSession, conversation, onBack }) => {
  const [messages, setMessages] = useState<ProviderChatMessage[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [inputText, setInputText] = useState('');
  const [selectedFiles, setSelectedFiles] = useState<File[]>([]);
  const [sending, setSending] = useState(false);

  const requestId = useMemo(() => String(conversation.requestId || '').trim(), [conversation.requestId]);
  const providerId = useMemo(() => String(conversation.providerId || '').trim(), [conversation.providerId]);

  const fileInputRef = useRef<HTMLInputElement>(null);
  const messagesRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!messagesRef.current) {
      return;
    }

    messagesRef.current.scrollTop = messagesRef.current.scrollHeight;
  }, [messages, loading]);

  const mergeReceipt = useCallback((receipt: ProviderChatMessageReceipt) => {
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
      // keep silent, next refresh/retry will update receipts
    }
  }, [authSession?.token, requestId]);

  const loadMessages = useCallback(async () => {
    if (!authSession?.token || !requestId || !providerId) {
      setLoading(false);
      setError('Sessao invalida para carregar chat.');
      return;
    }

    setLoading(true);
    setError('');

    try {
      await joinProviderRealtimeConversation(authSession.token, requestId, providerId);
      const history = await fetchMobileProviderChatMessages(authSession.token, requestId);
      setMessages(sortByDate(history));
      await markSeen();
    } catch {
      setMessages([]);
      setError('Nao foi possivel carregar a conversa agora.');
    } finally {
      setLoading(false);
    }
  }, [authSession?.token, providerId, requestId, markSeen]);

  useEffect(() => {
    void loadMessages();
  }, [loadMessages]);

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
            return sortByDate(updated);
          }

          return sortByDate([...previous, message]);
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

        mergeReceipt(receipt);
      }
    });

    return () => {
      unsubscribe();
    };
  }, [authSession?.token, authSession?.userId, mergeReceipt, markSeen, providerId, requestId]);

  const handleSend = async (event: React.FormEvent) => {
    event.preventDefault();

    if (!authSession?.token || !requestId || !providerId) {
      setError('Chat indisponivel no momento.');
      return;
    }

    const normalizedText = inputText.trim();
    if (!normalizedText && selectedFiles.length === 0) {
      return;
    }

    setSending(true);
    setError('');

    try {
      const attachments = selectedFiles.length > 0
        ? await uploadMobileProviderChatAttachments(authSession.token, requestId, selectedFiles)
        : [];

      const sent = await sendMobileProviderChatMessage(authSession.token, requestId, normalizedText, attachments);
      if (sent) {
        setMessages((previous) => {
          const existing = previous.find((item) => normalizeId(item.id) === normalizeId(sent.id));
          if (existing) {
            return previous;
          }

          return sortByDate([...previous, sent]);
        });
      }

      setInputText('');
      setSelectedFiles([]);
      await markSeen();
    } catch {
      setError('Nao foi possivel enviar a mensagem agora.');
    } finally {
      setSending(false);
    }
  };

  const onSelectFiles = (event: React.ChangeEvent<HTMLInputElement>) => {
    const files = Array.from(event.target.files || []);
    if (files.length > 0) {
      setSelectedFiles((current) => [...current, ...files]);
    }

    event.target.value = '';
  };

  const removeSelectedFile = (index: number) => {
    setSelectedFiles((current) => current.filter((_, currentIndex) => currentIndex !== index));
  };

  return (
    <div className="min-h-screen bg-[#f4f7fb] pb-4 flex flex-col">
      <header className="bg-white border-b border-[#e4e7ec] sticky top-0 z-10">
        <div className="max-w-md mx-auto px-4 py-4 flex items-center justify-between gap-2">
          <button type="button" onClick={onBack} className="text-sm font-semibold text-[#344054] flex items-center gap-1">
            <span className="material-symbols-outlined text-base">arrow_back</span>
            Voltar
          </button>
          <div className="text-center min-w-0">
            <h1 className="text-sm font-bold text-[#101828] truncate">{conversation.counterpartName}</h1>
            <p className="text-[11px] text-[#667085] truncate">{conversation.title}</p>
          </div>
          <button type="button" onClick={() => void loadMessages()} className="text-sm font-semibold text-primary">Atualizar</button>
        </div>
      </header>

      {error ? (
        <div className="max-w-md mx-auto w-full px-4 pt-3">
          <div className="rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{error}</div>
        </div>
      ) : null}

      <main ref={messagesRef} className="flex-1 overflow-y-auto max-w-md mx-auto w-full px-4 py-4 space-y-3">
        {loading ? (
          <div className="rounded-xl bg-white border border-[#e4e7ec] p-4 text-sm text-[#667085]">Carregando conversa...</div>
        ) : messages.length === 0 ? (
          <div className="rounded-xl bg-white border border-[#e4e7ec] p-4 text-sm text-[#667085]">Sem mensagens ainda.</div>
        ) : (
          messages.map((message) => {
            const mine = normalizeId(message.senderId) === normalizeId(authSession?.userId);

            return (
              <div key={message.id} className={`flex ${mine ? 'justify-end' : 'justify-start'}`}>
                <div className={`max-w-[84%] rounded-2xl px-3 py-2 text-sm shadow-sm ${mine ? 'bg-primary text-white rounded-tr-none' : 'bg-white border border-[#e4e7ec] text-[#101828] rounded-tl-none'}`}>
                  {message.text ? <p className="whitespace-pre-wrap leading-relaxed">{message.text}</p> : null}

                  {message.attachments.length > 0 ? (
                    <div className="mt-2 space-y-2">
                      {message.attachments.map((attachment) => {
                        const mediaKind = String(attachment.mediaKind || '').toLowerCase();
                        const fileUrl = resolveMobileProviderChatAttachmentUrl(attachment.fileUrl);

                        if (mediaKind === 'image') {
                          return (
                            <a key={`${message.id}-${attachment.fileUrl}`} href={fileUrl} target="_blank" rel="noreferrer" className="block">
                              <img src={fileUrl} alt={attachment.fileName} className="max-h-52 w-full object-cover rounded-xl border border-white/20" />
                            </a>
                          );
                        }

                        if (mediaKind === 'video') {
                          return (
                            <video key={`${message.id}-${attachment.fileUrl}`} src={fileUrl} controls className="max-h-52 w-full rounded-xl border border-white/20 bg-black" />
                          );
                        }

                        return (
                          <a
                            key={`${message.id}-${attachment.fileUrl}`}
                            href={fileUrl}
                            target="_blank"
                            rel="noreferrer"
                            className={`inline-flex items-center gap-2 rounded-xl px-3 py-2 text-xs font-semibold ${mine ? 'bg-white/15 text-white' : 'bg-primary/5 text-primary'}`}
                          >
                            <span className="material-symbols-outlined text-base">attach_file</span>
                            <span className="truncate">{attachment.fileName}</span>
                          </a>
                        );
                      })}
                    </div>
                  ) : null}

                  <p className={`text-[10px] mt-2 ${mine ? 'text-white/70' : 'text-[#667085]'}`}>
                    {formatMessageTime(message.createdAt)}
                    {mine ? ` - ${resolveMessageStatus(message)}` : ''}
                  </p>
                </div>
              </div>
            );
          })
        )}
      </main>

      <footer className="max-w-md mx-auto w-full px-4 pb-4 pt-2 bg-white border-t border-[#e4e7ec]">
        {selectedFiles.length > 0 ? (
          <div className="mb-2 flex flex-wrap gap-2">
            {selectedFiles.map((file, index) => (
              <div key={`${file.name}-${file.size}-${index}`} className="inline-flex items-center gap-1 rounded-full border border-primary/20 bg-primary/5 px-3 py-1 text-xs text-primary">
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
            onClick={() => fileInputRef.current?.click()}
            className="p-2 text-[#667085] hover:text-primary"
          >
            <span className="material-symbols-outlined">add_circle</span>
          </button>
          <input
            ref={fileInputRef}
            type="file"
            multiple
            accept=".jpg,.jpeg,.png,.webp,.mp4,.webm,.mov"
            className="hidden"
            onChange={onSelectFiles}
          />

          <input
            type="text"
            value={inputText}
            onChange={(event) => setInputText(event.target.value)}
            placeholder="Digite sua mensagem..."
            className="flex-1 rounded-full border border-[#d0d5dd] px-4 py-2 text-sm"
            disabled={sending}
          />

          <button
            type="submit"
            disabled={(!inputText.trim() && selectedFiles.length === 0) || sending}
            className="rounded-full bg-primary text-white p-2 disabled:opacity-60"
          >
            <span className="material-symbols-outlined">send</span>
          </button>
        </form>
      </footer>
    </div>
  );
};

export default Chat;
