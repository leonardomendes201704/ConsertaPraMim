import React, { useMemo, useState } from 'react';
import { ProviderCreateProposalPayload, ProviderRequestDetailsData } from '../types';

interface Props {
  details: ProviderRequestDetailsData | null;
  loading: boolean;
  error: string;
  submitting: boolean;
  submitError: string;
  submitSuccess: string;
  onBack: () => void;
  onRefresh: () => Promise<void>;
  onSubmitProposal: (payload: ProviderCreateProposalPayload) => Promise<void>;
  onOpenChat: () => void;
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

const RequestDetails: React.FC<Props> = ({
  details,
  loading,
  error,
  submitting,
  submitError,
  submitSuccess,
  onBack,
  onRefresh,
  onSubmitProposal,
  onOpenChat
}) => {
  const [estimatedValueInput, setEstimatedValueInput] = useState('');
  const [message, setMessage] = useState('');

  const parsedEstimatedValue = useMemo(() => {
    if (!estimatedValueInput.trim()) {
      return undefined;
    }

    const normalized = estimatedValueInput.replace(/\./g, '').replace(',', '.');
    const value = Number(normalized);
    return Number.isFinite(value) ? value : undefined;
  }, [estimatedValueInput]);

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    await onSubmitProposal({
      estimatedValue: parsedEstimatedValue,
      message
    });
  };

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

      <main className="max-w-md mx-auto px-4 py-5 space-y-4">
        {error && <div className="rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{error}</div>}

        {loading ? (
          <div className="rounded-2xl bg-white border border-[#e4e7ec] p-4 text-sm text-[#667085]">Carregando detalhes...</div>
        ) : !details ? (
          <div className="rounded-2xl bg-white border border-[#e4e7ec] p-4 text-sm text-[#667085]">Pedido nao encontrado.</div>
        ) : (
          <>
            <section className="rounded-2xl bg-white border border-[#e4e7ec] p-4 shadow-sm">
              <div className="flex items-start gap-3">
                <span className="material-symbols-outlined text-primary">{details.request.categoryIcon}</span>
                <div>
                  <h1 className="text-lg font-bold text-[#101828]">{details.request.category}</h1>
                  <p className="text-sm text-[#475467] mt-1">{details.request.description}</p>
                  <p className="text-xs text-[#667085] mt-2">{details.request.street}, {details.request.city} - {details.request.zip}</p>
                  <p className="text-xs text-[#667085]">Pedido em {details.request.createdAtLabel}</p>
                </div>
              </div>
            </section>

            {details.existingProposal ? (
              <section className="rounded-2xl bg-emerald-50 border border-emerald-200 p-4">
                <h2 className="font-bold text-emerald-800">Proposta ja enviada</h2>
                <p className="text-sm text-emerald-700 mt-1">Status: {details.existingProposal.statusLabel}</p>
                <p className="text-sm text-emerald-700">Valor: {formatCurrency(details.existingProposal.estimatedValue)}</p>
                {details.existingProposal.message && (
                  <p className="text-sm text-emerald-700 mt-2">Mensagem: {details.existingProposal.message}</p>
                )}
                <button
                  type="button"
                  onClick={onOpenChat}
                  className="mt-3 w-full rounded-xl bg-primary text-white font-bold py-2.5"
                >
                  Conversar com cliente
                </button>
              </section>
            ) : (
              <section className="rounded-2xl bg-white border border-[#e4e7ec] p-4 shadow-sm">
                <h2 className="font-bold text-[#101828] mb-3">Enviar proposta</h2>

                {submitError && (
                  <div className="mb-3 rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{submitError}</div>
                )}
                {submitSuccess && (
                  <div className="mb-3 rounded-xl border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm text-emerald-700">{submitSuccess}</div>
                )}

                <form onSubmit={handleSubmit} className="space-y-3">
                  <div>
                    <label className="block text-sm font-semibold text-[#344054] mb-1">Valor estimado (opcional)</label>
                    <input
                      type="text"
                      inputMode="decimal"
                      value={estimatedValueInput}
                      onChange={(event) => setEstimatedValueInput(event.target.value)}
                      className="w-full rounded-xl border border-[#d0d5dd] px-3 py-2"
                      placeholder="Ex.: 150,00"
                    />
                  </div>

                  <div>
                    <label className="block text-sm font-semibold text-[#344054] mb-1">Mensagem ao cliente</label>
                    <textarea
                      value={message}
                      onChange={(event) => setMessage(event.target.value)}
                      rows={4}
                      className="w-full rounded-xl border border-[#d0d5dd] px-3 py-2"
                      placeholder="Explique seu atendimento, prazo e observacoes."
                    />
                  </div>

                  <button
                    type="submit"
                    disabled={submitting || !details.canSubmitProposal}
                    className="w-full rounded-xl bg-primary text-white font-bold py-3 disabled:opacity-60"
                  >
                    {submitting ? 'Enviando...' : 'Enviar proposta'}
                  </button>
                </form>
              </section>
            )}
          </>
        )}
      </main>
    </div>
  );
};

export default RequestDetails;
