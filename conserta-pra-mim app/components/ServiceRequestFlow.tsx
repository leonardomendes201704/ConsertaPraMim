
import React, { useState, useMemo } from 'react';
import { getAIDiagnostic } from '../services/gemini';
import { ServiceRequest } from '../types';

interface Props {
  categoryId: string | null;
  onCancel: () => void;
  onFinish: (newRequest?: ServiceRequest) => void;
}

const CATEGORY_MAP: Record<string, { name: string, icon: string, problems: string[] }> = {
  '1': { name: 'Elétrica', icon: 'bolt', problems: ['Troca de resistência de chuveiro', 'Tomada parou de funcionar', 'Disjuntor caindo', 'Instalação de luminária', 'Curto-circuito em fiação'] },
  '2': { name: 'Hidráulica', icon: 'water_drop', problems: ['Torneira pingando', 'Vaso sanitário entupido', 'Vazamento sob a pia', 'Caixa acoplada não enche', 'Baixa pressão na água'] },
  '3': { name: 'Montagem', icon: 'construction', problems: ['Montar guarda-roupa', 'Instalar painel de TV', 'Montar mesa de escritório', 'Ajustar portas de armário', 'Instalar cortinas/persianas'] },
  '4': { name: 'Pintura', icon: 'format_paint', problems: ['Pintura de parede única', 'Retoque de teto com mofo', 'Pintura de portas e batentes', 'Massa corrida em pequena área', 'Pintura de janelas de ferro'] },
  '5': { name: 'Limpeza', icon: 'cleaning_services', problems: ['Limpeza pós-obra', 'Limpeza pesada de cozinha', 'Higienização de sofá', 'Limpeza de janelas externas', 'Limpeza pré-mudança'] },
  '6': { name: 'Ar Condicionado', icon: 'ac_unit', problems: ['Limpeza de filtros', 'Ar não gela', 'Vazamento de água na evaporadora', 'Instalação de novo aparelho', 'Recarga de gás'] },
  '12': { name: 'Faz Tudo', icon: 'handyman', problems: ['Pendurar quadros e espelhos', 'Trocar fechadura', 'Instalar suporte de TV', 'Vedação de box de banheiro', 'Instalação de varal'] }
};

const DEFAULT_PROBLEMS = ['Torneira com defeito', 'Problema elétrico', 'Reparo em móveis', 'Vazamento visível', 'Limpeza urgente'];

