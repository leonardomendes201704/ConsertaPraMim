import React from 'react';
import { OrderProposalDetailsData, ProposalScheduleSlot } from '../types';

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
  scheduleDate?: string;
  scheduleReason?: string;
  onScheduleDateChange?: (value: string) => void;
  onScheduleReasonChange?: (value: string) => void;
  onLoadScheduleSlots?: () => void;
  availableSlots?: ProposalScheduleSlot[];
  hasSearchedSlots?: boolean;
  isLoadingSlots?: boolean;
  slotsErrorMessage?: string;
  onScheduleSlot?: (slot: ProposalScheduleSlot) => void;
  schedulingSlotStartUtc?: string | null;
  scheduleSuccessMessage?: string;
  scheduleErrorMessage?: string;
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
  scheduleDate = '',
  scheduleReason = '',
  onScheduleDateChange,
  onScheduleReasonChange,
  onLoadScheduleSlots,
  availableSlots = [],
  hasSearchedSlots = false,
  isLoadingSlots = false,
  slotsErrorMessage,
  onScheduleSlot,
  schedulingSlotStartUtc = null,
  scheduleSuccessMessage,
  scheduleErrorMessage,
  onBack
}) => {
  const [slotPendingConfirmation, setSlotPendingConfirmation] = React.useState<ProposalScheduleSlot | null>(null);
  const canAcceptProposal = !!details && !details.proposal.accepted && !details.proposal.invalidated;
  const canSchedule = !!details && details.proposal.accepted && !details.proposal.invalidated;
  const currentAppointment = details?.currentAppointment;

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

        {scheduleSuccessMessage ? (
          <div className="mb-4 rounded-xl border border-green-200 bg-green-50 px-3 py-2">
            <p className="text-xs font-semibold text-green-800">{scheduleSuccessMessage}</p>
          </div>
        ) : null}

        {scheduleErrorMessage ? (
          <div className="mb-4 rounded-xl border border-red-200 bg-red-50 px-3 py-2">
            <p className="text-xs font-semibold text-red-800">{scheduleErrorMessage}</p>
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

              {details.proposal.accepted && currentAppointment ? (
                <div className="rounded-xl border border-amber-200 bg-amber-50 px-3 py-3">
                  <p className="text-xs font-bold text-amber-800">Agendamento solicitado</p>
                  <p className="text-sm font-semibold text-amber-900">{currentAppointment.windowLabel}</p>
                  <p className="text-xs text-amber-800 mt-1">
                    Solicitacao enviada ao prestador. Aguarde a confirmacao da visita.
                  </p>
                </div>
              ) : null}
            </div>

            {currentAppointment ? null : (
              <div className="rounded-2xl border border-primary/10 bg-white p-4 shadow-sm space-y-3">
                <div className="flex items-center justify-between gap-2">
                  <h4 className="text-xs font-bold text-[#5e8d8d] uppercase tracking-wide">Agendamento do servico</h4>
                </div>

                {canSchedule ? (
                  <>
                    <div className="grid grid-cols-1 gap-3">
                      <div>
                        <label className="text-xs font-bold text-[#5e8d8d] uppercase tracking-wide">Data</label>
                        <input
                          type="date"
                          value={scheduleDate}
                          onChange={(event) => onScheduleDateChange?.(event.target.value)}
                          className="mt-1 h-11 w-full rounded-xl border border-primary/20 px-3 text-sm text-[#101818] outline-none focus:border-primary"
                        />
                      </div>
                      <div>
                        <label className="text-xs font-bold text-[#5e8d8d] uppercase tracking-wide">Observacao (opcional)</label>
                        <input
                          type="text"
                          value={scheduleReason}
                          onChange={(event) => onScheduleReasonChange?.(event.target.value)}
                          maxLength={250}
                          placeholder="Ex.: Portao destrancado apos 14h"
                          className="mt-1 h-11 w-full rounded-xl border border-primary/20 px-3 text-sm text-[#101818] outline-none focus:border-primary"
                        />
                      </div>
                    </div>

                    <button
                      type="button"
                      onClick={onLoadScheduleSlots}
                      disabled={isLoadingSlots}
                      className="w-full h-11 inline-flex items-center justify-center gap-2 rounded-xl border border-primary text-primary text-sm font-bold disabled:opacity-60 disabled:cursor-not-allowed"
                    >
                      {isLoadingSlots ? (
                        <>
                          <span className="material-symbols-outlined text-base animate-spin">progress_activity</span>
                          Buscando horarios...
                        </>
                      ) : (
                        <>
                          <span className="material-symbols-outlined text-base">schedule</span>
                          Buscar horarios disponiveis
                        </>
                      )}
                    </button>

                    {slotsErrorMessage ? (
                      <div className="rounded-xl border border-amber-200 bg-amber-50 px-3 py-2">
                        <p className="text-xs font-semibold text-amber-800">{slotsErrorMessage}</p>
                      </div>
                    ) : null}

                    {!isLoadingSlots && availableSlots.length > 0 ? (
                      <div className="flex flex-wrap gap-2">
                        {availableSlots.map((slot) => {
                          const isSubmittingThisSlot = schedulingSlotStartUtc === slot.windowStartUtc;
                          return (
                            <button
                              key={`${slot.windowStartUtc}-${slot.windowEndUtc}`}
                              type="button"
                              onClick={() => setSlotPendingConfirmation(slot)}
                              disabled={!!schedulingSlotStartUtc}
                              className="inline-flex items-center rounded-full border border-green-600 text-green-700 px-3 py-1.5 text-xs font-semibold disabled:opacity-60 disabled:cursor-not-allowed"
                            >
                              {isSubmittingThisSlot ? 'Solicitando...' : slot.label}
                            </button>
                          );
                        })}
                      </div>
                    ) : null}

                    {!isLoadingSlots && hasSearchedSlots && availableSlots.length === 0 && !slotsErrorMessage ? (
                      <div className="rounded-xl border border-primary/10 bg-[#f7fbfb] px-3 py-2">
                        <p className="text-xs font-semibold text-[#5e8d8d]">Nenhum horario disponivel para a data selecionada.</p>
                      </div>
                    ) : null}
                  </>
                ) : (
                  <div className="rounded-xl border border-blue-200 bg-blue-50 px-3 py-2">
                    <p className="text-xs font-semibold text-blue-800">
                      Aceite a proposta para liberar a solicitacao de agendamento com este prestador.
                    </p>
                  </div>
                )}
              </div>
            )}
          </div>
        ) : (
          <div className="rounded-2xl border border-primary/10 bg-white p-4">
            <p className="text-sm text-[#5e8d8d]">Nenhum detalhe de proposta disponivel.</p>
          </div>
        )}
      </div>

      {slotPendingConfirmation ? (
        <div className="fixed inset-0 z-[80] flex items-end justify-center bg-black/40 p-4">
          <div className="w-full max-w-md rounded-2xl border border-primary/10 bg-white p-4 shadow-2xl space-y-3">
            <h4 className="text-sm font-bold text-[#101818]">Confirmar agendamento</h4>
            <p className="text-sm text-[#3f4f4f]">
              Deseja solicitar o agendamento para:
            </p>
            <div className="rounded-xl border border-primary/10 bg-primary/5 px-3 py-2">
              <p className="text-sm font-semibold text-[#204646]">{slotPendingConfirmation.label}</p>
            </div>
            <div className="grid grid-cols-2 gap-2 pt-1">
              <button
                type="button"
                onClick={() => setSlotPendingConfirmation(null)}
                disabled={!!schedulingSlotStartUtc}
                className="h-11 rounded-xl border border-primary/20 text-primary text-sm font-bold disabled:opacity-60"
              >
                Cancelar
              </button>
              <button
                type="button"
                onClick={() => {
                  onScheduleSlot?.(slotPendingConfirmation);
                  setSlotPendingConfirmation(null);
                }}
                disabled={!!schedulingSlotStartUtc}
                className="h-11 rounded-xl bg-primary text-white text-sm font-bold disabled:opacity-60"
              >
                Confirmar
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
};

export default ProposalDetails;
