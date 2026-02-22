import React, { useMemo, useState } from 'react';
import type { AdminAppView, AdminHomeTab } from '../types';

interface AppShellProps {
  initialView: Exclude<AdminAppView, 'SPLASH'>;
}

const TAB_ITEMS: Array<{ id: AdminHomeTab; label: string; icon: string }> = [
  { id: 'dashboard', label: 'Painel', icon: 'dashboard' },
  { id: 'monitoring', label: 'Monitorar', icon: 'monitoring' },
  { id: 'support', label: 'Chamados', icon: 'support_agent' },
  { id: 'settings', label: 'Conta', icon: 'manage_accounts' }
];

const AppShell: React.FC<AppShellProps> = ({ initialView }) => {
  const [view, setView] = useState<Exclude<AdminAppView, 'SPLASH'>>(initialView);
  const [activeTab, setActiveTab] = useState<AdminHomeTab>('dashboard');

  const tabTitle = useMemo(() => {
    const match = TAB_ITEMS.find((item) => item.id === activeTab);
    return match?.label || 'Painel';
  }, [activeTab]);

  if (view === 'AUTH') {
    return (
      <div className="min-h-screen bg-slate-950 px-6 py-8 text-slate-100">
        <div className="mx-auto flex min-h-[80vh] max-w-md flex-col justify-center gap-6">
          <div className="rounded-3xl border border-slate-700 bg-slate-900/80 p-6 shadow-2xl shadow-black/40">
            <h1 className="text-2xl font-semibold">Admin Mobile</h1>
            <p className="mt-2 text-sm text-slate-300">
              Base do app concluida. O fluxo de autenticacao entra na proxima task (ST-029).
            </p>
            <button
              type="button"
              onClick={() => setView('HOME')}
              className="mt-6 w-full rounded-2xl bg-blue-600 px-4 py-3 text-sm font-semibold text-white transition hover:bg-blue-500"
            >
              Acessar shell de navegacao
            </button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-slate-100 text-slate-900">
      <header className="sticky top-0 z-10 border-b border-slate-200 bg-white/95 px-4 py-4 backdrop-blur">
        <div className="mx-auto flex max-w-lg items-center justify-between">
          <div>
            <p className="text-xs uppercase tracking-[0.18em] text-slate-400">Admin Mobile</p>
            <h1 className="text-lg font-semibold">{tabTitle}</h1>
          </div>
          <span className="material-symbols-outlined rounded-full bg-slate-100 p-2 text-slate-700">settings</span>
        </div>
      </header>

      <main className="mx-auto max-w-lg px-4 pb-28 pt-6">
        <section className="rounded-3xl border border-slate-200 bg-white p-6 shadow-sm">
          <h2 className="text-base font-semibold">Estrutura base pronta</h2>
          <p className="mt-2 text-sm text-slate-600">
            Esta area sera preenchida pelos modulos de autenticacao, dashboard, monitoramento e atendimento nas proximas tasks.
          </p>
          <div className="mt-4 grid grid-cols-2 gap-3 text-xs text-slate-500">
            <div className="rounded-xl border border-slate-200 bg-slate-50 p-3">Tab ativa: {tabTitle}</div>
            <div className="rounded-xl border border-slate-200 bg-slate-50 p-3">Estado: bootstrap</div>
          </div>
        </section>
      </main>

      <nav className="fixed bottom-0 left-0 right-0 border-t border-slate-200 bg-white/95 px-2 py-2 backdrop-blur">
        <div className="mx-auto flex max-w-lg items-stretch justify-between gap-1">
          {TAB_ITEMS.map((item) => {
            const isActive = item.id === activeTab;
            return (
              <button
                key={item.id}
                type="button"
                onClick={() => setActiveTab(item.id)}
                className={`flex min-w-0 flex-1 flex-col items-center rounded-xl px-2 py-2 text-[11px] font-medium transition ${
                  isActive ? 'bg-blue-50 text-blue-700' : 'text-slate-500 hover:bg-slate-50'
                }`}
              >
                <span className={`material-symbols-outlined text-[20px] ${isActive ? 'material-symbols-fill' : ''}`}>
                  {item.icon}
                </span>
                <span className="truncate">{item.label}</span>
              </button>
            );
          })}
        </div>
      </nav>
    </div>
  );
};

export default AppShell;
