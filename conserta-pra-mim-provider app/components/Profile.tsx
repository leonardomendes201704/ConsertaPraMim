import React from 'react';
import { ProviderAuthSession } from '../types';

interface Props {
  session: ProviderAuthSession | null;
  onBack: () => void;
  onLogout: () => void;
}

const Profile: React.FC<Props> = ({ session, onBack, onLogout }) => {
  return (
    <div className="min-h-screen bg-[#f4f7fb] pb-8">
      <header className="bg-white border-b border-[#e4e7ec] sticky top-0 z-10">
        <div className="max-w-md mx-auto px-4 py-4">
          <button type="button" onClick={onBack} className="text-sm font-semibold text-[#344054] flex items-center gap-1">
            <span className="material-symbols-outlined text-base">arrow_back</span>
            Voltar
          </button>
        </div>
      </header>

      <main className="max-w-md mx-auto px-4 py-5">
        <section className="rounded-2xl bg-white border border-[#e4e7ec] p-5 shadow-sm">
          <h1 className="text-xl font-bold text-[#101828]">Perfil do prestador</h1>
          <p className="text-sm text-[#667085] mt-1">Dados da sessao atual no app.</p>

          <div className="mt-4 space-y-2 text-sm">
            <p><span className="font-semibold text-[#344054]">Nome:</span> {session?.userName || '-'}</p>
            <p><span className="font-semibold text-[#344054]">E-mail:</span> {session?.email || '-'}</p>
            <p><span className="font-semibold text-[#344054]">Perfil:</span> {session?.role || '-'}</p>
          </div>

          <button
            type="button"
            onClick={onLogout}
            className="mt-6 w-full rounded-xl bg-red-600 text-white py-3 font-bold"
          >
            Sair da conta
          </button>
        </section>
      </main>
    </div>
  );
};

export default Profile;
