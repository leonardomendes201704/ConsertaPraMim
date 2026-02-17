import React from 'react';

const SplashScreen: React.FC = () => {
  return (
    <div className="min-h-screen bg-gradient-to-br from-[#0D6EFD] to-[#23A6F0] flex flex-col items-center justify-center text-white px-6">
      <div className="w-20 h-20 rounded-2xl bg-white/20 backdrop-blur-sm flex items-center justify-center mb-6 shadow-lg">
        <span className="material-symbols-outlined text-5xl material-symbols-fill">construction</span>
      </div>
      <h1 className="text-3xl font-bold">Conserta Pra Mim</h1>
      <p className="text-sm mt-2 opacity-90 tracking-wide">App do Prestador</p>
      <div className="mt-10 w-12 h-12 border-4 border-white/30 border-t-white rounded-full animate-spin"></div>
    </div>
  );
};

export default SplashScreen;
