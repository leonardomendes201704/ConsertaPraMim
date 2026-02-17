import React from 'react';
import { ProviderProposalsData } from '../types';

interface Props {
  proposals: ProviderProposalsData | null;
  loading: boolean;
  error: string;
  onBack: () => void;
  onRefresh: () => Promise<void>;
}

function formatCurrency(value?: number): string {
  if (!Number.isFinite(value)) {
    return 'A combinar';
  }

  return Number(value).toLocaleString('pt-BR', {
    style: 'currency',
    currency: 'BRL'
  });
}

const Proposals: React.FC<Props> = ({ proposals, loading, error, onBack, onRefresh }) => {
  return (
    <div className="min-h-screen bg-[#f4f7fb] pb-8">
      <header className="bg-white border-b border-[#e4e7ec] sticky top-0 z-10">
        <div className="max-w-md mx-auto px-4 py-4 flex items-center justify-between">
          <button type="button" onClick={onBack} className="text-sm font-semibold text-[#344054] flex items-center gap-1">
            <span className="material-symbols-outlined text-base">arrow_back</span>
            Voltar
          </button>
          <button type="button" onClick={() => void onRefresh()} className="text-sm font-semibold text-primary">Atualizar</button>
        </div>
      </header>

      <main className="max-w-md mx-auto px-4 py-5">
        {error && <div className="mb-4 rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{error}</div>}

        <section className="grid grid-cols-3 gap-2 mb-4">
          <Kpi label="Total" value={proposals?.totalCount ?? 0} />
          <Kpi label="Abertas" value={proposals?.openCount ?? 0} />
          <Kpi label="Aceitas" value={proposals?.acceptedCount ?? 0} />
        </section>

        <section className="rounded-2xl bg-white border border-[#e4e7ec] p-4 shadow-sm">
          <h1 className="font-bold text-[#101828] mb-3">Minhas propostas</h1>

          {loading ? (
            <p className="text-sm text-[#667085]">Carregando propostas...</p>
          ) : !proposals?.items.length ? (
            <p className="text-sm text-[#667085]">Voce ainda nao enviou propostas.</p>
          ) : (
            <div className="space-y-3">
              {proposals.items.map((proposal) => (
                <div key={proposal.id} className="rounded-xl border border-[#eaecf0] p-3 bg-[#fcfcfd]">
                  <div className="flex items-center justify-between gap-2">
                    <p className="text-xs font-semibold text-[#344054]">Pedido #{proposal.requestId.slice(0, 8)}</p>
                    <span className={`text-[10px] px-2 py-1 rounded-full font-bold uppercase ${
                      proposal.accepted ? 'bg-emerald-100 text-emerald-700' : 'bg-blue-100 text-blue-700'
                    }`}>
                      {proposal.statusLabel}
                    </span>
                  </div>
                  <p className="mt-2 text-sm font-semibold text-[#101828]">{formatCurrency(proposal.estimatedValue)}</p>
                  {proposal.message && (
                    <p className="text-xs text-[#667085] mt-1 line-clamp-2">{proposal.message}</p>
                  )}
                  <p className="text-[11px] text-[#98a2b3] mt-2">{proposal.createdAtLabel}</p>
                </div>
              ))}
            </div>
          )}
        </section>
      </main>
    </div>
  );
};

const Kpi: React.FC<{ label: string; value: number }> = ({ label, value }) => (
  <div className="rounded-xl bg-white border border-[#e4e7ec] py-2 px-3 text-center">
    <p className="text-[11px] uppercase text-[#667085] font-semibold tracking-wide">{label}</p>
    <p className="text-lg font-bold text-[#101828]">{value}</p>
  </div>
);

export default Proposals;
