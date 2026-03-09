import * as signalR from '@microsoft/signalr/dist/esm/index.js';
import L from 'leaflet';
import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
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

function createMapIcon(type: 'provider' | 'request', tone: string, label: string): L.DivIcon {
  return L.divIcon({
    className: 'tv-map-marker-shell',
    html: `<span class="tv-map-point tv-map-point--${type} is-${tone}">${label}</span>`,
    iconSize: [34, 34],
    iconAnchor: [17, 17]
  });
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

const CategoryPanel: React.FC<{ categories: FireTvOperationalCategory[]; panelHeight?: number | null }> = ({ categories, panelHeight }) => (
  <section className="tv-panel tv-panel--glass tv-panel--equal-height" style={panelHeight ? { height: `${panelHeight}px` } : undefined}>
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
  panelHeight?: number | null;
}> = ({ providerPoints, requestPoints, panelHeight }) => {
  const mapElementRef = useRef<HTMLDivElement | null>(null);
  const mapInstanceRef = useRef<L.Map | null>(null);
  const markerLayerRef = useRef<L.LayerGroup | null>(null);
  const allPoints = useMemo(() => [...providerPoints, ...requestPoints], [providerPoints, requestPoints]);

  useEffect(() => {
    if (!mapElementRef.current || mapInstanceRef.current) {
      return;
    }

    const map = L.map(mapElementRef.current, {
      zoomControl: false,
      attributionControl: false,
      dragging: false,
      scrollWheelZoom: false,
      doubleClickZoom: false,
      boxZoom: false,
      keyboard: false,
      touchZoom: false
    }).setView([-23.967, -46.334], 11);

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '&copy; OpenStreetMap contributors'
    }).addTo(map);

    mapInstanceRef.current = map;
    markerLayerRef.current = L.layerGroup().addTo(map);

    return () => {
      markerLayerRef.current?.clearLayers();
      markerLayerRef.current = null;
      mapInstanceRef.current?.remove();
      mapInstanceRef.current = null;
    };
  }, []);

  useEffect(() => {
    const map = mapInstanceRef.current;
    const markerLayer = markerLayerRef.current;
    if (!map || !markerLayer) {
      return;
    }

    markerLayer.clearLayers();

    const markers: L.Marker[] = [];

    for (const point of providerPoints) {
      const marker = L.marker([point.latitude, point.longitude], {
        icon: createMapIcon('provider', point.tone, 'P'),
        keyboard: false
      }).bindTooltip(`${point.label} - ${point.subtitle}`, {
        direction: 'top',
        offset: [0, -18],
        opacity: 0.92
      });

      marker.addTo(markerLayer);
      markers.push(marker);
    }

    for (const point of requestPoints) {
      const marker = L.marker([point.latitude, point.longitude], {
        icon: createMapIcon('request', point.tone, 'S'),
        keyboard: false
      }).bindTooltip(`${point.label} - ${point.subtitle}`, {
        direction: 'top',
        offset: [0, -18],
        opacity: 0.92
      });

      marker.addTo(markerLayer);
      markers.push(marker);
    }

    if (markers.length === 0) {
      map.setView([-23.967, -46.334], 11, { animate: false });
      return;
    }

    if (markers.length === 1) {
      map.setView(markers[0].getLatLng(), 15, { animate: false });
      return;
    }

    const bounds = L.featureGroup(markers).getBounds();
    map.fitBounds(bounds.pad(0.08), {
      animate: false,
      padding: [18, 18],
      maxZoom: 16
    });

    const tighterZoom = Math.min(17, map.getZoom() + 1);
    map.setView(bounds.getCenter(), tighterZoom, { animate: false });
  }, [providerPoints, requestPoints]);

  return (
    <section className="tv-panel tv-panel--map tv-panel--equal-height" style={panelHeight ? { height: `${panelHeight}px` } : undefined}>
      <div className="tv-panel-header">
        <h2>Mapa de atendimentos</h2>
      </div>
      {allPoints.length === 0 ? (
        <p className="tv-empty-state">Sem pontos georreferenciados para exibir.</p>
      ) : (
        <div className="tv-map-surface">
          <div ref={mapElementRef} className="tv-leaflet-map" aria-label="Mapa operacional" />
          <div className="tv-map-legend">
            <span><i className="tv-legend-bullet is-provider" /> Prestadores</span>
            <span><i className="tv-legend-bullet is-request" /> Pedidos</span>
          </div>
        </div>
      )}
    </section>
  );
};

