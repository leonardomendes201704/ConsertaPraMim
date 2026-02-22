import React from 'react';
import type { AdminDashboardData } from '../types';

interface DashboardProps {
  data: AdminDashboardData | null;
  isLoading: boolean;
  errorMessage: string;
  onRefresh: () => void;
  onOpenMonitoring: () => void;
}

function formatCurrency(value: number): string {
  return new Intl.NumberFormat('pt-BR', {
    style: 'currency',
    currency: 'BRL',
    maximumFractionDigits: 0
  }).format(value || 0);
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

const Dashboard: React.FC<DashboardProps> = ({ data, isLoading, errorMessage, onRefresh, onOpenMonitoring }) => {
  if (isLoading) {
    return (
      <div className="space-y-4">
        <div className="h-24 animate-pulse rounded-2xl bg-slate-100" />
        <div className="h-24 animate-pulse rounded-2xl bg-slate-100" />
        <div className="h-24 animate-pulse rounded-2xl bg-slate-100" />
      </div>
    );
  }

  if (errorMessage) {
    return (
      <div className="rounded-2xl border border-rose-200 bg-rose-50 p-5 text-rose-700">
        <h2 className="text-base font-semibold">Falha ao carregar dashboard</h2>
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

  if (!data) {
    return (
      <div className="rounded-2xl border border-slate-200 bg-slate-50 p-5">
        <p className="text-sm text-slate-600">Sem dados para o periodo atual.</p>
      </div>
    );
  }

  const cards: Array<{ label: string; value: string }> = [
    { label: 'Usuarios', value: String(data.totalUsers || 0) },
    { label: 'Prestadores', value: String(data.totalProviders || 0) },
    { label: 'Clientes', value: String(data.totalClients || 0) },
    { label: 'Pedidos ativos', value: String(data.activeRequests || 0) },
    { label: 'Pagantes', value: String(data.payingProviders || 0) },
    { label: 'Receita mensal', value: formatCurrency(data.monthlySubscriptionRevenue || 0) }
  ];

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-base font-semibold">Visao executiva</h2>
        <button
          type="button"
          onClick={onRefresh}
          className="rounded-lg border border-slate-300 px-3 py-1.5 text-xs font-semibold text-slate-700"
        >
          Atualizar
        </button>
      </div>

      <div className="grid grid-cols-2 gap-3">
        {cards.map((card) => (
          <article key={card.label} className="rounded-2xl border border-slate-200 bg-white p-3">
            <p className="text-[11px] uppercase tracking-[0.08em] text-slate-400">{card.label}</p>
            <p className="mt-2 text-lg font-semibold text-slate-900">{card.value}</p>
          </article>
        ))}
      </div>

      <article className="rounded-2xl border border-slate-200 bg-white p-4">
        <h3 className="text-sm font-semibold text-slate-900">Interacoes recentes</h3>
        <p className="mt-1 text-xs text-slate-500">Conversas ativas 24h: {data.activeChatConversationsLast24h || 0}</p>
        <div className="mt-3 space-y-2">
          {(data.recentEvents || []).slice(0, 5).map((event) => (
            <div key={`${event.referenceId}-${event.createdAt}`} className="rounded-xl border border-slate-100 bg-slate-50 px-3 py-2">
              <p className="text-sm font-medium text-slate-800">{event.title}</p>
              <p className="text-xs text-slate-500">{formatDateTime(event.createdAt)}</p>
            </div>
          ))}
          {(data.recentEvents || []).length === 0 ? (
            <p className="text-xs text-slate-500">Sem eventos recentes para exibir.</p>
          ) : null}
        </div>
      </article>

      <button
        type="button"
        onClick={onOpenMonitoring}
        className="flex w-full items-center justify-center gap-2 rounded-2xl bg-blue-600 px-4 py-3 text-sm font-semibold text-white"
      >
        <span className="material-symbols-outlined text-[18px]">monitoring</span>
        Ir para monitoramento da API
      </button>
    </div>
  );
};

export default Dashboard;