import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  fetchMobileAdminMailboxMessageDetails,
  fetchMobileAdminMailboxMessages,
  fetchMobileAdminMailboxRecipients,
  markMobileAdminMailboxRead,
  MobileAdminError,
  sendMobileAdminMailboxEmail,
  syncMobileAdminMailbox
} from '../services/mobileAdmin';
import type {
  AdminMailboxListResponse,
  AdminMailboxMessageDetails,
  AdminMailboxRecipient
} from '../types';

interface AdminMailboxProps {
  token: string;
  onUnauthorized: () => void;
}

function toErrorMessage(error: unknown, fallback: string): string {
  if (error instanceof MobileAdminError) {
    return error.message;
  }

  if (error instanceof Error && error.message.trim()) {
    return error.message;
  }

  return fallback;
}

function isUnauthorizedError(error: unknown): boolean {
  return error instanceof MobileAdminError && error.httpStatus === 401;
}

function formatDateTime(value?: string | null): string {
  if (!value) {
    return '-';
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return '-';
  }

  return parsed.toLocaleString('pt-BR', {
    dateStyle: 'short',
    timeStyle: 'short'
  });
}

const EMPTY_MAILBOX_RESPONSE: AdminMailboxListResponse = {
  items: [],
  page: 1,
  pageSize: 20,
  totalCount: 0,
  totalPages: 0,
  lastSyncAtUtc: null,
  lastSyncStatus: null,
  lastSyncError: null
};

