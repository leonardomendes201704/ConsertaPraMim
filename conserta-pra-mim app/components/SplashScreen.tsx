
import React, { useEffect, useState } from 'react';

const SplashScreen: React.FC = () => {
  const [progress, setProgress] = useState(0);

  useEffect(() => {
    const interval = setInterval(() => {
      setProgress(prev => {
        if (prev >= 100) return 100;
        return prev + Math.random() * 15;
      });
    }, 200);
    return () => clearInterval(interval);
  }, []);

  return (
    <div className="flex flex-col h-screen w-full items-center justify-between p-8 bg-background-light overflow-hidden">
      <div className="absolute top-[-10%] right-[-10%] w-64 h-64 bg-primary/5 rounded-full blur-3xl"></div>
      <div className="flex-1"></div>
      <div className="flex flex-col items-center justify-center gap-6 z-10">
        <div className="relative flex items-center justify-center w-32 h-32 bg-primary rounded-xl shadow-xl shadow-primary/20">
          <span className="material-symbols-outlined text-white text-[72px] material-symbols-fill">home</span>
          <div className="absolute -bottom-2 -right-2 bg-white p-2 rounded-lg shadow-md border border-primary/10 flex items-center justify-center">
             <div className="w-8 h-5 overflow-hidden rounded-sm shadow-sm">
                <svg viewBox="0 0 720 504" className="w-full h-full">
                  <rect width="720" height="504" fill="#009b3a"/>
                  <path d="M360 48L672 252L360 456L48 252L360 48Z" fill="#fedf00"/>
                  <circle cx="360" cy="252" r="105" fill="#002776"/>
                  <path d="M255 252C255 264 260 275 268 284C286 276 312 268 360 268C408 268 434 276 452 284C460 275 465 264 465 252C465 240 460 229 452 220C434 228 408 236 360 236C312 236 286 228 268 220C260 229 255 240 255 252Z" fill="white"/>
                </svg>
              </div>
          </div>
        </div>
        <div className="text-center">
          <h1 className="text-background-dark text-4xl font-bold tracking-tight mb-2">
            Conserta <span className="text-primary">Pra Mim</span>
          </h1>
          <p className="text-background-dark/60 text-lg font-medium">Soluções para o seu lar</p>
        </div>
      </div>
      <div className="flex-1 flex flex-col justify-end w-full max-w-xs pb-12 z-10">
        <div className="flex flex-col gap-4">
          <div className="flex justify-between items-center px-1">
            <span className="text-background-dark/40 text-sm font-medium">Iniciando serviços...</span>
            <span className="text-primary text-sm font-bold">{Math.floor(progress)}%</span>
          </div>
          <div className="h-1.5 w-full bg-primary/10 rounded-full overflow-hidden mb-6">
            <div 
              className="h-full bg-primary rounded-full transition-all duration-300" 
              style={{ width: `${progress}%` }}
            ></div>
          </div>
          <div className="flex flex-col items-center gap-1">
            <p className="text-center text-[10px] font-bold text-primary/40 tracking-widest uppercase">
              Feito com orgulho no Brasil 🇧🇷
            </p>
            <p className="text-center text-[8px] font-bold text-primary/30 tracking-widest uppercase">
              Powered by DevCfrat Studio
            </p>
          </div>
        </div>
      </div>
    </div>
  );
};

export default SplashScreen;
