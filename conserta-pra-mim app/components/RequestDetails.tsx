
import React from 'react';
import { ServiceRequest } from '../types';

interface Props {
  request: ServiceRequest;
  onBack: () => void;
  onOpenChat: () => void;
  onFinishService?: () => void;
}

const RequestDetails: React.FC<Props> = ({ request, onBack, onOpenChat, onFinishService }) => {
  return (
    <div className="flex flex-col h-screen bg-background-light overflow-hidden">
      {/* Header */}
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
        {/* Status Badge */}
        <div className="px-4 py-6 bg-white border-b border-primary/5">
          <div className={`flex flex-col items-center justify-center p-6 rounded-2xl border ${
            request.status === 'EM_ANDAMENTO' ? 'bg-orange-50 border-orange-100' : 'bg-green-50 border-green-100'
          }`}>
            <span className={`material-symbols-outlined text-4xl mb-2 ${
              request.status === 'EM_ANDAMENTO' ? 'text-orange-500' : 'text-green-500'
            }`}>
              {request.status === 'EM_ANDAMENTO' ? 'sync' : 'check_circle'}
            </span>
            <h3 className="text-lg font-bold text-[#101818]">
              {request.status === 'EM_ANDAMENTO' ? 'Serviço em Andamento' : 'Serviço Concluído'}
            </h3>
            <p className="text-sm text-[#5e8d8d] font-medium text-center mt-1">
              {request.status === 'EM_ANDAMENTO' 
                ? 'O profissional está trabalhando no seu reparo.' 
                : 'Tudo pronto! Seu reparo foi finalizado com sucesso.'}
            </p>
          </div>
        </div>

        {/* Professional Card */}
        {request.provider && (
          <div className="m-4 p-4 bg-white rounded-2xl border border-primary/5 shadow-sm">
            <h4 className="text-xs font-bold text-primary uppercase tracking-wider mb-4">Profissional Designado</h4>
            <div className="flex items-center gap-4">
              <img src={request.provider.avatar} className="size-14 rounded-full border-2 border-primary/10 object-cover" alt={request.provider.name} />
              <div className="flex-1">
                <h3 className="font-bold text-[#101818]">{request.provider.name}</h3>
                <p className="text-xs text-[#5e8d8d]">{request.provider.specialty}</p>
                <div className="flex items-center gap-1 mt-1">
                  <span className="material-symbols-outlined text-yellow-500 text-sm material-symbols-fill">star</span>
                  <span className="text-xs font-bold text-[#101818]">{request.provider.rating}</span>
                </div>
              </div>
              <div className="flex gap-2">
                <button 
                  onClick={onOpenChat}
                  className="size-10 rounded-full bg-primary/10 text-primary flex items-center justify-center active:scale-90 transition-transform"
                >
                  <span className="material-symbols-outlined text-xl">chat</span>
                </button>
                <button className="size-10 rounded-full bg-primary/10 text-primary flex items-center justify-center active:scale-90 transition-transform">
                  <span className="material-symbols-outlined text-xl">call</span>
                </button>
              </div>
            </div>
          </div>
        )}

        {/* Diagnosis Card */}
        {request.aiDiagnosis && (
          <div className="mx-4 mb-4 p-4 bg-white rounded-2xl border border-primary/5 shadow-sm">
            <div className="flex items-center gap-2 mb-3">
              <span className="material-symbols-outlined text-primary text-xl">auto_awesome</span>
              <h4 className="text-sm font-bold text-[#101818]">Diagnóstico Inteligente</h4>
            </div>
            <p className="text-sm text-gray-700 italic border-l-2 border-primary/20 pl-3 py-1 mb-4">
              "{request.aiDiagnosis.summary}"
            </p>
            <div className="space-y-2">
              <h5 className="text-xs font-bold text-[#5e8d8d] uppercase">Possíveis Causas Analisadas:</h5>
              <div className="flex flex-wrap gap-2">
                {request.aiDiagnosis.possibleCauses.map((cause, i) => (
                  <span key={i} className="text-[10px] font-bold bg-primary/5 text-primary px-2 py-1 rounded-lg">
                    {cause}
                  </span>
                ))}
              </div>
            </div>
          </div>
        )}

        {/* Timeline */}
        <div className="mx-4 p-4 bg-white rounded-2xl border border-primary/5 shadow-sm">
          <h4 className="text-xs font-bold text-[#5e8d8d] uppercase mb-4">Acompanhamento</h4>
          <div className="space-y-6 relative before:absolute before:left-[11px] before:top-2 before:bottom-2 before:w-[2px] before:bg-primary/10">
            <TimelineItem active icon="check_circle" title="Pedido Solicitado" time="10:30" />
            <TimelineItem active icon="person_search" title="Profissional Encontrado" time="10:45" />
            <TimelineItem 
              active={request.status === 'EM_ANDAMENTO' || request.status === 'CONCLUIDO'} 
              icon="directions_bike" 
              title="Profissional a caminho" 
              time="11:15" 
            />
            <TimelineItem 
              active={request.status === 'EM_ANDAMENTO' || request.status === 'CONCLUIDO'} 
              icon="build" 
              title="Em Execução" 
              time="11:30" 
            />
          </div>
        </div>
      </div>

      {/* Action Footer */}
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

const TimelineItem: React.FC<{ active?: boolean; icon: string; title: string; time: string }> = ({ active, icon, title, time }) => (
  <div className="flex items-start gap-4 relative z-10">
    <div className={`size-6 rounded-full flex items-center justify-center border-2 ${
      active ? 'bg-primary border-primary text-white' : 'bg-white border-primary/20 text-primary/30'
    }`}>
      <span className="material-symbols-outlined text-xs">{icon}</span>
    </div>
    <div className="flex-1 flex justify-between items-center">
      <p className={`text-sm font-bold ${active ? 'text-[#101818]' : 'text-primary/30'}`}>{title}</p>
      <span className="text-xs font-medium text-[#5e8d8d]">{time}</span>
    </div>
  </div>
);

export default RequestDetails;
