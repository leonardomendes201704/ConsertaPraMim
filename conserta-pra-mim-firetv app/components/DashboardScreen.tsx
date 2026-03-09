import React, { useCallback, useEffect, useState } from 'react';
import { FireTvDashboardApiError, fetchFireTvLandingDashboard } from '../services/dashboard';
import type { FireTvAuthSession, FireTvBreakdownItem, FireTvLandingDashboardData, FireTvRecentSession } from '../types';
import HeatmapGrid from './HeatmapGrid';

interface DashboardScreenProps {
  session: FireTvAuthSession;
  onLogout: () => void;
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

function formatDuration(seconds: number): string {
  if (seconds < 60) {
    return `${seconds}s`;
  }

  const minutes = Math.floor(seconds / 60);
  const remainingSeconds = seconds % 60;
  return remainingSeconds > 0 ? `${minutes}m ${remainingSeconds}s` : `${minutes}m`;
}

const MetricList: React.FC<{ title: string; items: FireTvBreakdownItem[] }> = ({ title, items }) => (
  <section className="tv-panel">
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

const DashboardScreen: React.FC<DashboardScreenProps> = ({ session, onLogout }) => {
  const [rangeDays, setRangeDays] = useState<number>();
  const [dashboard, setDashboard] = useState<FireTvLandingDashboardData | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState('');

  const loadDashboard = useCallback(async (selectedRangeDays?: number, options?: { silent?: boolean }) => {
    if (!options?.silent) {
      setIsLoading(true);
    }

    try {
      const payload = await fetchFireTvLandingDashboard(session.token, selectedRangeDays);
      setDashboard(payload);
      setRangeDays(payload.selectedRangeDays);
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
    void loadDashboard(rangeDays);
  }, []);

  useEffect(() => {
    if (!dashboard?.autoRefreshSeconds) {
      return;
    }

    const intervalId = window.setInterval(() => {
      void loadDashboard(rangeDays, { silent: true });
    }, dashboard.autoRefreshSeconds * 1000);

    return () => window.clearInterval(intervalId);
  }, [dashboard?.autoRefreshSeconds, loadDashboard, rangeDays]);

  const title = dashboard?.appTitle || 'ConsertaPraMim TV';
  const subtitle = dashboard?.appSubtitle || 'Landing publica';
  const allowedRanges = dashboard?.allowedRangeDays || [1, 7, 30];
  const generatedAtLabel = dashboard ? formatGeneratedAt(dashboard.generatedAtUtc) : '--';
  const disabled = dashboard && !dashboard.enabled;

  return (
    <div className="tv-shell">
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
          <span className="tv-user-chip">{session.userName}</span>
          <button type="button" className="tv-secondary-button" onClick={() => void loadDashboard(rangeDays)}>
            Atualizar
          </button>
          <button type="button" className="tv-secondary-button" onClick={onLogout}>
            Sair
          </button>
        </div>
      </header>

      <main className="tv-dashboard-content">
        <section className="tv-range-bar">
          <div>
            <p className="tv-eyebrow">Janela</p>
            <strong>{rangeDays || '--'} dia(s)</strong>
          </div>
          <div className="tv-range-actions">
            {allowedRanges.map((option) => (
              <button
                key={option}
                type="button"
                className={`tv-chip-button ${rangeDays === option ? 'is-active' : ''}`}
                onClick={() => void loadDashboard(option)}
              >
                {option} dia(s)
              </button>
            ))}
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
                </article>
              ))}
            </section>

            <section className="tv-lower-grid">
              <section className="tv-panel tv-panel--heatmap">
                <div className="tv-panel-header">
                  <h2>Heatmap agregado</h2>
                </div>
                <HeatmapGrid rows={dashboard.heatmapRows} columns={dashboard.heatmapColumns} cells={dashboard.heatmap} />
              </section>

              <div className="tv-side-column">
                <MetricList title="Top origens" items={dashboard.topOrigins} />
                <MetricList title="Top localidades" items={dashboard.topLocalities} />
              </div>
            </section>

            <SessionList items={dashboard.recentSessions} />
          </>
        ) : null}
      </main>
    </div>
  );
};

export default DashboardScreen;
