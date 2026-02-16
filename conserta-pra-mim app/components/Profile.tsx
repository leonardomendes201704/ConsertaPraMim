
import React, { useState } from 'react';

interface Props {
  onBack: () => void;
  onLogout: () => void;
  onGoToHome: () => void;
  onGoToOrders: () => void;
  onGoToChat: () => void;
}

const Profile: React.FC<Props> = ({ onBack, onLogout, onGoToHome, onGoToOrders, onGoToChat }) => {
  const [name, setName] = useState('João Silva');
  const [email, setEmail] = useState('joao.silva@exemplo.com');
  const [phone, setPhone] = useState('(11) 98765-4321');
  const [cep, setCep] = useState('01310-100');
  const [address, setAddress] = useState('Avenida Paulista, 1000 - São Paulo, SP');
  const [isSearchingCep, setIsSearchingCep] = useState(false);
  
  const [periods, setPeriods] = useState({
    manha: true,
    tarde: true,
    noite: false
  });

  const formatPhoneNumber = (value: string) => {
    const digits = value.replace(/\D/g, '');
    let formatted = digits;
    if (digits.length > 0) formatted = '(' + digits;
    if (digits.length > 2) formatted = '(' + digits.slice(0, 2) + ') ' + digits.slice(2);
    if (digits.length > 7) formatted = '(' + digits.slice(0, 2) + ') ' + digits.slice(2, 7) + '-' + digits.slice(7, 11);
    return formatted.slice(0, 15);
  };

  const handlePhoneChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setPhone(formatPhoneNumber(e.target.value));
  };

  const handleCepLookup = () => {
    setIsSearchingCep(true);
    // Simulação de busca de CEP
    setTimeout(() => {
      setAddress('Avenida Paulista, 1000 - São Paulo, SP (Localizado via CEP)');
      setIsSearchingCep(false);
    }, 1200);
  };

  const togglePeriod = (key: keyof typeof periods) => {
    setPeriods(prev => ({ ...prev, [key]: !prev[key] }));
  };

  return (
    <div className="flex flex-col h-screen bg-background-light overflow-hidden">
      {/* Header */}
      <header className="bg-white px-4 pt-6 pb-4 sticky top-0 z-20 border-b border-primary/10 flex items-center justify-between">
        <button onClick={onBack} className="p-2 -ml-2 text-primary hover:bg-primary/5 rounded-full transition-colors">
          <span className="material-symbols-outlined">arrow_back</span>
        </button>
        <h1 className="text-lg font-bold text-[#101818]">Meu Perfil</h1>
        <button className="text-primary text-sm font-bold">Salvar</button>
      </header>

      <div className="flex-1 overflow-y-auto no-scrollbar pb-24">
        {/* Avatar Section */}
        <section className="bg-white p-6 flex flex-col items-center border-b border-primary/5">
          <div className="relative group cursor-pointer">
            <div className="size-24 rounded-full border-4 border-primary/10 overflow-hidden">
              <img src="https://i.pravatar.cc/150?u=joao" alt="Profile" className="w-full h-full object-cover" />
            </div>
            <div className="absolute bottom-0 right-0 size-8 bg-primary text-white rounded-full flex items-center justify-center shadow-md border-2 border-white">
              <span className="material-symbols-outlined text-sm">photo_camera</span>
            </div>
          </div>
          <h2 className="mt-4 text-xl font-bold text-[#101818]">{name}</h2>
          <p className="text-sm text-[#5e8d8d]">Membro desde Outubro 2024</p>
        </section>

        {/* Personal Info */}
        <section className="p-4 space-y-4">
          <h3 className="text-xs font-bold text-[#5e8d8d] uppercase tracking-wider ml-1">Dados Pessoais</h3>
          <div className="bg-white rounded-2xl border border-primary/5 shadow-sm p-4 space-y-4">
            <div className="space-y-1">
              <label className="text-[10px] font-bold text-primary uppercase ml-1">Nome Completo</label>
              <input 
                type="text" 
                value={name} 
                onChange={(e) => setName(e.target.value)}
                className="w-full h-11 bg-background-light border-none rounded-xl px-4 text-sm focus:ring-2 focus:ring-primary/20"
              />
            </div>
            <div className="space-y-1">
              <label className="text-[10px] font-bold text-primary uppercase ml-1">E-mail</label>
              <input 
                type="email" 
                value={email} 
                onChange={(e) => setEmail(e.target.value)}
                className="w-full h-11 bg-background-light border-none rounded-xl px-4 text-sm focus:ring-2 focus:ring-primary/20"
              />
            </div>
            <div className="space-y-1">
              <label className="text-[10px] font-bold text-primary uppercase ml-1">Telefone</label>
              <input 
                type="tel" 
                value={phone} 
                onChange={handlePhoneChange}
                className="w-full h-11 bg-background-light border-none rounded-xl px-4 text-sm focus:ring-2 focus:ring-primary/20"
              />
            </div>
          </div>
        </section>

        {/* Address Info */}
        <section className="p-4 space-y-4">
          <h3 className="text-xs font-bold text-[#5e8d8d] uppercase tracking-wider ml-1">Localização</h3>
          <div className="bg-white rounded-2xl border border-primary/5 shadow-sm p-4 space-y-4">
            <div className="space-y-1">
              <label className="text-[10px] font-bold text-primary uppercase ml-1">CEP</label>
              <div className="flex gap-2">
                <input 
                  type="text" 
                  value={cep} 
                  onChange={(e) => setCep(e.target.value)}
                  className="flex-1 h-11 bg-background-light border-none rounded-xl px-4 text-sm focus:ring-2 focus:ring-primary/20"
                />
                <button 
                  onClick={handleCepLookup}
                  className="px-4 bg-primary/10 text-primary rounded-xl font-bold text-xs flex items-center gap-2 active:scale-95 transition-all"
                >
                  {isSearchingCep ? '...' : 'Buscar'}
                  <span className="material-symbols-outlined text-sm">location_on</span>
                </button>
              </div>
            </div>
            <div className="space-y-1">
              <label className="text-[10px] font-bold text-primary uppercase ml-1">Endereço Atual</label>
              <div className="bg-background-light p-3 rounded-xl min-h-[44px] flex items-start gap-2">
                <span className="material-symbols-outlined text-primary text-sm mt-0.5">map</span>
                <p className="text-xs text-[#101818] leading-relaxed">{address}</p>
              </div>
            </div>
          </div>
        </section>

        {/* Preferences */}
        <section className="p-4 space-y-4">
          <h3 className="text-xs font-bold text-[#5e8d8d] uppercase tracking-wider ml-1">Preferências de Atendimento</h3>
          <div className="bg-white rounded-2xl border border-primary/5 shadow-sm p-4">
            <p className="text-xs text-[#5e8d8d] mb-4">Escolha os períodos que você costuma estar disponível para receber prestadores:</p>
            <div className="flex gap-2">
              <button 
                onClick={() => togglePeriod('manha')}
                className={`flex-1 py-3 rounded-xl border-2 font-bold text-[10px] uppercase transition-all flex flex-col items-center gap-1 ${
                  periods.manha ? 'bg-primary border-primary text-white shadow-lg shadow-primary/20' : 'bg-white border-primary/5 text-[#5e8d8d]'
                }`}
              >
                <span className="material-symbols-outlined">light_mode</span>
                Manhã
              </button>
              <button 
                onClick={() => togglePeriod('tarde')}
                className={`flex-1 py-3 rounded-xl border-2 font-bold text-[10px] uppercase transition-all flex flex-col items-center gap-1 ${
                  periods.tarde ? 'bg-primary border-primary text-white shadow-lg shadow-primary/20' : 'bg-white border-primary/5 text-[#5e8d8d]'
                }`}
              >
                <span className="material-symbols-outlined">sunny</span>
                Tarde
              </button>
              <button 
                onClick={() => togglePeriod('noite')}
                className={`flex-1 py-3 rounded-xl border-2 font-bold text-[10px] uppercase transition-all flex flex-col items-center gap-1 ${
                  periods.noite ? 'bg-primary border-primary text-white shadow-lg shadow-primary/20' : 'bg-white border-primary/5 text-[#5e8d8d]'
                }`}
              >
                <span className="material-symbols-outlined">dark_mode</span>
                Noite
              </button>
            </div>
          </div>
        </section>

        {/* Danger Zone */}
        <section className="p-4 mb-10">
          <button 
            onClick={onLogout}
            className="w-full h-14 bg-red-50 text-red-600 rounded-2xl font-bold flex items-center justify-center gap-2 border border-red-100 hover:bg-red-100 transition-colors"
          >
            <span className="material-symbols-outlined">logout</span>
            Sair da conta
          </button>
        </section>
      </div>

      {/* Navigation Footer */}
      <nav className="fixed bottom-0 left-0 right-0 z-50 bg-white border-t border-primary/10 px-4 pb-4 pt-2 max-w-md mx-auto">
        <div className="flex items-center justify-between mb-2">
          <NavItem icon="home" label="Início" onClick={onGoToHome} />
          <NavItem icon="assignment" label="Pedidos" onClick={onGoToOrders} />
          <NavItem icon="chat_bubble" label="Chat" onClick={onGoToChat} />
          <NavItem active icon="person" label="Perfil" />
        </div>
        <p className="text-center text-[8px] font-bold text-primary/30 tracking-widest uppercase">
          Powered by DevCfrat Studio
        </p>
      </nav>
    </div>
  );
};

const NavItem: React.FC<{ icon: string; label: string; active?: boolean; onClick?: () => void }> = ({ icon, label, active, onClick }) => (
  <button 
    onClick={onClick}
    className={`flex flex-col items-center gap-1 ${active ? 'text-primary' : 'text-[#5e8d8d]'} active:scale-95 transition-transform`}
  >
    <div className="flex h-8 items-center justify-center">
      <span className={`material-symbols-outlined text-[28px] ${active ? 'material-symbols-fill' : ''}`}>{icon}</span>
    </div>
    <p className={`text-[10px] leading-normal tracking-wide ${active ? 'font-bold' : 'font-medium'}`}>{label}</p>
  </button>
);

export default Profile;
