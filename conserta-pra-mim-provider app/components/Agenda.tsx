import React from 'react';
import { ProviderAgendaData, ProviderAgendaItem } from '../types';

interface Props {
  agenda: ProviderAgendaData | null;
  loading: boolean;
  error: string;
  actionLoadingKey: string | null;
  onBack: () => void;
  onRefresh: () => Promise<void>;
  onOpenRequest: (requestId: string) => void;
  onConfirm: (appointmentId: string) => Promise<void>;
  onReject: (appointmentId: string, reason: string) => Promise<void>;
  onRespondReschedule: (appointmentId: string, accept: boolean, reason?: string) => Promise<void>;
}

const Agenda: React.FC<Props> = ({
  agenda,
  loading,
  error,
  actionLoadingKey,
  onBack,
  onRefresh,
  onOpenRequest,
  onConfirm,
  onReject,
  onRespondReschedule
}) => {
  const handleReject = async (item: ProviderAgendaItem) => {
    const reason = window.prompt('Informe o motivo da recusa:')?.trim();
    if (!reason) {
      return;
    }

    await onReject(item.appointmentId, reason);
  };

  const handleRejectReschedule = async (item: ProviderAgendaItem) => {
    const reason = window.prompt('Informe o motivo da recusa do reagendamento (opcional):')?.trim();
    await onRespondReschedule(item.appointmentId, false, reason || undefined);
  };

  const isLoadingAction = (item: ProviderAgendaItem): boolean => {
    if (!actionLoadingKey) {
      return false;
    }

    return actionLoadingKey.startsWith(`${item.appointmentId}:`);
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

      <main className="max-w-md mx-auto px-4 py-5">
        {error && <div className="mb-4 rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{error}</div>}

        <section className="grid grid-cols-2 gap-2 mb-4">
          <KpiCard title="Pendencias" value={agenda?.pendingCount ?? 0} />
          <KpiCard title="Proximas visitas" value={agenda?.upcomingCount ?? 0} />
        </section>

        <section className="rounded-2xl bg-white border border-[#e4e7ec] p-4 shadow-sm mb-4">
          <h1 className="font-bold text-[#101828] mb-3">Aguardando sua acao</h1>
          {loading ? (
            <p className="text-sm text-[#667085]">Carregando agenda...</p>
          ) : !agenda?.pendingItems.length ? (
            <p className="text-sm text-[#667085]">Sem pendencias de confirmacao ou reagendamento.</p>
          ) : (
            <div className="space-y-3">
              {agenda.pendingItems.map((item) => (
                <AgendaCard
                  key={item.appointmentId}
                  item={item}
                  loading={isLoadingAction(item)}
                  onOpenRequest={onOpenRequest}
                  onConfirm={onConfirm}
                  onReject={handleReject}
                  onAcceptReschedule={(agendaItem) => onRespondReschedule(agendaItem.appointmentId, true)}
                  onRejectReschedule={handleRejectReschedule}
                />
              ))}
            </div>
          )}
        </section>

        <section className="rounded-2xl bg-white border border-[#e4e7ec] p-4 shadow-sm">
          <h2 className="font-bold text-[#101828] mb-3">Proximas visitas</h2>
          {loading ? (
            <p className="text-sm text-[#667085]">Carregando agenda...</p>
          ) : !agenda?.upcomingItems.length ? (
            <p className="text-sm text-[#667085]">Sem visitas confirmadas no periodo atual.</p>
          ) : (
            <div className="space-y-3">
              {agenda.upcomingItems.map((item) => (
                <div key={item.appointmentId} className="rounded-xl border border-[#eaecf0] p-3 bg-[#fcfcfd]">
                  <p className="text-sm font-semibold text-[#101828]">{item.category || 'Servico'}</p>
                  <p className="text-xs text-[#475467] mt-1">{item.clientName || 'Cliente'} - {item.windowLabel}</p>
                  <p className="text-xs text-[#667085] mt-1">{item.street}, {item.city}</p>
                  <div className="mt-3 flex items-center justify-between gap-2">
                    <span className="text-[11px] px-2 py-1 rounded-full bg-blue-100 text-blue-700 font-bold uppercase">
                      {item.appointmentStatusLabel}
                    </span>
                    <button
                      type="button"
                      onClick={() => onOpenRequest(item.serviceRequestId)}
                      className="text-xs font-semibold text-primary"
                    >
                      Ver pedido
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </section>
      </main>
    </div>
  );
};

const AgendaCard: React.FC<{
  item: ProviderAgendaItem;
  loading: boolean;
  onOpenRequest: (requestId: string) => void;
  onConfirm: (item: ProviderAgendaItem) => Promise<void>;
  onReject: (item: ProviderAgendaItem) => Promise<void>;
  onAcceptReschedule: (item: ProviderAgendaItem) => Promise<void>;
  onRejectReschedule: (item: ProviderAgendaItem) => Promise<void>;
}> = ({
  item,
  loading,
  onOpenRequest,
  onConfirm,
  onReject,
  onAcceptReschedule,
  onRejectReschedule
}) => {
  return (
    <div className="rounded-xl border border-[#eaecf0] p-3 bg-[#fcfcfd]">
      <div className="flex items-start justify-between gap-2">
        <div>
          <p className="text-sm font-semibold text-[#101828]">{item.category || 'Servico'}</p>
          <p className="text-xs text-[#475467] mt-1">{item.clientName || 'Cliente'} - {item.windowLabel}</p>
          <p className="text-xs text-[#667085] mt-1">{item.street}, {item.city}</p>
        </div>
        <span className="text-[10px] px-2 py-1 rounded-full bg-amber-100 text-amber-700 font-bold uppercase">
          {item.appointmentStatusLabel}
        </span>
      </div>

      <div className="mt-3 flex flex-wrap gap-2">
        {item.canConfirm && (
          <button
            type="button"
            disabled={loading}
            onClick={() => void onConfirm(item)}
            className="rounded-lg bg-emerald-600 text-white text-xs font-semibold px-3 py-2 disabled:opacity-60"
          >
            Confirmar
          </button>
        )}

        {item.canReject && (
          <button
            type="button"
            disabled={loading}
            onClick={() => void onReject(item)}
            className="rounded-lg border border-red-300 text-red-700 text-xs font-semibold px-3 py-2 disabled:opacity-60"
          >
            Recusar
          </button>
        )}

        {item.canRespondReschedule && (
          <button
            type="button"
            disabled={loading}
            onClick={() => void onAcceptReschedule(item)}
            className="rounded-lg bg-primary text-white text-xs font-semibold px-3 py-2 disabled:opacity-60"
          >
            Aceitar reagendamento
          </button>
        )}

        {item.canRespondReschedule && (
          <button
            type="button"
            disabled={loading}
            onClick={() => void onRejectReschedule(item)}
            className="rounded-lg border border-red-300 text-red-700 text-xs font-semibold px-3 py-2 disabled:opacity-60"
          >
            Recusar reagendamento
          </button>
        )}

        <button
          type="button"
          onClick={() => onOpenRequest(item.serviceRequestId)}
          className="rounded-lg border border-[#d0d5dd] text-[#344054] text-xs font-semibold px-3 py-2"
        >
          Ver pedido
        </button>
      </div>
    </div>
  );
};

const KpiCard: React.FC<{ title: string; value: number }> = ({ title, value }) => (
  <div className="rounded-xl bg-white border border-[#e4e7ec] py-2 px-3 text-center">
    <p className="text-[11px] uppercase text-[#667085] font-semibold tracking-wide">{title}</p>
    <p className="text-lg font-bold text-[#101828]">{value}</p>
  </div>
);

export default Agenda;
