import React, { useMemo, useState } from 'react';
import type { AdminPushStoredEvent, AdminRecentEvent } from '../types';

interface RecentEventsProps {
  items: AdminRecentEvent[];
  pushItems: AdminPushStoredEvent[];
  isLoading: boolean;
  errorMessage: string;
  onRefresh: () => void;
}

type UnifiedEventSource = 'operational' | 'push';

interface UnifiedEventItem {
  id: string;
  source: UnifiedEventSource;
  type: string;
  title: string;
  description?: string;
  createdAtIso: string;
  referenceId?: string;
  pushPayload?: AdminPushStoredEvent;
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
    case 'provider_support_ticket_opened':
      return 'Prestador abriu chamado';
    case 'admin_summary':
      return 'Resumo de entrega';
    case 'chat':
      return 'Chat';
    default:
      return 'Evento';
  }
}

function resolveTypeIcon(rawType?: string, source: UnifiedEventSource = 'operational'): string {
  if (source === 'push') {
    return 'notifications_active';
  }

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
    case 'provider_support_ticket_opened':
      return 'support_agent';
    case 'chat':
      return 'chat';
    default:
      return 'notifications';
  }
}

function buildUnifiedEvents(items: AdminRecentEvent[], pushItems: AdminPushStoredEvent[]): UnifiedEventItem[] {
  const operational = items.map<UnifiedEventItem>((event) => ({
    id: `op-${event.referenceId || 'na'}-${event.createdAt}-${event.type || 'event'}`,
    source: 'operational',
    type: String(event.type || ''),
    title: String(event.title || 'Evento operacional'),
    description: event.description || undefined,
    createdAtIso: event.createdAt,
    referenceId: event.referenceId || undefined
  }));

  const push = pushItems.map<UnifiedEventItem>((event) => ({
    id: `push-${event.id}`,
    source: 'push',
    type: event.notificationType || 'push_notification',
    title: event.title,
    description: event.body,
    createdAtIso: event.createdAtIso,
    pushPayload: event
  }));

  return [...push, ...operational].sort((left, right) => {
    return new Date(right.createdAtIso).getTime() - new Date(left.createdAtIso).getTime();
  });
}

function openExternalUrl(url: string | undefined): void {
  if (!url) {
    return;
  }

  window.open(url, '_blank', 'noopener,noreferrer');
}

