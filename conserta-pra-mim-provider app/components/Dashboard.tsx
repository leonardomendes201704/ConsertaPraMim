import React from 'react';
import { ProviderDashboardData, ProviderRequestCard } from '../types';

interface Props {
  dashboard: ProviderDashboardData | null;
  loading: boolean;
  error: string;
  onRefresh: () => Promise<void>;
  onOpenRequest: (request: ProviderRequestCard) => void;
  onOpenAgenda: () => void;
  onOpenProposals: () => void;
  onOpenProfile: () => void;
}

function formatDistance(distanceKm?: number): string {
  if (!Number.isFinite(distanceKm)) {
    return 'Distancia nao informada';
  }

  return `${distanceKm!.toFixed(1)} km`;
}

const Dashboard: React.FC<Props> = ({
  dashboard,
  loading,
  error,
  onRefresh,
  onOpenRequest,
  onOpenAgenda,
  onOpenProposals,
  onOpenProfile
}) => {
  const kpi = dashboard?.kpis;

  return (
    <div className="min-h-screen bg-[#f4f7fb] pb-24">
      <header className="bg-white border-b border-[#e4e7ec] sticky top-0 z-10">
        <div className="max-w-md mx-auto px-4 py-4 flex items-center justify-between">
          <div>
            <p className="text-xs uppercase tracking-wide text-[#667085] font-semibold">App Prestador</p>
            <h1 className="text-lg font-bold text-[#101828]">Ola, {dashboard?.providerName || 'Prestador'}!</h1>
          </div>
          <button
            type="button"
            onClick={() => void onRefresh()}
            className="rounded-xl border border-[#d0d5dd] px-3 py-2 text-sm font-semibold text-[#344054]"
          >
            Atualizar
          </button>
        </div>
      </header>

      <main className="max-w-md mx-auto px-4 py-5">
        {error && (
          <div className="mb-4 rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-red-700 text-sm">{error}</div>
        )}

        <section className="grid grid-cols-2 gap-3 mb-5">
          <KpiCard title="Oportunidades" value={kpi?.nearbyRequestsCount ?? 0} icon="pin_drop" />
          <KpiCard title="Propostas abertas" value={kpi?.activeProposalsCount ?? 0} icon="savings" />
          <KpiCard title="Propostas aceitas" value={kpi?.acceptedProposalsCount ?? 0} icon="verified" />
          <button type="button" onClick={onOpenAgenda} className="text-left">
            <KpiCard title="Pendentes agenda" value={kpi?.pendingAppointmentsCount ?? 0} icon="event_busy" />
          </button>
          <button type="button" onClick={onOpenAgenda} className="text-left">
            <KpiCard title="Visitas confirmadas" value={kpi?.upcomingConfirmedVisitsCount ?? 0} icon="event_available" />
          </button>
        </section>

        <section className="rounded-2xl bg-white border border-[#e4e7ec] shadow-sm p-4 mb-4">
          <div className="flex items-center justify-between mb-3">
            <h2 className="font-bold text-[#101828]">Pedidos proximos</h2>
            <span className="text-xs text-[#667085]">Top {dashboard?.nearbyRequests.length ?? 0}</span>
          </div>

          {loading ? (
            <p className="text-sm text-[#667085]">Carregando pedidos...</p>
          ) : !dashboard?.nearbyRequests.length ? (
            <p className="text-sm text-[#667085]">Nenhum pedido proximo encontrado no momento.</p>
          ) : (
            <div className="space-y-3">
              {dashboard.nearbyRequests.map((request) => (
                <button
                  key={request.id}
                  type="button"
                  onClick={() => onOpenRequest(request)}
                  className="w-full text-left rounded-xl border border-[#eaecf0] p-3 hover:border-primary/40 transition-colors"
                >
                  <div className="flex justify-between items-start gap-2">
                    <div className="flex items-start gap-3">
                      <span className="material-symbols-outlined text-primary mt-0.5">{request.categoryIcon}</span>
                      <div>
                        <p className="font-semibold text-sm text-[#101828]">{request.category}</p>
                        <p className="text-xs text-[#475467] line-clamp-2">{request.description}</p>
                      </div>
                    </div>
                    {request.alreadyProposed ? (
                      <span className="text-[10px] px-2 py-1 rounded-full bg-emerald-100 text-emerald-700 font-bold uppercase">Proposta enviada</span>
                    ) : (
                      <span className="text-[10px] px-2 py-1 rounded-full bg-blue-100 text-blue-700 font-bold uppercase">Novo</span>
                    )}
                  </div>

                  <div className="mt-3 pt-2 border-t border-[#f2f4f7] flex justify-between text-[11px] text-[#667085]">
                    <span>{formatDistance(request.distanceKm)}</span>
                    <span>{request.createdAtLabel}</span>
                  </div>
                </button>
              ))}
            </div>
          )}
        </section>

        <section className="rounded-2xl bg-white border border-[#e4e7ec] shadow-sm p-4">
          <div className="flex items-center justify-between gap-2 mb-3">
            <h2 className="font-bold text-[#101828]">Agenda (destaques)</h2>
            <button
              type="button"
              onClick={onOpenAgenda}
              className="text-xs font-semibold text-primary"
            >
              Abrir agenda completa
            </button>
          </div>

          {!dashboard?.agendaHighlights.length ? (
            <p className="text-sm text-[#667085]">Sem itens de agenda com prioridade no momento.</p>
          ) : (
            <div className="space-y-2">
              {dashboard.agendaHighlights.map((item) => (
                <div key={item.appointmentId} className="rounded-xl bg-[#f8fafc] border border-[#e4e7ec] px-3 py-2">
                  <p className="text-sm font-semibold text-[#101828]">{item.category || 'Servico'}</p>
                  <p className="text-xs text-[#475467]">{item.clientName || 'Cliente'} - {item.windowLabel}</p>
                  <p className="text-[11px] mt-1 text-primary font-semibold">{item.statusLabel}</p>
                </div>
              ))}
            </div>
          )}
        </section>
      </main>

      <nav className="fixed bottom-0 left-0 right-0 bg-white border-t border-[#e4e7ec] max-w-md mx-auto px-6 py-3 flex justify-between z-20">
        <button className="flex flex-col items-center text-primary">
          <span className="material-symbols-outlined material-symbols-fill">home</span>
          <span className="text-[10px] font-semibold">Inicio</span>
        </button>
        <button onClick={onOpenProposals} className="flex flex-col items-center text-[#667085]">
          <span className="material-symbols-outlined">description</span>
          <span className="text-[10px] font-semibold">Propostas</span>
        </button>
        <button onClick={onOpenAgenda} className="flex flex-col items-center text-[#667085]">
          <span className="material-symbols-outlined">event</span>
          <span className="text-[10px] font-semibold">Agenda</span>
        </button>
        <button onClick={onOpenProfile} className="flex flex-col items-center text-[#667085]">
          <span className="material-symbols-outlined">person</span>
          <span className="text-[10px] font-semibold">Perfil</span>
        </button>
      </nav>
    </div>
  );
};

const KpiCard: React.FC<{ title: string; value: number; icon: string }> = ({ title, value, icon }) => (
  <div className="rounded-2xl bg-white border border-[#e4e7ec] shadow-sm p-3">
    <div className="flex items-start justify-between gap-2">
      <p className="text-xs text-[#667085] font-semibold uppercase tracking-wide">{title}</p>
      <span className="material-symbols-outlined text-primary text-lg">{icon}</span>
    </div>
    <p className="text-2xl font-bold text-[#101828] mt-2">{value}</p>
  </div>
);

export default Dashboard;
