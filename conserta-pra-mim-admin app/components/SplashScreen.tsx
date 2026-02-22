import React from 'react';

const SplashScreen: React.FC = () => {
  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-950 via-slate-900 to-blue-900 px-6 py-10 text-white">
      <div className="mx-auto flex min-h-[80vh] max-w-md flex-col items-center justify-center gap-6 text-center">
        <div className="h-16 w-16 rounded-2xl bg-white/10 p-4 ring-1 ring-white/20">
          <span className="material-symbols-outlined text-4xl">admin_panel_settings</span>
        </div>
        <div className="space-y-2">
          <p className="text-xs uppercase tracking-[0.2em] text-blue-200">ConsertaPraMim</p>
          <h1 className="text-2xl font-semibold leading-tight">Admin Mobile Compacto</h1>
          <p className="text-sm text-blue-100/80">Inicializando modulo operacional...</p>
        </div>
      </div>
    </div>
  );
};

export default SplashScreen;
