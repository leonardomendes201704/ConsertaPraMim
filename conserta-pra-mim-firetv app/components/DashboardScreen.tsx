import React, { useCallback, useEffect, useState } from 'react';
import { FireTvDashboardApiError, fetchFireTvLandingDashboard } from '../services/dashboard';
import type {
  FireTvAuthSession,
  FireTvBreakdownItem,
  FireTvDashboardFilterOption,
  FireTvElementRankingItem,
  FireTvLandingDashboardData,
  FireTvRecentSession,
  FireTvScrollmapBucket
} from '../types';
import HeatmapGrid from './HeatmapGrid';

interface DashboardScreenProps {
  session: FireTvAuthSession;
  onBack: () => void;
  onLogout: () => void;
}

interface DashboardFilters {
  rangeDays?: number;
  origin?: string;
  comparisonMode?: string;
}

function formatGeneratedAt(value: string): string {
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return 'agora';
  }

  return parsed.toLocaleString('pt-BR', {
    day: '2-digit',
    month: '2-digit',
    hour: '2-digit',
    minute: '2-digit'
  });
}

function formatDate(value?: string | null): string {
  if (!value) {
    return '--';
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return '--';
  }

  return parsed.toLocaleDateString('pt-BR', {
    day: '2-digit',
    month: '2-digit'
  });
}

function formatDuration(seconds: number): string {
  if (seconds < 60) {
    return `${seconds}s`;
  }

  const minutes = Math.floor(seconds / 60);
  const remainingSeconds = seconds % 60;
  return remainingSeconds > 0 ? `${minutes}m ${remainingSeconds}s` : `${minutes}m`;
}

function resolveOriginLabel(value: string): string {
  switch ((value || '').toLowerCase()) {
    case 'client':
      return 'Cliente';
    case 'provider':
      return 'Prestador';
    default:
      return 'Todas as origens';
  }
}

const FilterGroup: React.FC<{
  title: string;
  options: FireTvDashboardFilterOption[];
  selectedValue: string;
  onSelect: (value: string) => void;
}> = ({ title, options, selectedValue, onSelect }) => (
  <div className="tv-filter-group">
    <p className="tv-eyebrow">{title}</p>
    <div className="tv-filter-chip-list">
      {options.map((option) => (
        <button
          key={`${title}-${option.value}`}
          type="button"
          className={`tv-chip-button ${selectedValue === option.value ? 'is-active' : ''}`}
          onClick={() => onSelect(option.value)}
        >
          {option.label}
        </button>
      ))}
    </div>
  </div>
);

const RangeFilterGroup: React.FC<{
  selectedValue: number;
  options: number[];
  onSelect: (value: number) => void;
}> = ({ selectedValue, options, onSelect }) => (
  <div className="tv-filter-group">
    <p className="tv-eyebrow">Janela</p>
    <div className="tv-filter-chip-list">
      {options.map((option) => (
        <button
          key={`range-${option}`}
          type="button"
          className={`tv-chip-button ${selectedValue === option ? 'is-active' : ''}`}
          onClick={() => onSelect(option)}
        >
          {option} dia(s)
        </button>
      ))}
    </div>
  </div>
);

const MetricList: React.FC<{ title: string; items: FireTvBreakdownItem[]; tone?: 'default' | 'soft' }> = ({ title, items, tone = 'default' }) => (
  <section className={`tv-panel ${tone === 'soft' ? 'tv-panel--soft' : ''}`}>
    <div className="tv-panel-header">
      <h2>{title}</h2>
    </div>
    {items.length === 0 ? (
      <p className="tv-empty-state">Sem dados suficientes no periodo.</p>
    ) : (
      <ul className="tv-ranked-list">
        {items.map((item) => (
          <li key={`${title}-${item.label}`}>
            <span>{item.label}</span>
            <strong>{item.count}</strong>
          </li>
        ))}
      </ul>
    )}
  </section>
);

const ScrollmapPanel: React.FC<{ items: FireTvScrollmapBucket[] }> = ({ items }) => (
  <section className="tv-panel tv-panel--soft">
    <div className="tv-panel-header">
      <h2>Scrollmap</h2>
    </div>
    {items.length === 0 ? (
      <p className="tv-empty-state">Scrollmap indisponivel para o filtro atual.</p>
    ) : (
      <div className="tv-scrollmap-list">
        {items.map((item) => (
          <article key={`scroll-${item.milestonePercent}`} className="tv-scrollmap-item">
            <div className="tv-scrollmap-head">
              <span>{item.milestonePercent}% da pagina</span>
              <strong>{item.sessionReachRatePercent.toFixed(1)}%</strong>
            </div>
            <div className="tv-scrollmap-bar">
              <div className="tv-scrollmap-bar-fill" style={{ width: `${Math.max(4, item.sessionReachRatePercent)}%` }} />
            </div>
            <small>{item.sessionsReached} sessao(oes) atingiram este marco</small>
          </article>
        ))}
      </div>
    )}
  </section>
);

