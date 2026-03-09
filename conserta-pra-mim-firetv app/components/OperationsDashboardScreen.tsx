import * as signalR from '@microsoft/signalr/dist/esm/index.js';
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { FireTvDashboardApiError, fetchFireTvOperationsDashboard } from '../services/dashboard';
import { getApiBaseUrl } from '../services/http';
import type {
  FireTvAuthSession,
  FireTvDashboardKpi,
  FireTvHealthTargetStatus,
  FireTvOperationalCategory,
  FireTvOperationalDailySeriesItem,
  FireTvOperationalMapPoint,
  FireTvOperationalRecentActivity,
  FireTvOperationsDashboardData
} from '../types';

interface OperationsDashboardScreenProps {
  session: FireTvAuthSession;
  onBack: () => void;
  onLogout: () => void;
}

function formatGeneratedAt(value: string): string {
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime())
    ? '--'
    : parsed.toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' });
}

function getToneColor(index: number): string {
  const palette = ['#facc15', '#3b82f6', '#f97316', '#38bdf8', '#22c55e', '#a855f7'];
  return palette[index % palette.length];
}

function buildCategoryGradient(categories: FireTvOperationalCategory[]): string {
  if (categories.length === 0) {
    return 'conic-gradient(#1e293b 0deg 360deg)';
  }

  let cursor = 0;
  const slices = categories.map((item, index) => {
    const start = cursor;
    const angle = Math.max(6, Math.round(item.percent * 3.6));
    cursor += angle;
    return `${getToneColor(index)} ${start}deg ${Math.min(360, cursor)}deg`;
  });

  if (cursor < 360) {
    slices.push(`#1e293b ${cursor}deg 360deg`);
  }

  return `conic-gradient(${slices.join(', ')})`;
}

function resolveHealthSummary(targets: FireTvHealthTargetStatus[]): string {
  if (targets.length === 0) {
    return 'Sem alvos';
  }

  const healthyTargets = targets.filter((item) => item.healthy).length;
  return healthyTargets === targets.length ? 'APIs: OK' : `${healthyTargets}/${targets.length} OK`;
}

function buildMapBounds(points: FireTvOperationalMapPoint[]): { minLat: number; maxLat: number; minLng: number; maxLng: number } | null {
  if (points.length === 0) {
    return null;
  }

  const latitudes = points.map((item) => item.latitude);
  const longitudes = points.map((item) => item.longitude);
  return {
    minLat: Math.min(...latitudes),
    maxLat: Math.max(...latitudes),
    minLng: Math.min(...longitudes),
    maxLng: Math.max(...longitudes)
  };
}

function normalizePoint(point: FireTvOperationalMapPoint, bounds: { minLat: number; maxLat: number; minLng: number; maxLng: number }): { left: string; top: string } {
  const latRange = Math.max(0.01, bounds.maxLat - bounds.minLat);
  const lngRange = Math.max(0.01, bounds.maxLng - bounds.minLng);
  const left = ((point.longitude - bounds.minLng) / lngRange) * 100;
  const top = 100 - ((point.latitude - bounds.minLat) / latRange) * 100;
  return {
    left: `${Math.min(96, Math.max(4, left))}%`,
    top: `${Math.min(94, Math.max(6, top))}%`
  };
}

const HealthStrip: React.FC<{
  dashboard: FireTvOperationsDashboardData;
  realtimeConnected: boolean;
  currentTimeLabel: string;
}> = ({ dashboard, realtimeConnected, currentTimeLabel }) => {
  const statusClass = dashboard.overallStatus === 'online'
    ? 'is-success'
    : dashboard.overallStatus === 'warning'
      ? 'is-warning'
      : 'is-danger';

  return (
    <div className="tv-operations-status">
      <div className={`tv-live-pill ${statusClass}`}>
        <span className="tv-live-dot" />
        {realtimeConnected ? 'ONLINE' : 'RECONNECT'}
      </div>
      <span>Latencia: {dashboard.averageLatencyMs ?? '--'}ms</span>
      <span>{resolveHealthSummary(dashboard.healthTargets)}</span>
      <span className="tv-clock">{currentTimeLabel}</span>
    </div>
  );
};

const HealthTargetsPanel: React.FC<{ targets: FireTvHealthTargetStatus[] }> = ({ targets }) => (
  <section className="tv-panel tv-panel--glass">
    <div className="tv-panel-header">
      <h2>Health check</h2>
    </div>
    <div className="tv-health-target-list">
      {targets.map((item) => (
        <article key={item.key} className={`tv-health-target ${item.healthy ? 'is-healthy' : 'is-unhealthy'}`}>
          <div>
            <strong>{item.label}</strong>
            <span>{item.statusLabel}</span>
          </div>
          <small>{item.latencyMs ? `${item.latencyMs}ms` : item.detail || '--'}</small>
        </article>
      ))}
    </div>
  </section>
);

