import React from 'react';
import { ServiceRequest, ServiceRequestDetailsData } from '../types';

interface Props {
  request: ServiceRequest;
  details?: ServiceRequestDetailsData | null;
  isLoadingDetails?: boolean;
  detailsError?: string;
  onRetryDetails?: () => void;
  onBack: () => void;
  onOpenChat: () => void;
  onOpenProposalDetails?: (proposalId: string) => void;
  onFinishService?: () => void;
}

function getStatusMeta(status: ServiceRequest['status']): { title: string; subtitle: string; icon: string; cardClass: string; iconClass: string } {
  if (status === 'EM_ANDAMENTO') {
    return {
      title: 'Servico em andamento',
      subtitle: 'O prestador esta executando o atendimento.',
      icon: 'build',
      cardClass: 'bg-orange-50 border-orange-100',
      iconClass: 'text-orange-500'
    };
  }

  if (status === 'CONCLUIDO') {
    return {
      title: 'Servico concluido',
      subtitle: 'Atendimento finalizado com sucesso.',
      icon: 'check_circle',
      cardClass: 'bg-green-50 border-green-100',
      iconClass: 'text-green-500'
    };
  }

  if (status === 'CANCELADO') {
    return {
      title: 'Pedido cancelado',
      subtitle: 'Este pedido nao segue mais no fluxo operacional.',
      icon: 'cancel',
      cardClass: 'bg-red-50 border-red-100',
      iconClass: 'text-red-500'
    };
  }

  return {
    title: 'Aguardando andamento',
    subtitle: 'Seu pedido esta em fase de atendimento inicial.',
    icon: 'schedule',
    cardClass: 'bg-blue-50 border-blue-100',
    iconClass: 'text-blue-500'
  };
}

function resolveTimelineIcon(eventCode: string): string {
  const code = eventCode.toLowerCase();
  if (code.includes('created')) return 'add_circle';
  if (code.includes('proposal')) return 'person_search';
  if (code.includes('confirmed')) return 'event_available';
  if (code.includes('reschedule')) return 'update';
  if (code.includes('inprogress')) return 'construction';
  if (code.includes('arrived')) return 'location_on';
  if (code.includes('cancel')) return 'cancel';
  if (code.includes('completed')) return 'task_alt';
  if (code.includes('review')) return 'star';
  return 'history';
}