const ElementRankingPanel: React.FC<{ items: FireTvElementRankingItem[] }> = ({ items }) => (
  <section className="tv-panel tv-panel--soft">
    <div className="tv-panel-header">
      <h2>Elementos mais clicados</h2>
    </div>
    {items.length === 0 ? (
      <p className="tv-empty-state">Nenhum elemento ranqueado para o filtro atual.</p>
    ) : (
      <div className="tv-element-ranking-list">
        {items.map((item, index) => (
          <article key={`${item.elementKey}-${index}`} className="tv-element-ranking-card">
            <div className="tv-element-ranking-copy">
              <span className="tv-element-ranking-index">#{index + 1}</span>
              <h3>{item.label}</h3>
              <p>{item.href || item.elementKey}</p>
            </div>
            <div className="tv-element-ranking-metrics">
              <strong>{item.clicks}</strong>
              <span>{item.uniqueSessions} sessoes</span>
              <small>{item.sessionRatePercent.toFixed(1)}% das sessoes</small>
            </div>
          </article>
        ))}
      </div>
    )}
  </section>
);

const SessionList: React.FC<{ items: FireTvRecentSession[] }> = ({ items }) => (
  <section className="tv-panel tv-panel--wide">
    <div className="tv-panel-header">
      <h2>Sessoes recentes</h2>
    </div>
    {items.length === 0 ? (
      <p className="tv-empty-state">Nenhuma sessao recente para exibir.</p>
    ) : (
      <div className="tv-session-list">
        {items.map((item) => (
          <article key={item.sessionId} className="tv-session-card">
            <div>
              <span className="tv-session-path">{item.path}</span>
              <h3>{item.estimatedLocality || 'Localidade nao mapeada'}</h3>
            </div>
            <div className="tv-session-meta">
              <span>Ativo: {formatDuration(item.activeSeconds)}</span>
              <span>Scroll: {item.maxScrollPercent}%</span>
              <span>{item.leadStatusLabel}</span>
              <span>Ultima atividade: {item.lastActivityLabel}</span>
            </div>
          </article>
        ))}
      </div>
    )}
  </section>
);

