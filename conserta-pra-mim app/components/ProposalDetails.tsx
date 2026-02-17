import React from 'react';
import { OrderProposalDetailsData } from '../types';

interface Props {
  details: OrderProposalDetailsData | null;
  isLoading?: boolean;
  errorMessage?: string;
  onRetry?: () => void;
  onOpenChatWithProvider?: () => void;
  onAcceptProposal?: () => void;
  isAcceptingProposal?: boolean;
  acceptSuccessMessage?: string;
  acceptErrorMessage?: string;
  onBack: () => void;
}

function formatCurrency(value?: number): string {
  if (typeof value !== 'number' || Number.isNaN(value)) {
    return 'Nao informado';
  }

  return new Intl.NumberFormat('pt-BR', {
    style: 'currency',
    currency: 'BRL'
  }).format(value);
}

function resolveStatusBadgeClasses(statusLabel: string): string {
  const normalized = statusLabel.trim().toLowerCase();
  if (normalized === 'aceita') {
    return 'bg-green-100 text-green-700 border-green-200';
  }

  if (normalized === 'invalidada') {
    return 'bg-red-100 text-red-700 border-red-200';
  }

  return 'bg-blue-100 text-blue-700 border-blue-200';
}

const ProposalDetails: React.FC<Props> = ({
  details,
  isLoading = false,
  errorMessage,
  onRetry,
  onOpenChatWithProvider,
  onAcceptProposal,
  isAcceptingProposal = false,
  acceptSuccessMessage,
  acceptErrorMessage,
  onBack
}) => {
  const canAcceptProposal = !!details && !details.proposal.accepted && !details.proposal.invalidated;

  return (
    <div className="flex flex-col h-screen bg-background-light overflow-hidden">
      <header className="flex items-center bg-white p-4 border-b border-primary/10 sticky top-0 z-20">
        <button onClick={onBack} className="p-2 -ml-2 text-primary hover:bg-primary/5 rounded-full transition-colors">
          <span className="material-symbols-outlined">arrow_back</span>
        </button>
        <div className="flex-1 text-center pr-8">
          <h2 className="text-[#101818] text-base font-bold">Detalhes da proposta</h2>
          <p className="text-xs text-primary/60 font-medium">Pedido #{details?.order.id || '-'}</p>
        </div>
      </header>

      <div className="flex-1 overflow-y-auto no-scrollbar px-4 py-5">
        {acceptSuccessMessage ? (
          <div className="mb-4 rounded-xl border border-green-200 bg-green-50 px-3 py-2">
            <p className="text-xs font-semibold text-green-800">{acceptSuccessMessage}</p>
          </div>
        ) : null}

        {acceptErrorMessage ? (
          <div className="mb-4 rounded-xl border border-red-200 bg-red-50 px-3 py-2">
            <p className="text-xs font-semibold text-red-800">{acceptErrorMessage}</p>
          </div>
        ) : null}

        {isLoading ? (
          <div className="rounded-2xl border border-primary/10 bg-white p-6 text-center">
            <span className="material-symbols-outlined text-3xl text-primary animate-spin">progress_activity</span>
            <p className="text-sm text-[#5e8d8d] mt-2">Carregando detalhes da proposta...</p>
          </div>
        ) : errorMessage ? (
          <div className="rounded-2xl border border-amber-200 bg-amber-50 p-4">
            <p className="text-sm text-amber-900">{errorMessage}</p>
            {onRetry ? (
              <button
                type="button"
                onClick={onRetry}
                className="mt-3 inline-flex h-10 px-4 items-center justify-center rounded-xl bg-primary text-white text-sm font-bold"
              >
                Tentar novamente
              </button>
            ) : null}
          </div>
        ) : details ? (
          <div className="space-y-4">
            <div className="rounded-2xl border border-primary/10 bg-white p-4 shadow-sm">
              <div className="flex items-center justify-between gap-3">
                <h3 className="text-base font-bold text-[#101818]">{details.order.category}</h3>
                <span className={`inline-flex items-center rounded-full border px-2.5 py-1 text-[11px] font-bold ${resolveStatusBadgeClasses(details.proposal.statusLabel)}`}>
                  {details.proposal.statusLabel}
                </span>
              </div>
              <p className="text-xs text-[#5e8d8d] mt-1">Proposta enviada em {details.proposal.sentAt}</p>
            </div>

            <div className="rounded-2xl border border-primary/10 bg-white p-4 shadow-sm space-y-3">
              <h4 className="text-xs font-bold text-[#5e8d8d] uppercase tracking-wide">Prestador</h4>
              <p className="text-sm font-semibold text-[#101818]">{details.proposal.providerName}</p>

              <h4 className="text-xs font-bold text-[#5e8d8d] uppercase tracking-wide">Valor estimado</h4>
              <p className="text-2xl font-bold text-primary">{formatCurrency(details.proposal.estimatedValue)}</p>

              <h4 className="text-xs font-bold text-[#5e8d8d] uppercase tracking-wide">Mensagem da proposta</h4>
              <p className="text-sm text-[#3f4f4f]">
                {details.proposal.message || 'O prestador nao enviou mensagem adicional nesta proposta.'}
              </p>
            </div>

            <div className="rounded-2xl border border-primary/10 bg-white p-4 shadow-sm space-y-3">
              <button
                type="button"
                onClick={onOpenChatWithProvider}
                className="w-full h-12 inline-flex items-center justify-center gap-2 rounded-xl border border-primary/20 bg-primary/5 text-primary text-sm font-bold active:scale-[0.99] transition-all"
              >
                <span className="material-symbols-outlined text-base">chat</span>
                Conversar com o prestador
              </button>

              <button
                type="button"
                onClick={onAcceptProposal}
                disabled={!canAcceptProposal || isAcceptingProposal}
                className="w-full h-12 inline-flex items-center justify-center gap-2 rounded-xl bg-primary text-white text-sm font-bold shadow-lg shadow-primary/20 disabled:opacity-60 disabled:cursor-not-allowed active:scale-[0.99] transition-all"
              >
                {isAcceptingProposal ? (
                  <>
                    <span className="material-symbols-outlined text-base animate-spin">progress_activity</span>
                    Aceitando proposta...
                  </>
                ) : canAcceptProposal ? (
                  <>
                    <span className="material-symbols-outlined text-base">verified</span>
                    Aceitar proposta
                  </>
                ) : details.proposal.accepted ? (
                  <>
                    <span className="material-symbols-outlined text-base">task_alt</span>
                    Proposta ja aceita
                  </>
                ) : (
                  <>
                    <span className="material-symbols-outlined text-base">block</span>
                    Proposta indisponivel
                  </>
                )}
              </button>

              <p className="text-[11px] text-[#5e8d8d]">
                Ao aceitar esta proposta, o pedido avanca para a etapa de agendamento com este prestador.
              </p>
            </div>
          </div>
        ) : (
          <div className="rounded-2xl border border-primary/10 bg-white p-4">
            <p className="text-sm text-[#5e8d8d]">Nenhum detalhe de proposta disponivel.</p>
          </div>
        )}
      </div>
    </div>
  );
};

export default ProposalDetails;