const RequestDetails: React.FC<Props> = ({
  request,
  details,
  isLoadingDetails = false,
  detailsError,
  onRetryDetails,
  onBack,
  onOpenChat,
  onOpenProposalDetails,
  onFinishService
}) => {
  const statusMeta = getStatusMeta(request.status);
  const flowSteps = details?.flowSteps || [];
  const timeline = details?.timeline || [];

  return (
    <div className="flex flex-col h-screen bg-background-light overflow-hidden">
      <header className="flex items-center bg-white p-4 border-b border-primary/10 sticky top-0 z-20">
        <button onClick={onBack} className="p-2 -ml-2 text-primary hover:bg-primary/5 rounded-full transition-colors">
          <span className="material-symbols-outlined">arrow_back</span>
        </button>
        <div className="flex-1 text-center pr-8">
          <h2 className="text-[#101818] text-base font-bold">Pedido #{request.id}</h2>
          <p className="text-xs text-primary/60 font-medium">{request.category}</p>
        </div>
      </header>

      <div className="flex-1 overflow-y-auto no-scrollbar pb-32">
        <div className="px-4 py-6 bg-white border-b border-primary/5">
          <div className={`flex flex-col items-center justify-center p-6 rounded-2xl border ${statusMeta.cardClass}`}>
            <span className={`material-symbols-outlined text-4xl mb-2 ${statusMeta.iconClass}`}>{statusMeta.icon}</span>
            <h3 className="text-lg font-bold text-[#101818]">{statusMeta.title}</h3>
            <p className="text-sm text-[#5e8d8d] font-medium text-center mt-1">{statusMeta.subtitle}</p>
          </div>
        </div>

        {request.description ? (
          <div className="mx-4 mt-4 p-4 bg-white rounded-2xl border border-primary/5 shadow-sm">
            <h4 className="text-xs font-bold text-primary uppercase tracking-wider mb-2">Descricao do pedido</h4>
            <p className="text-sm text-[#3f4f4f]">{request.description}</p>
          </div>
        ) : null}

        <div className="mx-4 mt-4 p-4 bg-white rounded-2xl border border-primary/5 shadow-sm">
          <div className="flex items-center justify-between mb-3">
            <h4 className="text-xs font-bold text-[#5e8d8d] uppercase">Fluxo do pedido</h4>
            <button
              type="button"
              onClick={onOpenChat}
              className="inline-flex items-center gap-1 rounded-lg bg-primary/10 text-primary px-3 py-1 text-xs font-bold"
            >
              <span className="material-symbols-outlined text-sm">chat</span>
              Chat
            </button>
          </div>

          {isLoadingDetails ? (
            <div className="py-4 text-center">
              <span className="material-symbols-outlined text-3xl text-primary animate-spin">progress_activity</span>
              <p className="text-sm text-[#5e8d8d] mt-2">Atualizando acompanhamento...</p>
            </div>
          ) : detailsError ? (
            <div className="rounded-xl border border-amber-200 bg-amber-50 p-3">
              <p className="text-sm text-amber-900">{detailsError}</p>
              {onRetryDetails ? (
                <button
                  type="button"
                  onClick={onRetryDetails}
                  className="mt-2 inline-flex h-9 px-3 items-center justify-center rounded-lg bg-primary text-white text-sm font-bold"
                >
                  Recarregar historico
                </button>
              ) : null}
            </div>
          ) : flowSteps.length > 0 ? (
            <div className="space-y-2">
              {flowSteps.map((step) => (
                <div
                  key={step.step}
                  className={`flex items-center gap-3 rounded-xl px-3 py-2 border ${
                    step.current
                      ? 'border-primary bg-primary/5'
                      : step.completed
                      ? 'border-green-200 bg-green-50'
                      : 'border-primary/10 bg-white'
                  }`}
                >
                  <div
                    className={`size-6 rounded-full flex items-center justify-center text-xs font-bold ${
                      step.current
                        ? 'bg-primary text-white'
                        : step.completed
                        ? 'bg-green-500 text-white'
                        : 'bg-[#eef4f4] text-[#5e8d8d]'
                    }`}
                  >
                    {step.step}
                  </div>
                  <p className={`text-sm font-semibold ${step.current ? 'text-primary' : 'text-[#101818]'}`}>{step.title}</p>
                </div>
              ))}
            </div>
          ) : (
            <p className="text-sm text-[#5e8d8d]">Sem fluxo detalhado para este pedido.</p>
          )}
        </div>

        <div className="mx-4 mt-4 mb-6 p-4 bg-white rounded-2xl border border-primary/5 shadow-sm">
          <h4 className="text-xs font-bold text-[#5e8d8d] uppercase mb-4">Acompanhamento / Historico</h4>
          {isLoadingDetails ? (
            <p className="text-sm text-[#5e8d8d]">Carregando eventos...</p>
          ) : timeline.length === 0 ? (
            <p className="text-sm text-[#5e8d8d]">Ainda nao ha eventos historicos para este pedido.</p>
          ) : (
            <div className="space-y-4 relative before:absolute before:left-[11px] before:top-1 before:bottom-1 before:w-[2px] before:bg-primary/10">
              {timeline.map((event) => (
                (() => {
                  const isProposalLink =
                    event.relatedEntityType?.toLowerCase() === 'proposal' &&
                    !!event.relatedEntityId &&
                    !!onOpenProposalDetails;

                  const content = (
                    <>
                      <div className="size-6 rounded-full bg-primary text-white flex items-center justify-center">
                        <span className="material-symbols-outlined text-xs">{resolveTimelineIcon(event.eventCode)}</span>
                      </div>
                      <div className="flex-1 text-left">
                        <div className="flex justify-between gap-3">
                          <p className="text-sm font-bold text-[#101818]">{event.title}</p>
                          <span className="text-[11px] text-[#5e8d8d] font-semibold whitespace-nowrap">{event.occurredAt}</span>
                        </div>
                        <p className="text-xs text-[#5e8d8d] mt-1">{event.description}</p>
                        {isProposalLink ? (
                          <p className="text-[11px] text-primary font-semibold mt-1">Toque para ver detalhes da proposta</p>
                        ) : null}
                      </div>
                      {isProposalLink ? (
                        <span className="material-symbols-outlined text-primary text-sm">chevron_right</span>
                      ) : null}
                    </>
                  );

                  if (isProposalLink) {
                    return (
                      <button
                        key={`${event.eventCode}-${event.occurredAt}-${event.relatedEntityId}`}
                        type="button"
                        onClick={() => onOpenProposalDetails?.(event.relatedEntityId!)}
                        className="w-full flex items-start gap-3 relative z-10 rounded-xl px-1 py-1 hover:bg-primary/5 active:scale-[0.99] transition-all"
                      >
                        {content}
                      </button>
                    );
                  }

                  return (
                    <div key={`${event.eventCode}-${event.occurredAt}`} className="flex items-start gap-3 relative z-10">
                      {content}
                    </div>
                  );
                })()
              ))}
            </div>
          )}
        </div>
      </div>

      {request.status === 'EM_ANDAMENTO' && (
        <div className="fixed bottom-0 left-0 right-0 p-4 bg-white border-t border-primary/10 shadow-lg max-w-md mx-auto z-30 pb-10">
          <button
            onClick={onFinishService}
            className="w-full h-14 bg-primary text-white rounded-xl font-bold flex items-center justify-center gap-2 shadow-lg shadow-primary/20 active:scale-95 transition-all"
          >
            <span className="material-symbols-outlined">verified</span>
            Finalizar e Pagar
          </button>
        </div>
      )}
    </div>
  );
};

export default RequestDetails;