const AdminMailbox: React.FC<AdminMailboxProps> = ({ token, onUnauthorized }) => {
  const [folder, setFolder] = useState<'inbox' | 'sent'>('inbox');
  const [searchDraft, setSearchDraft] = useState('');
  const [searchValue, setSearchValue] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [mailbox, setMailbox] = useState<AdminMailboxListResponse>(EMPTY_MAILBOX_RESPONSE);
  const [recipients, setRecipients] = useState<AdminMailboxRecipient[]>([]);
  const [selectedMessageId, setSelectedMessageId] = useState<string | null>(null);
  const [selectedMessage, setSelectedMessage] = useState<AdminMailboxMessageDetails | null>(null);
  const [isListLoading, setIsListLoading] = useState(false);
  const [isDetailsLoading, setIsDetailsLoading] = useState(false);
  const [isActionLoading, setIsActionLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');
  const [successMessage, setSuccessMessage] = useState('');

  const [composeTo, setComposeTo] = useState('');
  const [composeSubject, setComposeSubject] = useState('');
  const [composeBody, setComposeBody] = useState('');

  const refreshMailboxList = useCallback(async (options?: { silent?: boolean }) => {
    const shouldShowLoading = !options?.silent;
    if (shouldShowLoading) {
      setIsListLoading(true);
    }

    setErrorMessage('');
    try {
      const payload = await fetchMobileAdminMailboxMessages(token, {
        folder,
        search: searchValue,
        page,
        pageSize
      });
      setMailbox(payload);

      setSelectedMessageId((currentSelected) => {
        if (!payload.items.length) {
          return null;
        }

        if (currentSelected && payload.items.some((item) => item.id === currentSelected)) {
          return currentSelected;
        }

        return payload.items[0].id;
      });
    } catch (error) {
      if (isUnauthorizedError(error)) {
        onUnauthorized();
        return;
      }

      setErrorMessage(toErrorMessage(error, 'Nao foi possivel carregar o inbox.'));
    } finally {
      if (shouldShowLoading) {
        setIsListLoading(false);
      }
    }
  }, [folder, onUnauthorized, page, pageSize, searchValue, token]);

  const refreshRecipients = useCallback(async () => {
    try {
      const payload = await fetchMobileAdminMailboxRecipients(token, 120);
      setRecipients(payload.filter((item) => item.isActive));
    } catch (error) {
      if (isUnauthorizedError(error)) {
        onUnauthorized();
      }
    }
  }, [onUnauthorized, token]);

  useEffect(() => {
    void refreshMailboxList();
  }, [refreshMailboxList]);

  useEffect(() => {
    void refreshRecipients();
  }, [refreshRecipients]);

  useEffect(() => {
    if (!selectedMessageId) {
      setSelectedMessage(null);
      return;
    }

    let isDisposed = false;
    setIsDetailsLoading(true);
    setErrorMessage('');

    void fetchMobileAdminMailboxMessageDetails(token, selectedMessageId)
      .then((payload) => {
        if (isDisposed) {
          return;
        }

        setSelectedMessage(payload);
      })
      .catch((error: unknown) => {
        if (isDisposed) {
          return;
        }

        if (isUnauthorizedError(error)) {
          onUnauthorized();
          return;
        }

        setErrorMessage(toErrorMessage(error, 'Nao foi possivel carregar a mensagem selecionada.'));
      })
      .finally(() => {
        if (!isDisposed) {
          setIsDetailsLoading(false);
        }
      });

    return () => {
      isDisposed = true;
    };
  }, [onUnauthorized, selectedMessageId, token]);

  useEffect(() => {
    const intervalId = window.setInterval(() => {
      void refreshMailboxList({ silent: true });
    }, 20000);

    const handleFocus = () => {
      void refreshMailboxList({ silent: true });
    };

    window.addEventListener('focus', handleFocus);

    return () => {
      window.clearInterval(intervalId);
      window.removeEventListener('focus', handleFocus);
    };
  }, [refreshMailboxList]);

  const recipientOptions = useMemo(() => {
    return recipients
      .map((recipient) => ({
        value: recipient.email,
        label: `${recipient.name} (${recipient.role})`
      }))
      .sort((left, right) => left.label.localeCompare(right.label, 'pt-BR'));
  }, [recipients]);

  const handleSearchSubmit = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setPage(1);
    setSearchValue(searchDraft.trim());
  };

  const handleSync = async () => {
    setIsActionLoading(true);
    setErrorMessage('');
    setSuccessMessage('');
    try {
      const result = await syncMobileAdminMailbox(token);
      if (!result.success) {
        setErrorMessage(result.errorMessage || 'Falha ao sincronizar inbox.');
        return;
      }

      setSuccessMessage(`Sincronizado com sucesso. Novos emails: ${result.newMessagesCount}.`);
      await refreshMailboxList({ silent: true });
    } catch (error) {
      if (isUnauthorizedError(error)) {
        onUnauthorized();
        return;
      }

      setErrorMessage(toErrorMessage(error, 'Falha ao sincronizar inbox.'));
    } finally {
      setIsActionLoading(false);
    }
  };

  const handleToggleRead = async () => {
    if (!selectedMessage) {
      return;
    }

    setIsActionLoading(true);
    setErrorMessage('');
    setSuccessMessage('');
    try {
      const updated = await markMobileAdminMailboxRead(token, selectedMessage.id, !selectedMessage.isRead);
      setSelectedMessage(updated);
      setSuccessMessage(updated.isRead ? 'Mensagem marcada como lida.' : 'Mensagem marcada como nao lida.');
      await refreshMailboxList({ silent: true });
    } catch (error) {
      if (isUnauthorizedError(error)) {
        onUnauthorized();
        return;
      }

      setErrorMessage(toErrorMessage(error, 'Falha ao atualizar status de leitura.'));
    } finally {
      setIsActionLoading(false);
    }
  };

  const handleSendEmail = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!composeTo.trim() || !composeSubject.trim() || !composeBody.trim()) {
      setErrorMessage('Preencha destinatario, assunto e mensagem.');
      return;
    }

    setIsActionLoading(true);
    setErrorMessage('');
    setSuccessMessage('');
    try {
      await sendMobileAdminMailboxEmail(token, {
        to: composeTo.trim(),
        subject: composeSubject.trim(),
        body: composeBody
      });

      setComposeSubject('');
      setComposeBody('');
      setSuccessMessage('Email enviado com sucesso.');
      setFolder('sent');
      setPage(1);
      setSearchValue('');
      setSearchDraft('');
    } catch (error) {
      if (isUnauthorizedError(error)) {
        onUnauthorized();
        return;
      }

      setErrorMessage(toErrorMessage(error, 'Falha ao enviar email.'));
    } finally {
      setIsActionLoading(false);
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-2">
        <div>
          <h2 className="text-base font-semibold">Webmail</h2>
          <p className="text-xs text-slate-500">
            Last sync: {formatDateTime(mailbox.lastSyncAtUtc)} ({mailbox.lastSyncStatus || 'n/a'})
          </p>
        </div>
        <button
          type="button"
          onClick={handleSync}
          disabled={isActionLoading}
          className="rounded-lg border border-slate-300 px-3 py-1.5 text-xs font-semibold text-slate-700 disabled:cursor-not-allowed disabled:opacity-60"
        >
          Sincronizar
        </button>
      </div>

      {errorMessage ? (
        <div className="rounded-2xl border border-rose-200 bg-rose-50 p-3 text-sm text-rose-700">{errorMessage}</div>
      ) : null}
      {successMessage ? (
        <div className="rounded-2xl border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-700">{successMessage}</div>
      ) : null}

      <div className="flex gap-2">
        <button
          type="button"
          onClick={() => {
            setFolder('inbox');
            setPage(1);
          }}
          className={`rounded-full px-3 py-1.5 text-xs font-semibold ${folder === 'inbox' ? 'bg-blue-600 text-white' : 'bg-slate-100 text-slate-700'}`}
        >
          Inbox
        </button>
        <button
          type="button"
          onClick={() => {
            setFolder('sent');
            setPage(1);
          }}
          className={`rounded-full px-3 py-1.5 text-xs font-semibold ${folder === 'sent' ? 'bg-blue-600 text-white' : 'bg-slate-100 text-slate-700'}`}
        >
          Enviados
        </button>
      </div>

      <form onSubmit={handleSearchSubmit} className="flex gap-2">
        <input
          value={searchDraft}
          onChange={(event) => setSearchDraft(event.target.value)}
          className="w-full rounded-xl border border-slate-300 px-3 py-2 text-sm"
          placeholder="Buscar assunto, remetente ou preview..."
        />
        <button
          type="submit"
          className="rounded-xl border border-slate-300 px-3 py-2 text-xs font-semibold text-slate-700"
        >
          Buscar
        </button>
      </form>

      <section className="space-y-2">
        {isListLoading ? (
          <>
            <div className="h-20 animate-pulse rounded-2xl bg-slate-100" />
            <div className="h-20 animate-pulse rounded-2xl bg-slate-100" />
          </>
        ) : null}

        {!isListLoading && mailbox.items.length === 0 ? (
          <div className="rounded-2xl border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
            Nenhuma mensagem encontrada.
          </div>
        ) : null}

        {!isListLoading && mailbox.items.length > 0 ? (
          <div className="space-y-2">
            {mailbox.items.map((message) => (
              <button
                key={message.id}
                type="button"
                onClick={() => setSelectedMessageId(message.id)}
                className={`w-full rounded-2xl border p-3 text-left transition ${
                  selectedMessageId === message.id
                    ? 'border-blue-200 bg-blue-50'
                    : message.isRead
                      ? 'border-slate-200 bg-white hover:bg-slate-50'
                      : 'border-amber-200 bg-amber-50/40 hover:bg-amber-50/70'
                }`}
              >
                <div className="flex items-start justify-between gap-2">
                  <div>
                    <p className="text-sm font-semibold text-slate-900">{message.subject}</p>
                    <p className="mt-1 text-xs text-slate-500">{message.fromAddress} {'->'} {message.toAddress}</p>
                  </div>
                  <span className="text-[11px] text-slate-500">{formatDateTime(message.occurredAtUtc)}</span>
                </div>
                <p className="mt-1 line-clamp-2 text-xs text-slate-600">{message.preview}</p>
              </button>
            ))}

            {mailbox.totalPages > 1 ? (
              <div className="flex items-center justify-between pt-1 text-xs">
                <button
                  type="button"
                  disabled={mailbox.page <= 1}
                  onClick={() => setPage((value) => Math.max(1, value - 1))}
                  className="rounded-lg border border-slate-300 px-3 py-1.5 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  Anterior
                </button>
                <span className="text-slate-500">{mailbox.page} / {Math.max(mailbox.totalPages, 1)}</span>
                <button
                  type="button"
                  disabled={mailbox.page >= mailbox.totalPages}
                  onClick={() => setPage((value) => Math.min(mailbox.totalPages, value + 1))}
                  className="rounded-lg border border-slate-300 px-3 py-1.5 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  Proxima
                </button>
              </div>
            ) : null}
          </div>
        ) : null}
      </section>

      <section className="space-y-3 rounded-2xl border border-slate-200 bg-slate-50 p-4">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-semibold">Detalhes</h3>
          {selectedMessage ? (
            <button
              type="button"
              onClick={handleToggleRead}
              disabled={isActionLoading || isDetailsLoading}
              className="rounded-lg border border-slate-300 px-3 py-1.5 text-xs font-semibold text-slate-700 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {selectedMessage.isRead ? 'Marcar nao lida' : 'Marcar lida'}
            </button>
          ) : null}
        </div>

        {isDetailsLoading ? (
          <div className="h-32 animate-pulse rounded-2xl bg-white" />
        ) : null}

        {!isDetailsLoading && selectedMessage ? (
          <div className="space-y-2 rounded-2xl border border-slate-200 bg-white p-3">
            <p className="text-sm font-semibold text-slate-900">{selectedMessage.subject}</p>
            <p className="text-xs text-slate-500">De: {selectedMessage.fromAddress}</p>
            <p className="text-xs text-slate-500">Para: {selectedMessage.toAddress}</p>
            <p className="text-xs text-slate-500">Data: {formatDateTime(selectedMessage.occurredAtUtc)}</p>
            <div className="rounded-xl border border-slate-200 bg-slate-50 p-2">
              <pre className="whitespace-pre-wrap text-xs text-slate-700">{selectedMessage.bodyText}</pre>
            </div>
          </div>
        ) : null}

        {!isDetailsLoading && !selectedMessage ? (
          <div className="rounded-2xl border border-slate-200 bg-white p-3 text-sm text-slate-600">
            Selecione uma mensagem para visualizar o conteudo.
          </div>
        ) : null}
      </section>

      <section className="space-y-3 rounded-2xl border border-slate-200 bg-white p-4">
        <h3 className="text-sm font-semibold">Enviar email</h3>
        <form onSubmit={handleSendEmail} className="space-y-3">
          <div>
            <label className="mb-1 block text-xs font-semibold text-slate-600">Para</label>
            <input
              type="email"
              value={composeTo}
              onChange={(event) => setComposeTo(event.target.value)}
              className="w-full rounded-xl border border-slate-300 px-3 py-2 text-sm"
              list="mobile-admin-mailbox-recipients"
              placeholder="destinatario@exemplo.com"
              required
            />
            <datalist id="mobile-admin-mailbox-recipients">
              {recipientOptions.map((item) => (
                <option key={`${item.value}-${item.label}`} value={item.value}>{item.label}</option>
              ))}
            </datalist>
          </div>
          <div>
            <label className="mb-1 block text-xs font-semibold text-slate-600">Assunto</label>
            <input
              type="text"
              value={composeSubject}
              onChange={(event) => setComposeSubject(event.target.value)}
              className="w-full rounded-xl border border-slate-300 px-3 py-2 text-sm"
              required
            />
          </div>
          <div>
            <label className="mb-1 block text-xs font-semibold text-slate-600">Mensagem</label>
            <textarea
              value={composeBody}
              onChange={(event) => setComposeBody(event.target.value)}
              className="min-h-32 w-full rounded-xl border border-slate-300 px-3 py-2 text-sm"
              required
            />
          </div>
          <button
            type="submit"
            disabled={isActionLoading}
            className="w-full rounded-xl bg-blue-600 px-4 py-2 text-sm font-semibold text-white disabled:cursor-not-allowed disabled:opacity-60"
          >
            Enviar
          </button>
        </form>
      </section>
    </div>
  );
};

export default AdminMailbox;
