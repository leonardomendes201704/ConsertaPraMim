import React from 'react';
import type { AdminSupportTicketQueueIndicators, AdminSupportTicketSummary } from '../types';

interface SupportTicketsProps {
  items: AdminSupportTicketSummary[];
  indicators: AdminSupportTicketQueueIndicators | null;
  statusFilter: string;
  isLoading: boolean;
  errorMessage: string;
  onStatusFilterChange: (status: string) => void;
  onRefresh: () => void;
  onOpenTicket: (ticketId: string) => void;
}

const STATUS_OPTIONS: Array<{ value: string; label: string }> = [
  { value: 'all', label: 'Todos' },
  { value: 'Open', label: 'Abertos' },
  { value: 'InProgress', label: 'Em andamento' },
  { value: 'WaitingProvider', label: 'Aguardando prestador' },
  { value: 'Resolved', label: 'Resolvidos' },
  { value: 'Closed', label: 'Fechados' }
];

function formatDateTime(value: string): string {
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return '-';
  }

  return parsed.toLocaleString('pt-BR', {
    dateStyle: 'short',
    timeStyle: 'short'
  });
}

function getStatusBadgeClass(status: string): string {
  const normalized = String(status || '').toLowerCase();
  if (normalized === 'open') {
    return 'bg-blue-100 text-blue-700';
  }

  if (normalized === 'inprogress') {
    return 'bg-amber-100 text-amber-700';
  }

  if (normalized === 'waitingprovider') {
    return 'bg-cyan-100 text-cyan-700';
  }

  if (normalized === 'resolved') {
    return 'bg-emerald-100 text-emerald-700';
  }

  if (normalized === 'closed') {
    return 'bg-slate-200 text-slate-700';
  }

  return 'bg-slate-100 text-slate-600';
}

function getPriorityBadgeClass(priority: string): string {
  const normalized = String(priority || '').toLowerCase();
  if (normalized === 'critical') {
    return 'bg-rose-100 text-rose-700';
  }

  if (normalized === 'high') {
    return 'bg-orange-100 text-orange-700';
  }

  if (normalized === 'medium') {
    return 'bg-blue-100 text-blue-700';
  }

  return 'bg-emerald-100 text-emerald-700';
}

const SupportTickets: React.FC<SupportTicketsProps> = ({
  items,
  indicators,
  statusFilter,
  isLoading,
  errorMessage,
  onStatusFilterChange,
  onRefresh,
  onOpenTicket
}) => {
  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-base font-semibold">Fila de chamados</h2>
        <button
          type="button"
          onClick={onRefresh}
          className="rounded-lg border border-slate-300 px-3 py-1.5 text-xs font-semibold text-slate-700"
        >
          Atualizar
        </button>
      </div>

      <div className="flex gap-2 overflow-x-auto pb-1">
        {STATUS_OPTIONS.map((option) => {
          const isActive = statusFilter.toLowerCase() === option.value.toLowerCase();
          return (
            <button
              key={option.value}
              type="button"
              onClick={() => onStatusFilterChange(option.value)}
              className={`whitespace-nowrap rounded-full px-3 py-1.5 text-xs font-semibold transition ${
                isActive ? 'bg-blue-600 text-white' : 'bg-slate-100 text-slate-600'
              }`}
            >
              {option.label}
            </button>
          );
        })}
      </div>

      {indicators ? (
        <div className="grid grid-cols-2 gap-2">
          <article className="rounded-xl border border-slate-200 bg-white p-3">
            <p className="text-[11px] uppercase tracking-[0.08em] text-slate-400">Abertos</p>
            <p className="mt-1 text-base font-semibold">{indicators.openCount}</p>
          </article>
          <article className="rounded-xl border border-slate-200 bg-white p-3">
            <p className="text-[11px] uppercase tracking-[0.08em] text-slate-400">Sem 1a resp.</p>
            <p className="mt-1 text-base font-semibold">{indicators.withoutFirstAdminResponseCount}</p>
          </article>
          <article className="rounded-xl border border-slate-200 bg-white p-3">
            <p className="text-[11px] uppercase tracking-[0.08em] text-slate-400">SLA vencido</p>
            <p className="mt-1 text-base font-semibold text-rose-700">{indicators.overdueWithoutFirstResponseCount}</p>
          </article>
          <article className="rounded-xl border border-slate-200 bg-white p-3">
            <p className="text-[11px] uppercase tracking-[0.08em] text-slate-400">Sem dono</p>
            <p className="mt-1 text-base font-semibold">{indicators.unassignedCount}</p>
          </article>
        </div>
      ) : null}

      {isLoading ? (
        <div className="space-y-2">
          <div className="h-24 animate-pulse rounded-2xl bg-slate-100" />
          <div className="h-24 animate-pulse rounded-2xl bg-slate-100" />
          <div className="h-24 animate-pulse rounded-2xl bg-slate-100" />
        </div>
      ) : null}

      {!isLoading && errorMessage ? (
        <div className="rounded-2xl border border-rose-200 bg-rose-50 p-5 text-rose-700">
          <h3 className="text-sm font-semibold">Falha ao carregar chamados</h3>
          <p className="mt-1 text-sm">{errorMessage}</p>
        </div>
      ) : null}

      {!isLoading && !errorMessage ? (
        <div className="space-y-2">
          {items.map((item) => (
            <button
              key={item.id}
              type="button"
              onClick={() => onOpenTicket(item.id)}
              className={`w-full rounded-2xl border p-4 text-left transition ${
                item.isOverdueFirstResponse
                  ? 'border-rose-200 bg-rose-50/40 hover:bg-rose-50'
                  : 'border-slate-200 bg-white hover:bg-slate-50'
              }`}
            >
              <div className="flex items-start justify-between gap-3">
                <div>
                  <p className="text-sm font-semibold text-slate-900">#{item.id.slice(0, 8)} - {item.subject}</p>
                  <p className="mt-1 text-xs text-slate-500">{item.providerName} ({item.providerEmail})</p>
                </div>
                <span className={`rounded-full px-2 py-1 text-[11px] font-semibold ${getStatusBadgeClass(item.status)}`}>
                  {item.status}
                </span>
              </div>

              <div className="mt-3 flex flex-wrap items-center gap-2 text-[11px]">
                <span className={`rounded-full px-2 py-1 font-semibold ${getPriorityBadgeClass(item.priority)}`}>{item.priority}</span>
                <span className="rounded-full bg-slate-100 px-2 py-1 text-slate-600">Msgs: {item.messageCount}</span>
                <span className="rounded-full bg-slate-100 px-2 py-1 text-slate-600">Atualizado: {formatDateTime(item.lastInteractionAtUtc)}</span>
              </div>

              {item.lastMessagePreview ? (
                <p className="mt-3 line-clamp-2 text-xs text-slate-600">{item.lastMessagePreview}</p>
              ) : null}
            </button>
          ))}

          {items.length === 0 ? (
            <div className="rounded-2xl border border-slate-200 bg-slate-50 p-5 text-sm text-slate-600">
              Nenhum chamado encontrado para o filtro selecionado.
            </div>
          ) : null}
        </div>
      ) : null}
    </div>
  );
};

export default SupportTickets;