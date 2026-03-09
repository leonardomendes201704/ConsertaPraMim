import * as signalR from '@microsoft/signalr/dist/esm/index.js';
import L from 'leaflet';
import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  XAxis,
  YAxis
} from 'recharts';
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
  return (
    <div className="tv-operations-status">
      <span>Latencia: {dashboard.averageLatencyMs ?? '--'}ms{!realtimeConnected ? ' • Reconnect' : ''}</span>
      <div className="tv-health-badge-list">
        {dashboard.healthTargets.map((item) => (
          <span
            key={item.key}
            className={`tv-health-badge ${item.healthy ? 'is-healthy' : 'is-unhealthy'}`}
          >
            {item.label} {item.healthy ? 'Online' : 'Offline'}
          </span>
        ))}
      </div>
      <span className="tv-clock">{currentTimeLabel}</span>
    </div>
  );
};

const CategoryPanel: React.FC<{ categories: FireTvOperationalCategory[]; panelHeight?: number | null }> = ({ categories, panelHeight }) => (
  <section className="tv-panel tv-panel--glass tv-panel--equal-height" style={panelHeight ? { height: `${panelHeight}px` } : undefined}>
    <div className="tv-panel-header">
      <h2>Servicos por categoria</h2>
    </div>
    {categories.length === 0 ? (
      <p className="tv-empty-state">Sem categorias para o periodo.</p>
    ) : (
      <div className="tv-category-panel">
        <div className="tv-category-chart" style={{ backgroundImage: buildCategoryGradient(categories) }} />
        <div className="tv-category-legend">
          {categories.map((item, index) => (
            <div key={item.category} className="tv-category-legend-row">
              <span className="tv-category-color" style={{ backgroundColor: getToneColor(index) }} />
              <span>{item.category}</span>
              <strong>{Math.round(item.percent)}%</strong>
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
    const mapElement = mapElementRef.current;
    const map = mapInstanceRef.current;
    if (!mapElement || !map) {
      return;
    }

    let frameId = 0;
    const syncMapSize = () => {
      if (frameId) {
        window.cancelAnimationFrame(frameId);
      }

      frameId = window.requestAnimationFrame(() => {
        map.invalidateSize({ pan: false, animate: false });
      });
    };

    syncMapSize();

    const observer = new ResizeObserver(() => {
      syncMapSize();
    });

    observer.observe(mapElement);

    return () => {
      observer.disconnect();
      if (frameId) {
        window.cancelAnimationFrame(frameId);
      }
    };
  }, [panelHeight]);

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
  return (
    <section className="tv-panel tv-panel--glass tv-panel--daily-series">
      <div className="tv-panel-header">
        <h2>Pedidos e atendimentos por dia</h2>
      </div>
      {items.length === 0 ? (
        <p className="tv-empty-state">Sem serie diaria disponivel.</p>
      ) : (
        <div className="tv-line-chart">
          <div className="tv-line-chart-canvas" role="img" aria-label="Serie diaria de pedidos e atendimentos">
            <ResponsiveContainer width="100%" height="100%">
              <LineChart data={items} margin={{ top: 4, right: 6, left: 6, bottom: -6 }}>
                <CartesianGrid vertical={false} stroke="rgba(148,163,184,0.12)" />
                <XAxis
                  dataKey="label"
                  axisLine={false}
                  tickLine={false}
                  tick={{ fill: '#cbd5e1', fontSize: 13, fontWeight: 700 }}
                  dy={2}
                />
                <YAxis hide domain={[0, 'dataMax']} />
                <Line
                  type="monotone"
                  dataKey="requests"
                  stroke="#38bdf8"
                  strokeWidth={3}
                  dot={{ r: 4, fill: '#38bdf8', stroke: '#08142a', strokeWidth: 1.5 }}
                  activeDot={false}
                />
                <Line
                  type="monotone"
                  dataKey="attendances"
                  stroke="#22c55e"
                  strokeWidth={3}
                  dot={{ r: 4, fill: '#22c55e', stroke: '#08142a', strokeWidth: 1.5 }}
                  activeDot={false}
                />
              </LineChart>
            </ResponsiveContainer>
          </div>
        </div>
      )}
    </section>
  );
};

const ProgressMetricCard: React.FC<{
  item?: FireTvDashboardKpi;
  progressPercent: number;
  progressTone: 'success' | 'danger';
}> = ({ item, progressPercent, progressTone }) => {
  const isCompletedServices = item?.key === 'completedServices';
  const isCancelledCalls = item?.key === 'cancelledCalls';
  const usesStackedCopy = isCompletedServices || isCancelledCalls;

  return (
    <section className={`tv-kpi-card tv-kpi-card--compact tv-kpi-card--progress tv-kpi-card--progress-${progressTone}`}>
      <div className="tv-kpi-progress-layout">
        <strong className={usesStackedCopy ? 'tv-kpi-progress-value--hero' : undefined}>{item?.value ?? '--'}</strong>
        <div className={`tv-kpi-progress-copy ${usesStackedCopy ? 'tv-kpi-progress-copy--stacked' : ''}`}>
          {isCompletedServices ? (
            <>
              <span>Servicos</span>
              <small>Concluidos</small>
              <em>Hoje</em>
            </>
          ) : isCancelledCalls ? (
            <>
              <span>Chamados</span>
              <small>Cancelados</small>
              <em>Hoje</em>
            </>
          ) : (
            <>
              <span>{item?.label ?? '--'}</span>
              {item?.helperText ? <small>{item.helperText}</small> : null}
            </>
          )}
        </div>
      </div>
      <div className="tv-kpi-progress-track" aria-hidden="true">
        <div style={{ width: `${Math.max(0, Math.min(100, progressPercent))}%` }} />
      </div>
    </section>
  );
};

const DualMetricPanel: React.FC<{
  left?: FireTvDashboardKpi;
  right?: FireTvDashboardKpi;
}> = ({ left, right }) => (
  <section className="tv-kpi-card tv-kpi-card--compact tv-kpi-card--dual">
    <div className="tv-kpi-dual-metric">
      <strong>{left?.value ?? '--'}</strong>
      <span>{left?.label ?? '--'}</span>
      {left?.helperText ? <small>{left.helperText}</small> : null}
    </div>
    <div className="tv-kpi-dual-metric">
      <strong>{right?.value ?? '--'}</strong>
      <span>{right?.label ?? '--'}</span>
      {right?.helperText ? <small>{right.helperText}</small> : null}
    </div>
  </section>
);

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
  const completedServicesKpi = dashboard?.kpis.find((item) => item.key === 'completedServices');
  const slaKpi = dashboard?.kpis.find((item) => item.key === 'sla');
  const monthlyRevenueKpi = dashboard?.kpis.find((item) => item.key === 'monthlySubscriptionRevenue');
  const cancelledCallsKpi = dashboard?.kpis.find((item) => item.key === 'cancelledCalls');
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
              <ProgressMetricCard item={completedServicesKpi} progressPercent={72} progressTone="success" />
              <DualMetricPanel left={slaKpi} right={monthlyRevenueKpi} />
              <ProgressMetricCard item={cancelledCallsKpi} progressPercent={34} progressTone="danger" />
            </div>
          </div>
        ) : null}
      </div>
    </div>
  );
};

export default OperationsDashboardScreen;



