import React from 'react';
import type {
  AdminMonitoringOverviewData,
  AdminMonitoringTopEndpoint,
  MonitoringRangePreset
} from '../types';

interface MonitoringPanelProps {
  overview: AdminMonitoringOverviewData | null;
  topEndpoints: AdminMonitoringTopEndpoint[];
  range: MonitoringRangePreset;
  isLoading: boolean;
  errorMessage: string;
  onChangeRange: (range: MonitoringRangePreset) => void;
  onRefresh: () => void;
}

const RANGE_OPTIONS: Array<{ value: MonitoringRangePreset; label: string }> = [
  { value: '1h', label: '1h' },
  { value: '24h', label: '24h' },
  { value: '7d', label: '7d' }
];

function getHealthTone(status: string): string {
  const normalized = String(status || '').toLowerCase();
  if (normalized.includes('healthy') || normalized.includes('ok')) {
    return 'bg-emerald-100 text-emerald-700';
  }

  if (normalized.includes('degraded') || normalized.includes('warning')) {
    return 'bg-amber-100 text-amber-700';
  }

  return 'bg-rose-100 text-rose-700';
}

function formatPercent(value: number): string {
  return `${(value || 0).toFixed(2)}%`;
}

const MonitoringPanel: React.FC<MonitoringPanelProps> = ({
  overview,
  topEndpoints,
  range,
  isLoading,
  errorMessage,
  onChangeRange,
  onRefresh
}) => {
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
        <h2 className="text-base font-semibold">Falha ao carregar monitoramento</h2>
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

  if (!overview) {
    return (
      <div className="rounded-2xl border border-slate-200 bg-slate-50 p-5">
        <p className="text-sm text-slate-600">Sem dados de monitoramento para o periodo.</p>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-2">
        <div className="flex items-center gap-2 rounded-xl border border-slate-200 bg-slate-50 p-1">
          {RANGE_OPTIONS.map((option) => {
            const isActive = option.value === range;
            return (
              <button
                key={option.value}
                type="button"
                onClick={() => onChangeRange(option.value)}
                className={`rounded-lg px-3 py-1.5 text-xs font-semibold transition ${
                  isActive ? 'bg-white text-blue-700 shadow-sm' : 'text-slate-500'
                }`}
              >
                {option.label}
              </button>
            );
          })}
        </div>

        <button
          type="button"
          onClick={onRefresh}
          className="rounded-lg border border-slate-300 px-3 py-1.5 text-xs font-semibold text-slate-700"
        >
          Atualizar
        </button>
      </div>

      <div className="grid grid-cols-2 gap-3">
        <article className="rounded-2xl border border-slate-200 bg-white p-3">
          <p className="text-[11px] uppercase tracking-[0.08em] text-slate-400">Requests</p>
          <p className="mt-2 text-lg font-semibold">{overview.totalRequests || 0}</p>
        </article>
        <article className="rounded-2xl border border-slate-200 bg-white p-3">
          <p className="text-[11px] uppercase tracking-[0.08em] text-slate-400">Erro</p>
          <p className="mt-2 text-lg font-semibold">{formatPercent(overview.errorRatePercent || 0)}</p>
        </article>
        <article className="rounded-2xl border border-slate-200 bg-white p-3">
          <p className="text-[11px] uppercase tracking-[0.08em] text-slate-400">P95</p>
          <p className="mt-2 text-lg font-semibold">{overview.p95LatencyMs || 0} ms</p>
        </article>
        <article className="rounded-2xl border border-slate-200 bg-white p-3">
          <p className="text-[11px] uppercase tracking-[0.08em] text-slate-400">RPM</p>
          <p className="mt-2 text-lg font-semibold">{(overview.requestsPerMinute || 0).toFixed(1)}</p>
        </article>
      </div>

      <article className="rounded-2xl border border-slate-200 bg-white p-4">
        <h3 className="text-sm font-semibold text-slate-900">Saude dos modulos</h3>
        <div className="mt-3 flex flex-wrap gap-2 text-xs font-semibold">
          <span className={`rounded-full px-2 py-1 ${getHealthTone(overview.apiHealthStatus)}`}>API: {overview.apiHealthStatus}</span>
          <span className={`rounded-full px-2 py-1 ${getHealthTone(overview.databaseHealthStatus)}`}>DB: {overview.databaseHealthStatus}</span>
          <span className={`rounded-full px-2 py-1 ${getHealthTone(overview.clientPortalHealthStatus)}`}>Portal cliente: {overview.clientPortalHealthStatus}</span>
          <span className={`rounded-full px-2 py-1 ${getHealthTone(overview.providerPortalHealthStatus)}`}>Portal prestador: {overview.providerPortalHealthStatus}</span>
        </div>
      </article>

      <article className="rounded-2xl border border-slate-200 bg-white p-4">
        <h3 className="text-sm font-semibold text-slate-900">Top endpoints</h3>
        <div className="mt-3 space-y-2">
          {topEndpoints.slice(0, 8).map((item) => (
            <div key={`${item.method}-${item.endpointTemplate}`} className="rounded-xl border border-slate-100 bg-slate-50 px-3 py-2">
              <p className="text-sm font-semibold text-slate-800">{item.method} {item.endpointTemplate}</p>
              <p className="mt-1 text-xs text-slate-600">
                Hits: {item.hits} | Erro: {formatPercent(item.errorRatePercent)} | P95: {item.p95LatencyMs}ms
              </p>
            </div>
          ))}
          {topEndpoints.length === 0 ? <p className="text-xs text-slate-500">Sem endpoints para o periodo selecionado.</p> : null}
        </div>
      </article>
    </div>
  );
};

export default MonitoringPanel;