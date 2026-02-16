
import React, { useState } from 'react';
import { ServiceRequest } from '../types';

interface Props {
  request: ServiceRequest;
  onCancel: () => void;
  onFinish: (requestId: string, rating: number, review: string, paymentMethod: string, amount?: string) => void;
}

const ServiceCompletionFlow: React.FC<Props> = ({ request, onCancel, onFinish }) => {
  const [step, setStep] = useState(1);
  const [rating, setRating] = useState(0);
  const [review, setReview] = useState('');
  const [paymentMethod, setPaymentMethod] = useState('');
  const [amount, setAmount] = useState('');

  const paymentOptions = [
    { id: 'pix', label: 'PIX', icon: 'qr_code_2' },
    { id: 'card', label: 'Cartão de Crédito', icon: 'credit_card' },
    { id: 'direct', label: 'Pagar ao Prestador', icon: 'payments' },
  ];

  const handleNext = () => setStep(step + 1);
  const handleBack = () => step > 1 ? setStep(step - 1) : onCancel();

  const handleFinish = () => {
    if (rating === 0) return;
    onFinish(request.id, rating, review, paymentMethod, amount);
  };

  return (
    <div className="flex flex-col h-screen bg-white overflow-hidden">
      {/* Header */}
      <header className="flex items-center p-4 border-b border-primary/5 sticky top-0 bg-white z-10">
        <button onClick={handleBack} className="p-2 -ml-2 text-primary hover:bg-primary/5 rounded-full transition-colors">
          <span className="material-symbols-outlined">arrow_back</span>
        </button>
        <h2 className="text-[#101818] text-base font-bold flex-1 text-center pr-10">Finalizar Serviço</h2>
      </header>

      <div className="flex-1 p-6 overflow-y-auto no-scrollbar pb-24">
        {step === 1 && (
          <div className="space-y-8 animate-fadeIn">
            <div className="text-center space-y-3">
              <div className="size-20 bg-primary/10 text-primary rounded-full flex items-center justify-center mx-auto">
                <span className="material-symbols-outlined text-4xl">task_alt</span>
              </div>
              <h1 className="text-2xl font-bold text-gray-900">O serviço foi concluído?</h1>
              <p className="text-gray-600">Confirme se o profissional finalizou o trabalho conforme o esperado.</p>
            </div>

            <div className="bg-background-light p-4 rounded-2xl border border-primary/5">
              <div className="flex items-center gap-3">
                <img src={request.provider?.avatar} className="size-12 rounded-full object-cover" alt="" />
                <div>
                  <p className="text-xs text-[#5e8d8d] font-bold uppercase tracking-wider">Prestador</p>
                  <p className="font-bold text-[#101818]">{request.provider?.name}</p>
                </div>
              </div>
            </div>

            <button 
              onClick={handleNext}
              className="w-full h-14 bg-primary text-white rounded-xl font-bold shadow-lg shadow-primary/20 active:scale-95 transition-all"
            >
              Sim, serviço concluído
            </button>
          </div>
        )}

        {step === 2 && (
          <div className="space-y-8 animate-fadeIn">
            <div className="text-center space-y-2">
              <h1 className="text-2xl font-bold text-gray-900">Forma de Pagamento</h1>
              <p className="text-gray-600">Escolha como deseja realizar o pagamento.</p>
            </div>

            <div className="space-y-3">
              {paymentOptions.map((opt) => (
                <button
                  key={opt.id}
                  onClick={() => setPaymentMethod(opt.id)}
                  className={`w-full p-4 rounded-2xl border-2 flex items-center gap-4 transition-all ${
                    paymentMethod === opt.id 
                      ? 'border-primary bg-primary/5' 
                      : 'border-primary/5 bg-white'
                  }`}
                >
                  <div className={`size-10 rounded-xl flex items-center justify-center ${
                    paymentMethod === opt.id ? 'bg-primary text-white' : 'bg-primary/10 text-primary'
                  }`}>
                    <span className="material-symbols-outlined">{opt.icon}</span>
                  </div>
                  <span className={`font-bold text-sm ${paymentMethod === opt.id ? 'text-primary' : 'text-[#101818]'}`}>
                    {opt.label}
                  </span>
                  {paymentMethod === opt.id && (
                    <span className="material-symbols-outlined text-primary ml-auto">check_circle</span>
                  )}
                </button>
              ))}
            </div>

            <div className="space-y-2">
              <label className="text-xs font-bold text-[#5e8d8d] uppercase ml-1">Valor do serviço (opcional)</label>
              <div className="relative">
                <span className="absolute left-4 top-1/2 -translate-y-1/2 text-primary font-bold">R$</span>
                <input 
                  type="text"
                  inputMode="decimal"
                  placeholder="0,00"
                  value={amount}
                  onChange={(e) => setAmount(e.target.value)}
                  className="w-full h-14 bg-background-light border border-primary/10 rounded-xl pl-12 pr-4 text-base font-bold text-[#101818] focus:ring-2 focus:ring-primary/20 transition-all placeholder:text-[#5e8d8d]/50"
                />
              </div>
            </div>

            <button 
              onClick={handleNext}
              disabled={!paymentMethod}
              className="w-full h-14 bg-primary text-white rounded-xl font-bold shadow-lg shadow-primary/20 disabled:opacity-50 active:scale-95 transition-all"
            >
              Próximo
            </button>
          </div>
        )}

        {step === 3 && (
          <div className="space-y-8 animate-fadeIn">
            <div className="text-center space-y-2">
              <h1 className="text-2xl font-bold text-gray-900">Avalie o Serviço</h1>
              <p className="text-gray-600">Sua avaliação ajuda a manter a qualidade dos nossos profissionais.</p>
            </div>

            <div className="flex flex-col items-center gap-6">
              <div className="flex gap-2">
                {[1, 2, 3, 4, 5].map((s) => (
                  <button 
                    key={s} 
                    onClick={() => setRating(s)}
                    className={`text-4xl transition-all ${rating >= s ? 'text-yellow-400 material-symbols-fill' : 'text-gray-300'}`}
                  >
                    <span className="material-symbols-outlined text-5xl">star</span>
                  </button>
                ))}
              </div>
              
              <div className="w-full space-y-2">
                <label className="text-xs font-bold text-[#5e8d8d] uppercase ml-1">Comentário (opcional)</label>
                <textarea
                  className="w-full h-32 p-4 border border-primary/10 rounded-2xl focus:ring-primary focus:border-primary text-sm resize-none"
                  placeholder="Conte-nos como foi sua experiência..."
                  value={review}
                  onChange={(e) => setReview(e.target.value)}
                />
              </div>
            </div>

            <button 
              onClick={handleFinish}
              disabled={rating === 0}
              className="w-full h-14 bg-primary text-white rounded-xl font-bold shadow-lg shadow-primary/20 disabled:opacity-50 active:scale-95 transition-all"
            >
              Concluir Avaliação
            </button>
          </div>
        )}
      </div>
    </div>
  );
};

export default ServiceCompletionFlow;
