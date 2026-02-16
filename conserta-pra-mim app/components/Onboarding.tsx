
import React, { useState } from 'react';

const slides = [
  {
    title: "Peça um serviço",
    description: "Encontre profissionais qualificados para reparos em sua casa de forma rápida e segura.",
    image: "https://images.unsplash.com/photo-1581578731548-c64695ce6958?auto=format&fit=crop&q=80&w=800"
  },
  {
    title: "Profissionais de Confiança",
    description: "Todos os nossos prestadores são verificados e avaliados pela comunidade.",
    image: "https://images.unsplash.com/photo-1621905251189-08b45d6a269e?auto=format&fit=crop&q=80&w=800"
  },
  {
    title: "Pagamento Facilitado",
    description: "Pague com segurança através do app após a conclusão do serviço.",
    image: "https://images.unsplash.com/photo-1563013544-824ae1b704d3?auto=format&fit=crop&q=80&w=800"
  }
];

interface Props {
  onFinish: () => void;
}

const Onboarding: React.FC<Props> = ({ onFinish }) => {
  const [current, setCurrent] = useState(0);

  const nextSlide = () => {
    if (current < slides.length - 1) {
      setCurrent(current + 1);
    } else {
      onFinish();
    }
  };

  return (
    <div className="flex h-screen flex-col bg-white overflow-hidden">
      <div className="flex items-center p-4 justify-end">
        <button onClick={onFinish} className="text-primary text-base font-semibold hover:opacity-80">
          Pular
        </button>
      </div>
      <div className="flex-grow flex flex-col justify-center px-6">
        <div className="relative w-full aspect-[4/3] rounded-xl overflow-hidden bg-primary/5 mb-10 shadow-lg border border-primary/5">
          <img 
            key={slides[current].image}
            src={slides[current].image} 
            alt={slides[current].title} 
            className="w-full h-full object-cover transition-opacity duration-500 animate-fadeIn"
            onError={(e) => {
              // Fallback elegante caso a imagem do Unsplash falhe
              const target = e.target as HTMLImageElement;
              target.src = `https://placehold.co/600x400/007f80/ffffff?text=${encodeURIComponent(slides[current].title)}`;
            }}
          />
          <div className="absolute inset-0 bg-gradient-to-t from-black/20 to-transparent pointer-events-none"></div>
        </div>
        <div className="text-center">
          <h1 className="text-primary text-3xl font-bold mb-4">{slides[current].title}</h1>
          <p className="text-[#4a5f5f] text-lg leading-relaxed px-4">
            {slides[current].description}
          </p>
        </div>
      </div>
      <div className="px-6 pb-12 pt-6">
        <div className="flex w-full flex-row items-center justify-center gap-2 mb-10">
          {slides.map((_, i) => (
            <div 
              key={i} 
              className={`h-2 rounded-full transition-all duration-300 ${i === current ? 'w-6 bg-primary' : 'w-2 bg-primary/20'}`}
            ></div>
          ))}
        </div>
        <button 
          onClick={nextSlide} 
          className="w-full bg-primary hover:bg-primary/90 text-white font-semibold py-4 rounded-xl flex items-center justify-center gap-2 shadow-lg shadow-primary/20 group transition-all"
        >
          {current === slides.length - 1 ? 'Começar' : 'Próximo'}
          <span className="material-symbols-outlined text-xl transition-transform group-hover:translate-x-1">arrow_forward</span>
        </button>
      </div>
    </div>
  );
};

export default Onboarding;