const RecentEvents: React.FC<RecentEventsProps> = ({ items, pushItems, isLoading, errorMessage, onRefresh }) => {
  const [selectedEventId, setSelectedEventId] = useState<string | null>(null);

  const unifiedEvents = useMemo(() => {
    return buildUnifiedEvents(items, pushItems);
  }, [items, pushItems]);

  const selectedEvent = useMemo(() => {
    return unifiedEvents.find((event) => event.id === selectedEventId) || null;
  }, [selectedEventId, unifiedEvents]);

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

  if (selectedEvent) {
    return (
      <div className="space-y-4">
        <div className="flex items-center justify-between gap-2">
          <button
            type="button"
            onClick={() => setSelectedEventId(null)}
            className="inline-flex items-center gap-1 rounded-lg border border-slate-300 px-3 py-1.5 text-xs font-semibold text-slate-700"
          >
            <span className="material-symbols-outlined text-base">arrow_back</span>
            Voltar
          </button>
          <button
            type="button"
            onClick={onRefresh}
            className="rounded-lg border border-slate-300 px-3 py-1.5 text-xs font-semibold text-slate-700"
          >
            Atualizar
          </button>
        </div>

        <article className="space-y-4 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
          <div className="flex items-start gap-3">
            <span className="material-symbols-outlined text-blue-600">
              {resolveTypeIcon(selectedEvent.type, selectedEvent.source)}
            </span>
            <div className="min-w-0 flex-1">
              <p className="text-sm font-semibold text-slate-900">{selectedEvent.title}</p>
              <div className="mt-2 flex flex-wrap items-center gap-2">
                <span className="rounded-full bg-blue-50 px-2 py-0.5 text-[11px] font-semibold text-blue-700">
                  {formatTypeLabel(selectedEvent.type)}
                </span>
                <span className="rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-semibold text-slate-600">
                  {selectedEvent.source === 'push' ? 'Push notification' : 'Evento operacional'}
                </span>
              </div>
            </div>
          </div>

          <div className="space-y-2 text-sm text-slate-700">
            <p><span className="font-semibold text-slate-900">Data:</span> {formatDateTime(selectedEvent.createdAtIso)}</p>
            {selectedEvent.referenceId ? (
              <p><span className="font-semibold text-slate-900">Referencia:</span> {selectedEvent.referenceId}</p>
            ) : null}
            {selectedEvent.description ? (
              <p><span className="font-semibold text-slate-900">Detalhe:</span> {selectedEvent.description}</p>
            ) : null}
          </div>

          {selectedEvent.source === 'push' && selectedEvent.pushPayload ? (
            <section className="space-y-3 rounded-xl border border-slate-200 bg-slate-50 p-3">
              <h3 className="text-sm font-semibold text-slate-800">Detalhes da push</h3>
              <p className="text-xs text-slate-600">
                Origem: {selectedEvent.pushPayload.origin === 'action' ? 'Abertura da notificacao' : 'Recebida em foreground'}
              </p>
              {selectedEvent.pushPayload.actionUrl ? (
                <button
                  type="button"
                  onClick={() => openExternalUrl(selectedEvent.pushPayload?.actionUrl)}
                  className="inline-flex items-center gap-2 rounded-lg border border-blue-300 bg-blue-50 px-3 py-2 text-xs font-semibold text-blue-700"
                >
                  <span className="material-symbols-outlined text-base">open_in_new</span>
                  Abrir acao da notificacao
                </button>
              ) : null}
              <div>
                <p className="mb-1 text-xs font-semibold uppercase tracking-[0.08em] text-slate-500">Payload JSON</p>
                <pre className="max-h-64 overflow-auto rounded-lg bg-slate-900 p-3 text-[11px] text-slate-100">
                  {JSON.stringify(selectedEvent.pushPayload.rawData || {}, null, 2)}
                </pre>
              </div>
            </section>
          ) : null}
        </article>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-base font-semibold">Eventos Recentes</h2>
          <p className="text-xs text-slate-500">
            {unifiedEvents.length} evento(s) no feed ({pushItems.length} push local)
          </p>
        </div>
        <button
          type="button"
          onClick={onRefresh}
          className="rounded-lg border border-slate-300 px-3 py-1.5 text-xs font-semibold text-slate-700"
        >
          Atualizar
        </button>
      </div>

      {unifiedEvents.length === 0 ? (
        <div className="rounded-2xl border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
          Nenhum evento operacional ou push recente para exibir.
        </div>
      ) : (
        <div className="space-y-3">
          {unifiedEvents.map((event) => (
            <button
              key={event.id}
              type="button"
              onClick={() => setSelectedEventId(event.id)}
              className="w-full rounded-2xl border border-slate-200 bg-white p-3 text-left shadow-sm transition hover:border-blue-200 hover:bg-blue-50/30"
            >
              <div className="flex items-start gap-3">
                <span className="material-symbols-outlined text-blue-600">
                  {resolveTypeIcon(event.type, event.source)}
                </span>
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="text-sm font-semibold text-slate-900">{event.title}</p>
                    <span className="rounded-full bg-blue-50 px-2 py-0.5 text-[11px] font-semibold text-blue-700">
                      {formatTypeLabel(event.type)}
                    </span>
                    {event.source === 'push' ? (
                      <span className="rounded-full bg-amber-50 px-2 py-0.5 text-[11px] font-semibold text-amber-700">Push</span>
                    ) : null}
                  </div>
                  {event.description ? (
                    <p className="mt-1 line-clamp-2 text-xs text-slate-600">{event.description}</p>
                  ) : null}
                  <p className="mt-1 text-[11px] text-slate-400">{formatDateTime(event.createdAtIso)}</p>
                </div>
                <span className="material-symbols-outlined text-slate-400">chevron_right</span>
              </div>
            </button>
          ))}
        </div>
      )}
    </div>
  );
};

export default RecentEvents;