const CategoryPanel: React.FC<{ categories: FireTvOperationalCategory[] }> = ({ categories }) => (
  <section className="tv-panel tv-panel--glass">
    <div className="tv-panel-header">
      <h2>Servicos por categoria</h2>
    </div>
    {categories.length === 0 ? (
      <p className="tv-empty-state">Sem categorias para o periodo.</p>
    ) : (
      <div className="tv-category-panel">
        <div className="tv-category-chart" style={{ backgroundImage: buildCategoryGradient(categories) }}>
          <div className="tv-category-chart-core">Mix</div>
        </div>
        <div className="tv-category-legend">
          {categories.map((item, index) => (
            <div key={item.category} className="tv-category-legend-row">
              <span className="tv-category-color" style={{ backgroundColor: getToneColor(index) }} />
              <span>{item.category}</span>
              <strong>{item.percent.toFixed(1)}%</strong>
            </div>
          ))}
        </div>
      </div>
    )}
  </section>
);

const MapPanel: React.FC<{
  providerPoints: FireTvOperationalMapPoint[];
  requestPoints: FireTvOperationalMapPoint[];
}> = ({ providerPoints, requestPoints }) => {
  const allPoints = [...providerPoints, ...requestPoints];
  const bounds = useMemo(() => buildMapBounds(allPoints), [allPoints]);

  return (
    <section className="tv-panel tv-panel--map">
      <div className="tv-panel-header">
        <h2>Mapa de atendimentos</h2>
      </div>
      {!bounds ? (
        <p className="tv-empty-state">Sem pontos georreferenciados para exibir.</p>
      ) : (
        <div className="tv-map-surface">
          <div className="tv-map-grid" />
          {providerPoints.map((point) => {
            const style = normalizePoint(point, bounds);
            return (
              <div
                key={`provider-${point.id}`}
                className={`tv-map-point tv-map-point--provider is-${point.tone}`}
                style={style}
                title={`${point.label} • ${point.subtitle}`}
              >
                P
              </div>
            );
          })}
          {requestPoints.map((point) => {
            const style = normalizePoint(point, bounds);
            return (
              <div
                key={`request-${point.id}`}
                className={`tv-map-point tv-map-point--request is-${point.tone}`}
                style={style}
                title={`${point.label} • ${point.subtitle}`}
              >
                S
              </div>
            );
          })}
          <div className="tv-map-legend">
            <span><i className="tv-legend-bullet is-provider" /> Prestadores</span>
            <span><i className="tv-legend-bullet is-request" /> Pedidos</span>
          </div>
        </div>
      )}
    </section>
  );
};

const RecentActivityPanel: React.FC<{ items: FireTvOperationalRecentActivity[] }> = ({ items }) => (
  <section className="tv-panel tv-panel--glass">
    <div className="tv-panel-header">
      <h2>Mapa de atendimentos</h2>
    </div>
    {items.length === 0 ? (
      <p className="tv-empty-state">Sem atividade recente.</p>
    ) : (
      <div className="tv-activity-list">
        {items.map((item, index) => (
          <article key={`${item.timeLabel}-${index}`} className={`tv-activity-item is-${item.tone}`}>
            <strong>{item.timeLabel}</strong>
            <div>
              <h3>{item.title}</h3>
              <p>{item.subtitle}</p>
            </div>
          </article>
        ))}
      </div>
    )}
  </section>
);

const DailySeriesPanel: React.FC<{ items: FireTvOperationalDailySeriesItem[] }> = ({ items }) => {
  const maxValue = items.reduce((max, item) => Math.max(max, item.requests, item.attendances), 0);

  return (
    <section className="tv-panel tv-panel--glass">
      <div className="tv-panel-header">
        <h2>Pedidos e atendimentos por dia</h2>
      </div>
      {items.length === 0 ? (
        <p className="tv-empty-state">Sem serie diaria disponivel.</p>
      ) : (
        <div className="tv-bar-chart">
          {items.map((item) => (
            <article key={item.label} className="tv-bar-chart-item">
              <div className="tv-bar-chart-columns">
                <div className="tv-bar-chart-column is-requests">
                  <div style={{ height: `${maxValue === 0 ? 8 : Math.max(8, (item.requests / maxValue) * 100)}%` }} />
                </div>
                <div className="tv-bar-chart-column is-attendances">
                  <div style={{ height: `${maxValue === 0 ? 8 : Math.max(8, (item.attendances / maxValue) * 100)}%` }} />
                </div>
              </div>
              <strong>{item.label}</strong>
            </article>
          ))}
        </div>
      )}
    </section>
  );
};

const KpiCard: React.FC<{ item: FireTvDashboardKpi; compact?: boolean }> = ({ item, compact = false }) => (
  <article className={`tv-kpi-card tv-kpi-card--${item.tone} ${compact ? 'tv-kpi-card--compact' : 'tv-kpi-card--hero'}`}>
    <span>{item.label}</span>
    <strong>{item.value}</strong>
    {item.helperText ? <small>{item.helperText}</small> : null}
  </article>
);

