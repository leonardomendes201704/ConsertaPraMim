import React, { useMemo, useState } from 'react';
import type { AdminSupportTicketDetails } from '../types';

interface SupportTicketDetailsProps {
  details: AdminSupportTicketDetails | null;
  isLoading: boolean;
  isActionLoading: boolean;
  errorMessage: string;
  onBack: () => void;
  onRefresh: () => void;
  onSendMessage: (message: string, isInternal: boolean) => Promise<void>;
  onAssignToMe: () => Promise<void>;
  onUpdateStatus: (status: string) => Promise<void>;
}

const STATUS_OPTIONS = ['Open', 'InProgress', 'WaitingProvider', 'Resolved', 'Closed'];

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

function getMessageRoleStyle(authorRole: string): string {
  const normalized = String(authorRole || '').toLowerCase();
  if (normalized === 'admin') {
    return 'border-blue-200 bg-blue-50';
  }

  if (normalized === 'provider') {
    return 'border-emerald-200 bg-emerald-50';
  }

  return 'border-slate-200 bg-slate-50';
}

const SupportTicketDetails: React.FC<SupportTicketDetailsProps> = ({
  details,
  isLoading,
  isActionLoading,
  errorMessage,
  onBack,
  onRefresh,
  onSendMessage,
  onAssignToMe,
  onUpdateStatus
}) => {
  const [draftMessage, setDraftMessage] = useState('');
  const [isInternalMessage, setIsInternalMessage] = useState(false);
  const [statusToApply, setStatusToApply] = useState('InProgress');

  const canSendMessage = useMemo(() => {
    return draftMessage.trim().length > 0 && !isActionLoading;
  }, [draftMessage, isActionLoading]);

  const handleSendMessage = async () => {
    if (!canSendMessage) {
      return;
    }

    await onSendMessage(draftMessage.trim(), isInternalMessage);
    setDraftMessage('');
    setIsInternalMessage(false);
  };

  if (isLoading) {
    return (
      <div className="space-y-3">
        <div className="h-20 animate-pulse rounded-2xl bg-slate-100" />
        <div className="h-40 animate-pulse rounded-2xl bg-slate-100" />
      </div>
    );
  }

  if (errorMessage) {
    return (
      <div className="rounded-2xl border border-rose-200 bg-rose-50 p-5 text-rose-700">
        <h3 className="text-sm font-semibold">Falha ao carregar chamado</h3>
        <p className="mt-1 text-sm">{errorMessage}</p>
        <div className="mt-4 flex gap-2">
          <button type="button" onClick={onBack} className="rounded-lg border border-rose-300 px-3 py-2 text-xs font-semibold">
            Voltar
          </button>
          <button type="button" onClick={onRefresh} className="rounded-lg bg-rose-600 px-3 py-2 text-xs font-semibold text-white">
            Recarregar
          </button>
        </div>
      </div>
    );
  }

  if (!details) {
    return null;
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <button type="button" onClick={onBack} className="rounded-lg border border-slate-300 px-3 py-1.5 text-xs font-semibold text-slate-700">
          Voltar para fila
        </button>
        <button type="button" onClick={onRefresh} className="rounded-lg border border-slate-300 px-3 py-1.5 text-xs font-semibold text-slate-700">
          Atualizar
        </button>
      </div>

      <article className="rounded-2xl border border-slate-200 bg-white p-4">
        <h2 className="text-base font-semibold">#{details.ticket.id.slice(0, 8)} - {details.ticket.subject}</h2>
        <p className="mt-1 text-sm text-slate-600">{details.ticket.providerName} ({details.ticket.providerEmail})</p>

        <div className="mt-3 flex flex-wrap gap-2 text-xs">
          <span className="rounded-full bg-slate-100 px-2 py-1 text-slate-700">Status: {details.ticket.status}</span>
          <span className="rounded-full bg-slate-100 px-2 py-1 text-slate-700">Prioridade: {details.ticket.priority}</span>
          <span className="rounded-full bg-slate-100 px-2 py-1 text-slate-700">Abertura: {formatDateTime(details.ticket.openedAtUtc)}</span>
        </div>

        <div className="mt-4 grid grid-cols-1 gap-2 sm:grid-cols-2">
          <button
            type="button"
            disabled={isActionLoading}
            onClick={() => void onAssignToMe()}
            className="rounded-xl border border-slate-300 px-3 py-2 text-xs font-semibold text-slate-700 disabled:opacity-60"
          >
            Assumir para mim
          </button>

          <div className="flex gap-2">
            <select
              value={statusToApply}
              onChange={(event) => setStatusToApply(event.target.value)}
              className="flex-1 rounded-xl border border-slate-300 px-3 py-2 text-xs"
              disabled={isActionLoading}
            >
              {STATUS_OPTIONS.map((status) => (
                <option key={status} value={status}>{status}</option>
              ))}
            </select>
            <button
              type="button"
              disabled={isActionLoading}
              onClick={() => void onUpdateStatus(statusToApply)}
              className="rounded-xl bg-blue-600 px-3 py-2 text-xs font-semibold text-white disabled:opacity-60"
            >
              Atualizar
            </button>
          </div>
        </div>
      </article>

      <article className="rounded-2xl border border-slate-200 bg-white p-4">
        <h3 className="text-sm font-semibold">Responder chamado</h3>
        <textarea
          value={draftMessage}
          onChange={(event) => setDraftMessage(event.target.value)}
          rows={4}
          className="mt-2 w-full rounded-xl border border-slate-300 px-3 py-2 text-sm"
          placeholder="Digite a resposta para o prestador"
          disabled={isActionLoading}
        />

        <label className="mt-2 flex items-center gap-2 text-xs text-slate-600">
          <input
            type="checkbox"
            checked={isInternalMessage}
            onChange={(event) => setIsInternalMessage(event.target.checked)}
            disabled={isActionLoading}
          />
          Nota interna (nao visivel para o prestador)
        </label>

        <button
          type="button"
          disabled={!canSendMessage}
          onClick={() => void handleSendMessage()}
          className="mt-3 rounded-xl bg-blue-600 px-4 py-2 text-xs font-semibold text-white disabled:opacity-60"
        >
          Enviar mensagem
        </button>
      </article>

      <article className="rounded-2xl border border-slate-200 bg-white p-4">
        <h3 className="text-sm font-semibold">Historico</h3>
        <div className="mt-3 max-h-[360px] space-y-2 overflow-y-auto pr-1">
          {(details.messages || []).map((message) => (
            <div key={message.id} className={`rounded-xl border p-3 ${getMessageRoleStyle(message.authorRole)}`}>
              <div className="flex flex-wrap items-center justify-between gap-2">
                <p className="text-xs font-semibold text-slate-700">{message.authorRole} - {message.authorName}</p>
                <p className="text-[11px] text-slate-500">{formatDateTime(message.createdAtUtc)}</p>
              </div>
              <p className="mt-2 whitespace-pre-wrap text-sm text-slate-800">{message.messageText}</p>
              {message.isInternal ? <p className="mt-2 text-[11px] font-semibold text-slate-500">Mensagem interna</p> : null}
            </div>
          ))}
          {(details.messages || []).length === 0 ? (
            <p className="text-sm text-slate-500">Sem mensagens no chamado.</p>
          ) : null}
        </div>
      </article>
    </div>
  );
};

export default SupportTicketDetails;