const ServiceRequestFlow: React.FC<Props> = ({ categoryId, onCancel, onFinish }) => {
  const [step, setStep] = useState(1);
  const [description, setDescription] = useState('');
  const [diagnosis, setDiagnosis] = useState<any>(null);
  const [loading, setLoading] = useState(false);
  const [createdRequest, setCreatedRequest] = useState<ServiceRequest | null>(null);

  const selectedCategory = useMemo(() => {
    return categoryId ? CATEGORY_MAP[categoryId] : null;
  }, [categoryId]);

  const commonProblems = selectedCategory?.problems || DEFAULT_PROBLEMS;

  const handleAIDiagnosis = async () => {
    if (!description.trim()) return;
    setLoading(true);
    const result = await getAIDiagnostic(description);
    setDiagnosis(result);
    setLoading(false);
    setStep(3);
  };

  const handleConfirm = () => {
    const newReq: ServiceRequest = {
      id: Math.floor(1000 + Math.random() * 9000).toString(),
      title: description.length > 30 ? description.substring(0, 27) + "..." : description,
      status: 'AGUARDANDO',
      date: new Date().toLocaleDateString('pt-BR', { day: '2-digit', month: 'short' }),
      category: selectedCategory?.name || 'Geral',
      icon: selectedCategory?.icon || 'build',
      description: description,
      aiDiagnosis: diagnosis ? {
        summary: diagnosis.summary,
        possibleCauses: diagnosis.possibleCauses
      } : undefined
    };
    
    setCreatedRequest(newReq);
    setStep(4);
  };

  const prevStep = () => setStep(step - 1);

  const handleSelectProblem = (problem: string) => {
    setDescription(prev => prev ? `${prev}. ${problem}` : problem);
  };

  return (
    <div className="flex flex-col h-screen bg-white">
      <header className="flex items-center p-4 border-b border-primary/5 sticky top-0 bg-white z-10">
        <button onClick={step === 1 ? onCancel : prevStep} className="text-primary hover:bg-primary/5 p-2 rounded-full transition-colors">
          <span className="material-symbols-outlined">arrow_back</span>
        </button>
        <h2 className="text-[#101818] text-base font-bold flex-1 text-center pr-10">
          {selectedCategory ? `Novo Pedido: ${selectedCategory.name}` : 'Novo Pedido'}
        </h2>
      </header>

      <div className="flex-1 p-6 overflow-y-auto no-scrollbar">
        {step === 1 && (
          <div className="space-y-6">
            <div className="space-y-2">
              <h1 className="text-2xl font-bold text-primary">O que precisa ser feito?</h1>
              <p className="text-gray-600 text-sm leading-relaxed">
                Descreva o problema ou escolha uma das sugestões abaixo para facilitar a análise da nossa IA.
              </p>
            </div>

            {/* Common Problems Suggestions */}
            <div className="space-y-3">
              <h3 className="text-xs font-bold text-[#5e8d8d] uppercase tracking-wider">Problemas comuns em {selectedCategory?.name || 'geral'}</h3>
              <div className="flex flex-wrap gap-2">
                {commonProblems.map((prob, i) => (
                  <button
                    key={i}
                    onClick={() => handleSelectProblem(prob)}
                    className="px-4 py-2 bg-primary/5 hover:bg-primary/10 text-primary text-xs font-bold rounded-full border border-primary/10 transition-colors whitespace-nowrap active:scale-95"
                  >
                    {prob}
                  </button>
                ))}
              </div>
            </div>

            <div className="space-y-2">
              <h3 className="text-xs font-bold text-[#5e8d8d] uppercase tracking-wider">Sua descrição</h3>
              <textarea
                className="w-full h-40 p-4 border border-[#dae7e7] rounded-xl focus:ring-2 focus:ring-primary/20 focus:border-primary resize-none text-sm placeholder:text-[#5e8d8d]"
                placeholder="Ex: Minha torneira da cozinha está pingando sem parar mesmo depois de fechar bem..."
                value={description}
                onChange={(e) => setDescription(e.target.value)}
              />
            </div>

            <button 
              onClick={handleAIDiagnosis}
              disabled={loading || !description.trim()}
              className="w-full bg-primary text-white h-14 rounded-xl font-bold disabled:opacity-50 flex items-center justify-center gap-2 shadow-lg shadow-primary/20 active:scale-[0.98] transition-all"
            >
              {loading ? (
                <>
                  <div className="size-5 border-2 border-white/30 border-t-white rounded-full animate-spin"></div>
                  Analisando...
                </>
              ) : (
                <>
                  Analisar com Conserta AI
                  <span className="material-symbols-outlined">auto_awesome</span>
                </>
              )}
            </button>
          </div>
        )}

        {step === 3 && diagnosis && (
          <div className="space-y-6 animate-fadeIn pb-10">
            <div className="bg-primary/5 p-5 rounded-2xl border border-primary/10 relative overflow-hidden">
              <div className="absolute top-[-20%] right-[-10%] size-32 bg-primary/5 rounded-full blur-2xl"></div>
              <h3 className="text-primary font-bold flex items-center gap-2 mb-3 relative z-10">
                <span className="material-symbols-outlined material-symbols-fill">auto_awesome</span>
                Diagnóstico Conserta AI
              </h3>
              <p className="text-gray-800 italic text-sm leading-relaxed relative z-10">"{diagnosis.summary}"</p>
            </div>

            <div className="space-y-3">
              <h4 className="font-bold text-gray-900 flex items-center gap-2">
                <span className="material-symbols-outlined text-primary text-xl">list_alt</span>
                Possíveis Causas
              </h4>
              <ul className="space-y-2">
                {diagnosis.possibleCauses.map((c: string, i: number) => (
                  <li key={i} className="text-gray-600 text-sm flex items-start gap-3 bg-gray-50 p-3 rounded-xl border border-gray-100">
                    <span className="size-5 rounded-full bg-primary/10 text-primary flex items-center justify-center text-[10px] font-bold shrink-0 mt-0.5">{i+1}</span>
                    {c}
                  </li>
                ))}
              </ul>
            </div>

            <div className="bg-orange-50 p-5 rounded-2xl border border-orange-100">
              <h4 className="font-bold text-orange-700 flex items-center gap-2 mb-3">
                <span className="material-symbols-outlined">warning</span>
                Instruções de Segurança
              </h4>
              <ul className="space-y-2">
                {diagnosis.safetyInstructions.map((s: string, i: number) => (
                  <li key={i} className="text-orange-800 text-xs flex items-start gap-3">
                    <span className="material-symbols-outlined text-orange-400 text-sm mt-0.5">priority_high</span>
                    {s}
                  </li>
                ))}
              </ul>
            </div>

            <button 
              onClick={handleConfirm}
              className="w-full bg-primary text-white h-14 rounded-xl font-bold shadow-lg shadow-primary/20 hover:bg-primary/90 transition-all active:scale-[0.98]"
            >
              Confirmar e Buscar Profissional
            </button>
            <p className="text-[10px] text-center text-[#5e8d8d] font-medium px-4">
              Ao confirmar, enviaremos este diagnóstico para os profissionais próximos.
            </p>
          </div>
        )}

        {step === 4 && (
          <div className="flex flex-col items-center justify-center h-full text-center space-y-8 animate-fadeIn">
            <div className="relative">
              <div className="absolute inset-0 bg-green-500/20 rounded-full blur-2xl animate-pulse"></div>
              <div className="size-28 bg-green-100 text-green-600 rounded-full flex items-center justify-center relative border-4 border-white shadow-xl">
                <span className="material-symbols-outlined text-6xl material-symbols-fill animate-scaleIn">check_circle</span>
              </div>
            </div>
            <div className="space-y-2">
              <h1 className="text-2xl font-bold text-gray-900">Pedido Enviado!</h1>
              <p className="text-gray-600 text-sm leading-relaxed px-6">
                Excelente! Já notificamos os profissionais de <strong>{selectedCategory?.name}</strong> mais próximos. Fique atento às notificações!
              </p>
            </div>
            <div className="w-full max-w-[280px] bg-background-light p-4 rounded-2xl border border-primary/5 space-y-3">
               <div className="flex items-center justify-between text-xs">
                 <span className="text-[#5e8d8d] font-medium">Protocolo:</span>
                 <span className="text-primary font-bold">#{createdRequest?.id}</span>
               </div>
               <div className="flex items-center justify-between text-xs">
                 <span className="text-[#5e8d8d] font-medium">Status:</span>
                 <span className="bg-blue-100 text-blue-600 px-2 py-0.5 rounded-full font-bold uppercase text-[9px]">Buscando...</span>
               </div>
            </div>
            <button 
              onClick={() => onFinish(createdRequest || undefined)}
              className="w-full bg-primary text-white h-14 rounded-xl font-bold shadow-lg shadow-primary/20 active:scale-[0.98] transition-all"
            >
              Voltar ao Início
            </button>
          </div>
        )}
      </div>
    </div>
  );
};

export default ServiceRequestFlow;