const DashboardScreen: React.FC<DashboardScreenProps> = ({ session, onBack, onLogout }) => {
  const [filters, setFilters] = useState<DashboardFilters>({
    rangeDays: undefined,
    origin: 'all',
    comparisonMode: 'previous_period'
  });
  const [dashboard, setDashboard] = useState<FireTvLandingDashboardData | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState('');

  const loadDashboard = useCallback(async (request: DashboardFilters, options?: { silent?: boolean }) => {
    if (!options?.silent) {
      setIsLoading(true);
    }

    try {
      const payload = await fetchFireTvLandingDashboard(session.token, request);
      setDashboard(payload);
      setFilters({
        rangeDays: payload.selectedRangeDays,
        origin: payload.selectedOrigin,
        comparisonMode: payload.selectedComparisonMode
      });
      setErrorMessage('');
    } catch (error) {
      if (error instanceof FireTvDashboardApiError && error.httpStatus === 401) {
        onLogout();
        return;
      }

      setErrorMessage(error instanceof Error ? error.message : 'Falha ao carregar dashboard.');
    } finally {
      if (!options?.silent) {
        setIsLoading(false);
      }
    }
  }, [onLogout, session.token]);

  useEffect(() => {
    void loadDashboard(filters);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (!dashboard?.autoRefreshSeconds) {
      return;
    }

    const intervalId = window.setInterval(() => {
      void loadDashboard(filters, { silent: true });
    }, dashboard.autoRefreshSeconds * 1000);

    return () => window.clearInterval(intervalId);
  }, [dashboard?.autoRefreshSeconds, filters, loadDashboard]);

  const title = dashboard?.appTitle || 'ConsertaPraMim TV';
  const subtitle = dashboard?.appSubtitle || 'Landing publica';
  const selectedRange = filters.rangeDays || dashboard?.selectedRangeDays || 7;
  const allowedRanges = dashboard?.allowedRangeDays || [1, 7, 30];
  const selectedOrigin = filters.origin || dashboard?.selectedOrigin || 'all';
  const selectedComparisonMode = filters.comparisonMode || dashboard?.selectedComparisonMode || 'previous_period';
  const originOptions = dashboard?.originOptions || [
    { value: 'all', label: 'Todas as origens' },
    { value: 'client', label: 'Cliente' },
    { value: 'provider', label: 'Prestador' }
  ];
  const comparisonOptions = dashboard?.comparisonOptions || [
    { value: 'none', label: 'Sem comparacao' },
    { value: 'previous_period', label: 'Periodo anterior' }
  ];
  const showComparison = dashboard?.showComparison ?? true;
  const generatedAtLabel = dashboard ? formatGeneratedAt(dashboard.generatedAtUtc) : '--';
  const disabled = dashboard && !dashboard.enabled;
  const comparisonWindowLabel = dashboard?.comparisonLabel || 'Sem comparacao';

  const updateFilters = (nextValues: Partial<DashboardFilters>) => {
    const nextFilters = {
      rangeDays: nextValues.rangeDays ?? selectedRange,
      origin: nextValues.origin ?? selectedOrigin,
      comparisonMode: nextValues.comparisonMode ?? selectedComparisonMode
    };

    setFilters(nextFilters);
    void loadDashboard(nextFilters);
  };

  return (
    <div className="tv-shell">
      <div className="tv-stage">
        <header className="tv-topbar">
          <div className="tv-brand-block">
            <img className="tv-brand-logo" src="/logo-wordmark.png" alt="ConsertaPraMim" />
            <div>
              <p className="tv-eyebrow">{subtitle}</p>
              <h1>{title}</h1>
              <p className="tv-supporting-copy">Atualizado em {generatedAtLabel}</p>
            </div>
          </div>

          <div className="tv-topbar-actions">
            <button type="button" className="tv-secondary-button" onClick={onBack}>
              Voltar
            </button>
            <span className="tv-user-chip">{session.userName}</span>
            <button type="button" className="tv-secondary-button" onClick={() => void loadDashboard(filters)}>
              Atualizar
            </button>
            <button type="button" className="tv-secondary-button" onClick={onLogout}>
              Sair
            </button>
          </div>
        </header>

        <main className="tv-dashboard-content">
          <section className="tv-command-deck">
            <div className="tv-command-summary">
              <p className="tv-eyebrow">Leitura atual</p>
              <strong>{selectedRange} dia(s) | {resolveOriginLabel(selectedOrigin)}</strong>
              <span>{showComparison ? comparisonWindowLabel : 'Comparacao desativada'}</span>
            </div>
            <div className="tv-command-filters">
              <RangeFilterGroup selectedValue={selectedRange} options={allowedRanges} onSelect={(value) => updateFilters({ rangeDays: value })} />
              <FilterGroup title="Origem" options={originOptions} selectedValue={selectedOrigin} onSelect={(value) => updateFilters({ origin: value })} />
              {showComparison ? (
                <FilterGroup title="Comparacao" options={comparisonOptions} selectedValue={selectedComparisonMode} onSelect={(value) => updateFilters({ comparisonMode: value })} />
              ) : null}
            </div>
            <div className="tv-window-comparison">
              <article className="tv-window-card">
                <span className="tv-window-card-label">Janela atual</span>
                <strong>{formatDate(dashboard?.fromUtc)} {'->'} {formatDate(dashboard?.toUtc)}</strong>
              </article>
              {showComparison ? (
                <article className="tv-window-card">
                  <span className="tv-window-card-label">Comparativo</span>
                  <strong>{dashboard?.comparisonFromUtc ? `${formatDate(dashboard.comparisonFromUtc)} -> ${formatDate(dashboard.comparisonToUtc)}` : 'Nao aplicado'}</strong>
                </article>
              ) : null}
            </div>
          </section>

          {isLoading ? <section className="tv-loading-panel">Carregando dashboard da landing...</section> : null}
          {errorMessage ? <section className="tv-error-panel">{errorMessage}</section> : null}
          {disabled ? <section className="tv-error-panel">Dashboard TV desativado na configuracao runtime.</section> : null}

          {!isLoading && !errorMessage && dashboard && dashboard.enabled ? (
            <>
              <section className="tv-kpi-grid">
                {dashboard.kpis.map((kpi) => (
                  <article key={kpi.key} className={`tv-kpi-card tv-kpi-card--${kpi.tone}`}>
                    <span>{kpi.label}</span>
                    <strong>{kpi.value}</strong>
                    <small>{kpi.helperText || ' '}</small>
                    {showComparison && kpi.comparisonLabel ? (
                      <div className={`tv-kpi-comparison tv-kpi-comparison--${kpi.comparisonTone || 'neutral'}`}>
                        <span>{kpi.comparisonValue || '--'}</span>
                        <small>{kpi.comparisonLabel}</small>
                        <em>Anterior: {kpi.previousValue || '--'}</em>
                      </div>
                    ) : null}
                  </article>
                ))}
              </section>

              <section className="tv-analytics-grid">
                {dashboard.showHeatmap ? (
                  <section className="tv-panel tv-panel--heatmap">
                    <div className="tv-panel-header">
                      <h2>Heatmap agregado</h2>
                    </div>
                    <HeatmapGrid rows={dashboard.heatmapRows} columns={dashboard.heatmapColumns} cells={dashboard.heatmap} />
                  </section>
                ) : null}

                {dashboard.showScrollmap ? <ScrollmapPanel items={dashboard.scrollmap} /> : null}
                {dashboard.showElementRanking ? <ElementRankingPanel items={dashboard.topElements} /> : null}
              </section>

              <section className="tv-meta-grid">
                <MetricList title="Top origens" items={dashboard.topOrigins} tone="soft" />
                <MetricList title="Top localidades" items={dashboard.topLocalities} tone="soft" />
              </section>

              <SessionList items={dashboard.recentSessions} />
            </>
          ) : null}
        </main>
      </div>
    </div>
  );
};

export default DashboardScreen;
