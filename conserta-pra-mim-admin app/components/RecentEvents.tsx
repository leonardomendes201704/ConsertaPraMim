import React from 'react';
import type { AdminRecentEvent } from '../types';

interface RecentEventsProps {
  items: AdminRecentEvent[];
  isLoading: boolean;
  errorMessage: string;
  onRefresh: () => void;
}

function formatDateTime(value?: string): string {
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

function formatTypeLabel(rawType?: string): string {
  const type = String(rawType || '').trim().toLowerCase();
  switch (type) {
    case 'client_request_opened':
      return 'Cliente abriu pedido';
    case 'provider_proposal_sent':
      return 'Prestador enviou proposta';
    case 'client_registered':
      return 'Cliente novo';
    case 'client_login':
      return 'Cliente login';
    case 'provider_registered':
      return 'Prestador novo';
    case 'provider_login':
      return 'Prestador login';
    case 'client_accepted_proposal':
      return 'Cliente aceitou proposta';
    case 'client_scheduled':
      return 'Cliente agendou';
    case 'chat':
      return 'Chat';
    default:
      return 'Evento';
  }
}

function resolveTypeIcon(rawType?: string): string {
  const type = String(rawType || '').trim().toLowerCase();
  switch (type) {
    case 'client_request_opened':
      return 'add_task';
    case 'provider_proposal_sent':
      return 'request_quote';
    case 'client_registered':
    case 'provider_registered':
      return 'person_add';
    case 'client_login':
    case 'provider_login':
      return 'login';
    case 'client_accepted_proposal':
      return 'check_circle';
    case 'client_scheduled':
      return 'event_available';
    case 'chat':
      return 'chat';
    default:
      return 'notifications';
  }
}

const RecentEvents: React.FC<RecentEventsProps> = ({ items, isLoading, errorMessage, onRefresh }) => {
  if (isLoading) {
    return (
      <div className="space-y-3">
        <div className="h-24 animate-pulse rounded-2xl bg-slate-100" />
        <div className="h-24 animate-pulse rounded-2xl bg-slate-100" />
        <div className="h-24 animate-pulse rounded-2xl bg-slate-100" />
      </div>
    );
  }

  if (errorMessage) {
    return (
      <div className="rounded-2xl border border-rose-200 bg-rose-50 p-5 text-rose-700">
        <h2 className="text-base font-semibold">Falha ao carregar eventos</h2>
        <p className="mt-2 text-sm">{errorMessage}</p>
        <button
          type="button"
          onClick={onRefresh}
          className="mt-4 rounded-xl bg-rose-600 px-4 py-2 text-sm font-semibold text-white"
        >
          Tentar novamente
        </button>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-base font-semibold">Eventos Recentes</h2>
          <p className="text-xs text-slate-500">{items.length} evento(s) no recorte atual</p>
        </div>
        <button
          type="button"
          onClick={onRefresh}
          className="rounded-lg border border-slate-300 px-3 py-1.5 text-xs font-semibold text-slate-700"
        >
          Atualizar
        </button>
      </div>

      {items.length === 0 ? (
        <div className="rounded-2xl border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
          Nenhum evento operacional recente para exibir.
        </div>
      ) : (
        <div className="space-y-3">
          {items.map((event) => (
            <article
              key={`${event.referenceId}-${event.createdAt}`}
              className="rounded-2xl border border-slate-200 bg-white p-3 shadow-sm"
            >
              <div className="flex items-start gap-3">
                <span className="material-symbols-outlined text-blue-600">{resolveTypeIcon(event.type)}</span>
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="text-sm font-semibold text-slate-900">{event.title}</p>
                    <span className="rounded-full bg-blue-50 px-2 py-0.5 text-[11px] font-semibold text-blue-700">
                      {formatTypeLabel(event.type)}
                    </span>
                  </div>
                  {event.description ? (
                    <p className="mt-1 text-xs text-slate-600">{event.description}</p>
                  ) : null}
                  <p className="mt-1 text-[11px] text-slate-400">{formatDateTime(event.createdAt)}</p>
                </div>
              </div>
            </article>
          ))}
        </div>
      )}
    </div>
  );
};

export default RecentEvents;