const RecentActivityPanel: React.FC<{
  items: FireTvOperationalRecentActivity[];
  onHeightChange?: (height: number) => void;
}> = ({ items, onHeightChange }) => {
  const panelRef = useRef<HTMLElement | null>(null);

  useEffect(() => {
    if (!panelRef.current || !onHeightChange) {
      return;
    }

    const emitHeight = () => {
      if (panelRef.current) {
        onHeightChange(panelRef.current.offsetHeight);
      }
    };

    emitHeight();

    const observer = new ResizeObserver(() => emitHeight());
    observer.observe(panelRef.current);

    return () => observer.disconnect();
  }, [items, onHeightChange]);

  return (
    <section ref={panelRef} className="tv-panel tv-panel--glass tv-panel--activity">
      <div className="tv-panel-header">
        <h2>Últimos serviços</h2>
      </div>
      {items.length === 0 ? (
        <p className="tv-empty-state">Sem atividade recente.</p>
      ) : (
        <div className="tv-activity-list">
          {items.map((item, index) => (
            <article key={`${item.categoryIcon}-${item.title}-${index}`} className={`tv-activity-item is-${item.tone}`}>
              <span className={`material-symbols-outlined tv-activity-category-icon is-${item.tone}`} aria-hidden="true">
                {item.categoryIcon || 'build_circle'}
              </span>
              <div>
                <h3>{item.title}</h3>
              </div>
            </article>
          ))}
        </div>
      )}
    </section>
  );
};

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

function resolveHeroKpiMeta(item: FireTvDashboardKpi): { iconKey: string; label: string; helper?: string; showRatingStars?: boolean } {
  switch (item.key) {
    case 'servicesToday':
      return { iconKey: 'services', label: 'Servicos', helper: 'Hoje' };
    case 'registeredProviders':
      return { iconKey: 'providers', label: 'Profissionais', helper: 'Cadastrados' };
    case 'activeAttendances':
      return { iconKey: 'attendances', label: 'Atendimentos', helper: 'Ativos' };
    case 'averageRating':
      return { iconKey: 'rating', label: 'Avaliacao Media', showRatingStars: true };
    default:
      return { iconKey: 'default', label: item.label, helper: item.helperText ?? undefined };
  }
}

function renderHeroIcon(iconKey: string): React.ReactNode {
  switch (iconKey) {
    case 'services':
      return <i className="bi bi-tools" aria-hidden="true" />;
    case 'providers':
      return <i className="bi bi-person-fill" aria-hidden="true" />;
    case 'attendances':
      return <i className="bi bi-geo-alt-fill" aria-hidden="true" />;
    case 'rating':
      return <i className="bi bi-star-fill" aria-hidden="true" />;
    default:
      return <i className="bi bi-circle-fill" aria-hidden="true" />;
  }
}

function renderRatingStars(rawValue: string): React.ReactNode {
  const parsed = Number.parseFloat(rawValue.replace(',', '.'));
  const filledStars = Number.isFinite(parsed)
    ? Math.max(0, Math.min(5, Math.round(parsed)))
    : 0;

  return (
    <span className="tv-kpi-hero-stars" aria-label={`Avaliacao ${rawValue}`}>
      {Array.from({ length: 5 }, (_, index) => (
        <span
          key={`star-${index}`}
          className={index < filledStars ? 'is-filled' : 'is-empty'}
          aria-hidden="true"
        >
          {'\u2605'}
        </span>
      ))}
    </span>
  );
}
const KpiCard: React.FC<{ item: FireTvDashboardKpi; compact?: boolean }> = ({ item, compact = false }) => {
  if (compact) {
    return (
      <article className={`tv-kpi-card tv-kpi-card--${item.tone} tv-kpi-card--compact`}>
        <span>{item.label}</span>
        <strong>{item.value}</strong>
        {item.helperText ? <small>{item.helperText}</small> : null}
      </article>
    );
  }

  const heroMeta = resolveHeroKpiMeta(item);

  return (
    <article className={`tv-kpi-card tv-kpi-card--${item.tone} tv-kpi-card--hero`}>
      <div className="tv-kpi-hero-layout">
        <span className={`tv-kpi-hero-icon tv-kpi-hero-icon--${item.tone} tv-kpi-hero-icon-key--${heroMeta.iconKey}`} aria-hidden="true">
          {renderHeroIcon(heroMeta.iconKey)}
        </span>
        <strong>{item.value}</strong>
        <div className="tv-kpi-hero-copy">
          <span className="tv-kpi-hero-label">{heroMeta.label}</span>
          {heroMeta.showRatingStars ? (
            renderRatingStars(item.value)
          ) : heroMeta.helper ? (
            <small className="tv-kpi-hero-helper">{heroMeta.helper}</small>
          ) : null}
        </div>
      </div>
    </article>
  );
};

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
  const [activityPanelHeight, setActivityPanelHeight] = useState<number | null>(null);

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
          <div className="tv-topbar-actions tv-topbar-actions--operations">
            {dashboard ? (
              <HealthStrip dashboard={dashboard} realtimeConnected={realtimeConnected} currentTimeLabel={currentTimeLabel} />
            ) : null}
            <button type="button" className="tv-secondary-button" onClick={onBack}>
              Voltar
            </button>
            <button type="button" className="tv-secondary-button" onClick={onLogout}>
              Sair
            </button>
          </div>
        </header>

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
              <CategoryPanel categories={dashboard.categories} panelHeight={activityPanelHeight} />
              <MapPanel providerPoints={dashboard.providerPoints} requestPoints={dashboard.requestPoints} panelHeight={activityPanelHeight} />
              <RecentActivityPanel items={dashboard.recentActivity} onHeightChange={setActivityPanelHeight} />
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



