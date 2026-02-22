import React, { useMemo } from 'react';
import type { AdminMonitoringOverviewData } from '../types';

interface MonitoringTimelineChartProps {
  overview: AdminMonitoringOverviewData | null;
  isLoading: boolean;
  errorMessage?: string;
}

interface TimelinePoint {
  bucketUtc: string;
  requests: number;
  errors: number;
  latencyP95Ms: number;
}

const CHART_WIDTH = 320;
const CHART_HEIGHT = 170;
const CHART_PADDING = {
  top: 10,
  right: 34,
  bottom: 24,
  left: 24
};

const GRID_STEPS = 4;

function sortBuckets(a: string, b: string): number {
  const timeA = Date.parse(a);
  const timeB = Date.parse(b);

  if (Number.isNaN(timeA) || Number.isNaN(timeB)) {
    return a.localeCompare(b);
  }

  return timeA - timeB;
}

function formatBucketLabel(value: string, includeDate: boolean): string {
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return '--:--';
  }

  if (includeDate) {
    return parsed.toLocaleString('pt-BR', {
      day: '2-digit',
      month: '2-digit',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  return parsed.toLocaleTimeString('pt-BR', {
    hour: '2-digit',
    minute: '2-digit'
  });
}

function buildPath(points: TimelinePoint[], toX: (index: number) => number, toY: (point: TimelinePoint) => number): string {
  if (points.length === 0) {
    return '';
  }

  return points
    .map((point, index) => {
      const command = index === 0 ? 'M' : 'L';
      return `${command} ${toX(index).toFixed(2)} ${toY(point).toFixed(2)}`;
    })
    .join(' ');
}

const MonitoringTimelineChart: React.FC<MonitoringTimelineChartProps> = ({
  overview,
  isLoading,
  errorMessage
}) => {
  const points = useMemo<TimelinePoint[]>(() => {
    if (!overview) {
      return [];
    }

    const requestsByBucket = new Map<string, number>();
    const errorsByBucket = new Map<string, number>();
    const latencyByBucket = new Map<string, number>();

    (overview.requestsSeries || []).forEach((item) => {
      requestsByBucket.set(item.bucketUtc, Number(item.value) || 0);
    });

    (overview.errorsSeries || []).forEach((item) => {
      errorsByBucket.set(item.bucketUtc, Number(item.value) || 0);
    });

    (overview.latencySeries || []).forEach((item) => {
      latencyByBucket.set(item.bucketUtc, Number(item.p95Ms) || 0);
    });

    const buckets = new Set<string>([
      ...requestsByBucket.keys(),
      ...errorsByBucket.keys(),
      ...latencyByBucket.keys()
    ]);

    return Array.from(buckets)
      .sort(sortBuckets)
      .map((bucketUtc) => ({
        bucketUtc,
        requests: requestsByBucket.get(bucketUtc) || 0,
        errors: errorsByBucket.get(bucketUtc) || 0,
        latencyP95Ms: latencyByBucket.get(bucketUtc) || 0
      }));
  }, [overview]);

  if (isLoading && points.length === 0) {
    return <div className="h-40 animate-pulse rounded-xl bg-slate-100" />;
  }

  if (points.length === 0) {
    return (
      <div className="rounded-xl border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
        {errorMessage || 'Sem serie temporal para o periodo atual.'}
      </div>
    );
  }

  const plotWidth = CHART_WIDTH - CHART_PADDING.left - CHART_PADDING.right;
  const plotHeight = CHART_HEIGHT - CHART_PADDING.top - CHART_PADDING.bottom;
  const maxCount = Math.max(1, ...points.map((point) => Math.max(point.requests, point.errors)));
  const maxLatency = Math.max(1, ...points.map((point) => point.latencyP95Ms));

  const firstBucket = points[0]?.bucketUtc || '';
  const lastBucket = points[points.length - 1]?.bucketUtc || '';
  const startTime = Date.parse(firstBucket);
  const endTime = Date.parse(lastBucket);
  const includeDate = !Number.isNaN(startTime) && !Number.isNaN(endTime) && endTime - startTime > 24 * 60 * 60 * 1000;

  const toX = (index: number): number => {
    if (points.length === 1) {
      return CHART_PADDING.left + plotWidth / 2;
    }

    return CHART_PADDING.left + (index / (points.length - 1)) * plotWidth;
  };

  const toCountY = (value: number): number => {
    const normalized = Math.max(0, Math.min(1, value / maxCount));
    return CHART_PADDING.top + (1 - normalized) * plotHeight;
  };

  const toLatencyY = (value: number): number => {
    const normalized = Math.max(0, Math.min(1, value / maxLatency));
    return CHART_PADDING.top + (1 - normalized) * plotHeight;
  };

  const requestsPath = buildPath(points, toX, (point) => toCountY(point.requests));
  const errorsPath = buildPath(points, toX, (point) => toCountY(point.errors));
  const latencyPath = buildPath(points, toX, (point) => toLatencyY(point.latencyP95Ms));

  const latest = points[points.length - 1];

  return (
    <div className="space-y-3">
      <div className="grid grid-cols-3 gap-2 text-xs">
        <div className="rounded-lg border border-blue-100 bg-blue-50 px-2 py-1.5 text-blue-700">
          <p className="font-semibold">Requests</p>
          <p>{latest.requests}</p>
        </div>
        <div className="rounded-lg border border-rose-100 bg-rose-50 px-2 py-1.5 text-rose-700">
          <p className="font-semibold">Erros</p>
          <p>{latest.errors}</p>
        </div>
        <div className="rounded-lg border border-amber-100 bg-amber-50 px-2 py-1.5 text-amber-700">
          <p className="font-semibold">P95</p>
          <p>{latest.latencyP95Ms} ms</p>
        </div>
      </div>

      <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
        <svg viewBox={`0 0 ${CHART_WIDTH} ${CHART_HEIGHT}`} className="h-44 w-full">
          {Array.from({ length: GRID_STEPS }).map((_, index) => {
            const y = CHART_PADDING.top + (index / (GRID_STEPS - 1)) * plotHeight;
            return (
              <line
                key={`grid-${index}`}
                x1={CHART_PADDING.left}
                y1={y}
                x2={CHART_WIDTH - CHART_PADDING.right}
                y2={y}
                stroke="#e2e8f0"
                strokeWidth="1"
              />
            );
          })}

          <path d={requestsPath} fill="none" stroke="#2563eb" strokeWidth="2.2" strokeLinecap="round" />
          <path d={errorsPath} fill="none" stroke="#e11d48" strokeWidth="2.2" strokeLinecap="round" />
          <path d={latencyPath} fill="none" stroke="#f59e0b" strokeWidth="2.2" strokeLinecap="round" />
        </svg>

        <div className="flex items-center justify-between border-t border-slate-100 px-3 py-2 text-[11px] text-slate-500">
          <span>{formatBucketLabel(firstBucket, includeDate)}</span>
          <span>{formatBucketLabel(lastBucket, includeDate)}</span>
        </div>
      </div>

      <div className="flex flex-wrap items-center gap-3 text-[11px] text-slate-600">
        <span className="inline-flex items-center gap-1">
          <span className="h-2.5 w-2.5 rounded-full bg-blue-600" />
          Requests (escala esquerda, max {maxCount})
        </span>
        <span className="inline-flex items-center gap-1">
          <span className="h-2.5 w-2.5 rounded-full bg-rose-600" />
          Erros (escala esquerda)
        </span>
        <span className="inline-flex items-center gap-1">
          <span className="h-2.5 w-2.5 rounded-full bg-amber-500" />
          Latencia P95 (escala direita, max {maxLatency} ms)
        </span>
      </div>

      {errorMessage ? (
        <p className="text-xs text-amber-700">
          Exibindo ultimo snapshot. Erro no refresh realtime: {errorMessage}
        </p>
      ) : null}
    </div>
  );
};

export default MonitoringTimelineChart;