const OperationsDashboardScreen: React.FC<OperationsDashboardScreenProps> = ({
  session,
  onBack,
  onLogout
}) => {
  const [dashboard, setDashboard] = useState<FireTvOperationsDashboardData | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState('');
  const [realtimeConnected, setRealtimeConnected] = useState(false);
  const [clock, setClock] = useState(() => new Date());

  const loadDashboard = useCallback(async (options?: { silent?: boolean }) => {
    if (!options?.silent) {
      setIsLoading(true);
    }

    try {
      const payload = await fetchFireTvOperationsDashboard(session.token);
      setDashboard(payload);
      setErrorMessage('');
    } catch (error) {
      if (error instanceof FireTvDashboardApiError && error.httpStatus === 401) {
        onLogout();
        return;
      }

      setErrorMessage(error instanceof Error ? error.message : 'Falha ao carregar a visao operacional.');
    } finally {
      if (!options?.silent) {
        setIsLoading(false);
      }
    }
  }, [onLogout, session.token]);

  useEffect(() => {
    void loadDashboard();
  }, [loadDashboard]);

  useEffect(() => {
    const timerId = window.setInterval(() => setClock(new Date()), 1000);
    return () => window.clearInterval(timerId);
  }, []);

  useEffect(() => {
    if (!dashboard?.refreshSeconds) {
      return;
    }

    const intervalId = window.setInterval(() => {
      void loadDashboard({ silent: true });
    }, dashboard.refreshSeconds * 1000);

    return () => window.clearInterval(intervalId);
  }, [dashboard?.refreshSeconds, loadDashboard]);

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${getApiBaseUrl()}/fireTvDashboardHub`, {
        accessTokenFactory: () => session.token
      })
      .withAutomaticReconnect()
      .build();

    connection.on('FireTvDashboardPulse', () => {
      setRealtimeConnected(true);
      void loadDashboard({ silent: true });
    });

    connection.onreconnected(() => setRealtimeConnected(true));
    connection.onreconnecting(() => setRealtimeConnected(false));
    connection.onclose(() => setRealtimeConnected(false));

    void connection
      .start()
      .then(async () => {
        setRealtimeConnected(true);
        try {
          await connection.invoke('JoinDashboardGroup');
        } catch {
          // SignalR group join is optional because the hub already auto-joins admins.
        }
      })
      .catch(() => setRealtimeConnected(false));

    return () => {
      void connection.stop();
    };
  }, [loadDashboard, session.token]);

  const topKpis = dashboard?.kpis.slice(0, 4) ?? [];
  const bottomKpis = dashboard?.kpis.slice(4, 8) ?? [];
  const currentTimeLabel = clock.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });

  return (
    <div className="tv-shell tv-shell--operations">
      <div className="tv-stage">
        <header className="tv-topbar tv-topbar--operations">
          <div className="tv-brand-block">
            <img className="tv-brand-logo" src="/logo-wordmark.png" alt="ConsertaPraMim" />
          </div>
          <div className="tv-topbar-actions">
            <button type="button" className="tv-secondary-button" onClick={onBack}>
              Voltar
            </button>
            <button type="button" className="tv-secondary-button" onClick={onLogout}>
              Sair
            </button>
          </div>
        </header>

        {dashboard ? (
          <HealthStrip dashboard={dashboard} realtimeConnected={realtimeConnected} currentTimeLabel={currentTimeLabel} />
        ) : null}

        {isLoading ? <div className="tv-loading-panel">Carregando visao operacional...</div> : null}
        {errorMessage ? <div className="tv-error-panel">{errorMessage}</div> : null}

        {!isLoading && dashboard ? (
          <div className="tv-operations-layout">
            <div className="tv-kpi-grid tv-kpi-grid--operations">
              {topKpis.map((item) => (
                <KpiCard key={item.key} item={item} />
              ))}
            </div>

            <div className="tv-operations-main-grid">
              <CategoryPanel categories={dashboard.categories} />
              <MapPanel providerPoints={dashboard.providerPoints} requestPoints={dashboard.requestPoints} />
              <RecentActivityPanel items={dashboard.recentActivity} />
            </div>

            <div className="tv-operations-bottom-grid">
              <DailySeriesPanel items={dashboard.dailySeries} />
              <div className="tv-kpi-stack">
                {bottomKpis.map((item) => (
                  <KpiCard key={item.key} item={item} compact />
                ))}
              </div>
              <HealthTargetsPanel targets={dashboard.healthTargets} />
            </div>

            <div className="tv-status-footer">
              <span>Ultima atualizacao: {formatGeneratedAt(dashboard.generatedAtUtc)}</span>
              <span>Janela operacional: {dashboard.historyDays} dia(s)</span>
            </div>
          </div>
        ) : null}
      </div>
    </div>
  );
};

export default OperationsDashboardScreen;
