import React from 'react';
import type { FireTvHeatmapCell } from '../types';

interface HeatmapGridProps {
  rows: number;
  columns: number;
  cells: FireTvHeatmapCell[];
}

const HeatmapGrid: React.FC<HeatmapGridProps> = ({ rows, columns, cells }) => {
  if (!rows || !columns) {
    return <p className="tv-empty-state">Heatmap desativado nesta configuracao.</p>;
  }

  const maxHits = cells.reduce((highest, item) => Math.max(highest, item.hits), 0);
  const cellMap = new Map(cells.map((item) => [`${item.row}:${item.column}`, item.hits]));

  return (
    <div className="tv-heatmap-grid" style={{ gridTemplateColumns: `repeat(${columns}, minmax(0, 1fr))` }}>
      {Array.from({ length: rows * columns }, (_, index) => {
        const row = Math.floor(index / columns);
        const column = index % columns;
        const hits = cellMap.get(`${row}:${column}`) || 0;
        const intensity = maxHits > 0 ? Math.max(0.12, hits / maxHits) : 0.08;

        return (
          <div
            key={`${row}-${column}`}
            className="tv-heatmap-cell"
            style={{ backgroundColor: `rgba(38, 99, 235, ${intensity})` }}
          >
            <span className="tv-heatmap-label">L{row + 1} / C{column + 1}</span>
            <strong>{hits}</strong>
          </div>
        );
      })}
    </div>
  );
};

export default HeatmapGrid;
