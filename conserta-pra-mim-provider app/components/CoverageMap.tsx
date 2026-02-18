import React, { useCallback, useEffect, useMemo, useRef } from 'react';
import { ProviderCoverageMapData } from '../types';

interface Props {
  data: ProviderCoverageMapData | null;
  loading: boolean;
  error: string;
  onRefresh: () => Promise<void>;
  onOpenRequestById: (requestId: string) => void;
  showExpandButton?: boolean;
  onExpand?: () => void;
  mapHeightClassName?: string;
}

declare global {
  interface Window {
    L?: any;
  }
}

function formatDistance(distanceKm?: number): string {
  if (!Number.isFinite(distanceKm)) {
    return '-';
  }

  return `${distanceKm!.toFixed(1)} km`;
}

function escapeHtml(value: string): string {
  return String(value)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');
}

function buildPinColor(isWithinInterestRadius: boolean): string {
  return isWithinInterestRadius ? '#0D6EFD' : '#98A2B3';
}

const CoverageMap: React.FC<Props> = ({
  data,
  loading,
  error,
  onRefresh,
  onOpenRequestById,
  showExpandButton = false,
  onExpand,
  mapHeightClassName
}) => {
  const mapContainerRef = useRef<HTMLDivElement | null>(null);
  const mapRef = useRef<any>(null);
  const markersLayerRef = useRef<any>(null);
  const providerLayerRef = useRef<any[]>([]);
  const lastBoundsRef = useRef<any>(null);

  const canRenderMap = useMemo(() => {
    if (!data?.hasBaseLocation) {
      return false;
    }

    return Number.isFinite(data.providerLatitude) && Number.isFinite(data.providerLongitude);
  }, [data?.hasBaseLocation, data?.providerLatitude, data?.providerLongitude]);

  const buildBoundsFromData = useCallback((leaflet: any, payload: ProviderCoverageMapData) => {
    const bounds = leaflet.latLngBounds([[payload.providerLatitude, payload.providerLongitude]]);

    (payload.pins || []).forEach((pin) => {
      if (Number.isFinite(pin.latitude) && Number.isFinite(pin.longitude)) {
        bounds.extend([pin.latitude, pin.longitude]);
      }
    });

    return bounds.isValid() ? bounds : null;
  }, []);

  const fitToLastBounds = useCallback(() => {
    const map = mapRef.current;
    const bounds = lastBoundsRef.current;
    if (!map || !bounds || !bounds.isValid?.()) {
      return;
    }

    try {
      map.invalidateSize();
      map.fitBounds(bounds.pad(0.15), { maxZoom: 13, animate: false });
    } catch {
      // noop
    }
  }, []);

  useEffect(() => {
    const container = mapContainerRef.current;
    const leaflet = window.L;
    if (!container || !leaflet || !canRenderMap) {
      return;
    }

    if (mapRef.current?.getContainer && mapRef.current.getContainer() !== container) {
      try {
        mapRef.current.remove();
      } catch {
        // noop
      }
      mapRef.current = null;
      markersLayerRef.current = null;
      providerLayerRef.current = [];
    }

    if (!mapRef.current) {
      mapRef.current = leaflet.map(container, {
        zoomControl: true,
        attributionControl: true
      });

      leaflet
        .tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
          maxZoom: 19,
          attribution: '&copy; OpenStreetMap contributors'
        })
        .addTo(mapRef.current);
    }

    const initialBounds = buildBoundsFromData(leaflet, data!);
    lastBoundsRef.current = initialBounds;
    if (initialBounds) {
      fitToLastBounds();
    } else {
      mapRef.current.setView([data!.providerLatitude, data!.providerLongitude], 12);
    }

    window.setTimeout(() => {
      try {
        mapRef.current?.invalidateSize();
        fitToLastBounds();
      } catch {
        // noop
      }
    }, 60);
  }, [canRenderMap, data, fitToLastBounds, buildBoundsFromData]);

  useEffect(() => {
    if (!mapRef.current || !canRenderMap) {
      return;
    }

    window.setTimeout(() => {
      try {
        mapRef.current?.invalidateSize();
        fitToLastBounds();
      } catch {
        // noop
      }
    }, 40);
  }, [loading, canRenderMap, fitToLastBounds]);

  useEffect(() => {
    const leaflet = window.L;
    if (!leaflet || !mapRef.current || !canRenderMap || !data) {
      return;
    }

    if (!markersLayerRef.current) {
      markersLayerRef.current = leaflet.layerGroup().addTo(mapRef.current);
    }

    markersLayerRef.current.clearLayers();
    providerLayerRef.current.forEach((layer) => {
      try {
        mapRef.current.removeLayer(layer);
      } catch {
        // noop
      }
    });
    providerLayerRef.current = [];

    const providerMarker = leaflet.circleMarker([data.providerLatitude, data.providerLongitude], {
      radius: 7,
      color: '#065986',
      fillColor: '#0EA5E9',
      fillOpacity: 0.95,
      weight: 2
    }).addTo(mapRef.current);
    providerMarker.bindPopup('<strong>Minha base</strong>');
    providerLayerRef.current.push(providerMarker);

    if (Number.isFinite(data.interestRadiusKm) && data.interestRadiusKm! > 0) {
      const radiusLayer = leaflet.circle([data.providerLatitude, data.providerLongitude], {
        radius: data.interestRadiusKm! * 1000,
        color: '#0D6EFD',
        fillColor: '#0D6EFD',
        fillOpacity: 0.08,
        weight: 1.5
      }).addTo(mapRef.current);
      providerLayerRef.current.push(radiusLayer);
    }

    const bounds = buildBoundsFromData(leaflet, data);

    data.pins.forEach((pin) => {
      const icon = leaflet.divIcon({
        className: 'provider-app-map-pin',
        html: `<div style="width:14px;height:14px;border-radius:9999px;border:2px solid #ffffff;background:${buildPinColor(pin.isWithinInterestRadius)};box-shadow:0 0 0 2px rgba(13,110,253,.25);"></div>`,
        iconSize: [14, 14],
        iconAnchor: [7, 7]
      });

      const marker = leaflet.marker([pin.latitude, pin.longitude], { icon });
      const popupHtml = `
        <div style="min-width:190px;max-width:240px;">
          <div style="font-weight:700;font-size:13px;margin-bottom:4px;">${escapeHtml(pin.category || 'Servico')}</div>
          <div style="font-size:12px;color:#344054;margin-bottom:6px;">${escapeHtml(pin.description || 'Sem descricao')}</div>
          <div style="font-size:11px;color:#667085;margin-bottom:8px;">${escapeHtml(pin.street || '')}, ${escapeHtml(pin.city || '')}</div>
          <div style="display:flex;justify-content:space-between;align-items:center;gap:8px;">
            <span style="font-size:11px;color:#667085;">${escapeHtml(formatDistance(pin.distanceKm))}</span>
            <button type="button" data-provider-request-id="${escapeHtml(pin.requestId)}" style="border:none;background:#0D6EFD;color:#fff;padding:5px 10px;border-radius:10px;font-size:11px;font-weight:700;cursor:pointer;">
              Abrir pedido
            </button>
          </div>
        </div>`;

      marker.bindPopup(popupHtml);
      marker.addTo(markersLayerRef.current);
    });

    if (bounds?.isValid()) {
      lastBoundsRef.current = bounds;
      fitToLastBounds();
      window.setTimeout(() => {
        fitToLastBounds();
      }, 120);
    } else {
      lastBoundsRef.current = null;
    }
  }, [canRenderMap, data, fitToLastBounds, buildBoundsFromData]);

  useEffect(() => {
    const container = mapContainerRef.current;
    if (!container) {
      return undefined;
    }

    const clickHandler = (event: Event) => {
      const target = event.target as HTMLElement | null;
      const button = target?.closest('[data-provider-request-id]') as HTMLElement | null;
      if (!button) {
        return;
      }

      const requestId = String(button.getAttribute('data-provider-request-id') || '').trim();
      if (!requestId) {
        return;
      }

      onOpenRequestById(requestId);
    };

    container.addEventListener('click', clickHandler);
    return () => container.removeEventListener('click', clickHandler);
  }, [onOpenRequestById]);

  useEffect(() => {
    return () => {
      try {
        mapRef.current?.remove();
      } catch {
        // noop
      }
      mapRef.current = null;
      markersLayerRef.current = null;
      providerLayerRef.current = [];
      lastBoundsRef.current = null;
    };
  }, []);

  return (
    <section className="rounded-2xl bg-white border border-[#e4e7ec] shadow-sm p-4 mb-4">
      <div className="flex items-center justify-between mb-3 gap-2">
        <h2 className="font-bold text-[#101828]">Mapa de cobertura</h2>
        <button
          type="button"
          onClick={() => void onRefresh()}
          className="text-xs font-semibold text-primary"
        >
          Atualizar mapa
        </button>
      </div>

      {error ? (
        <div className="mb-3 rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-red-700 text-sm">{error}</div>
      ) : null}

      {!canRenderMap && loading ? (
        <p className="text-sm text-[#667085]">Atualizando mapa...</p>
      ) : !canRenderMap ? (
        <p className="text-sm text-[#667085]">
          Defina CEP/base e coordenadas no perfil para visualizar o mapa de cobertura.
        </p>
      ) : !window.L ? (
        <p className="text-sm text-[#667085]">Mapa indisponivel no momento.</p>
      ) : (
        <>
          <div
            ref={mapContainerRef}
            className={`${mapHeightClassName || 'h-64'} relative w-full rounded-xl border border-[#e4e7ec] overflow-hidden`}
          >
            {loading ? (
              <div className="absolute inset-0 z-[500] bg-white/70 backdrop-blur-[1px] flex items-center justify-center text-xs font-semibold text-[#344054]">
                Atualizando mapa...
              </div>
            ) : null}
          </div>
          <div className="mt-3 flex flex-wrap gap-2 text-[11px] text-[#667085]">
            <span className="rounded-full bg-[#f2f4f7] px-2 py-1">Pins: {data?.pins.length ?? 0}/{data?.totalPins ?? 0}</span>
            <span className="rounded-full bg-[#f2f4f7] px-2 py-1">Raio base: {formatDistance(data?.interestRadiusKm)}</span>
            <span className="rounded-full bg-[#f2f4f7] px-2 py-1">Busca: {formatDistance(data?.mapSearchRadiusKm)}</span>
          </div>
          {showExpandButton ? (
            <button
              type="button"
              onClick={onExpand}
              className="mt-3 w-full rounded-xl border border-[#d0d5dd] px-3 py-2 text-sm font-semibold text-[#344054] hover:bg-[#f8fafc] transition-colors"
            >
              Expandir
            </button>
          ) : null}
        </>
      )}
    </section>
  );
};

export default CoverageMap;
