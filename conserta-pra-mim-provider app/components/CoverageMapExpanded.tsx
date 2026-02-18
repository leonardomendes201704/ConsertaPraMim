import React from 'react';
import CoverageMap from './CoverageMap';
import { ProviderCoverageMapData } from '../types';

interface Props {
  data: ProviderCoverageMapData | null;
  loading: boolean;
  error: string;
  onBack: () => void;
  onRefresh: () => Promise<void>;
  onOpenRequestById: (requestId: string) => void;
}

const CoverageMapExpanded: React.FC<Props> = ({
  data,
  loading,
  error,
  onBack,
  onRefresh,
  onOpenRequestById
}) => {
  return (
    <div className="min-h-screen bg-[#f4f7fb]">
      <header className="bg-white border-b border-[#e4e7ec] sticky top-0 z-10">
        <div className="max-w-md mx-auto px-4 py-4 flex items-center justify-between">
          <button
            type="button"
            onClick={onBack}
            className="rounded-xl border border-[#d0d5dd] px-3 py-2 text-sm font-semibold text-[#344054]"
          >
            Voltar
          </button>
          <h1 className="text-base font-bold text-[#101828]">Mapa expandido</h1>
          <button
            type="button"
            onClick={() => void onRefresh()}
            className="rounded-xl border border-[#d0d5dd] px-3 py-2 text-sm font-semibold text-[#344054]"
          >
            Atualizar
          </button>
        </div>
      </header>

      <main className="max-w-md mx-auto px-4 py-4">
        <CoverageMap
          data={data}
          loading={loading}
          error={error}
          onRefresh={onRefresh}
          onOpenRequestById={onOpenRequestById}
          mapHeightClassName="h-[calc(100vh-220px)]"
        />
      </main>
    </div>
  );
};

export default CoverageMapExpanded;
