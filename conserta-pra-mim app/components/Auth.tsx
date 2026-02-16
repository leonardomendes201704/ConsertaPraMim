
import React, { useState } from 'react';

interface Props {
  onLogin: () => void;
  onBack: () => void;
}

const Auth: React.FC<Props> = ({ onLogin, onBack }) => {
  const [phone, setPhone] = useState('');

  const formatPhoneNumber = (value: string) => {
    const digits = value.replace(/\D/g, '');
    let formatted = digits;
    if (digits.length > 0) {
      formatted = '(' + digits;
    }
    if (digits.length > 2) {
      formatted = '(' + digits.slice(0, 2) + ') ' + digits.slice(2);
    }
    if (digits.length > 7) {
      formatted = '(' + digits.slice(0, 2) + ') ' + digits.slice(2, 7) + '-' + digits.slice(7, 11);
    }
    return formatted.slice(0, 15);
  };

  const handlePhoneChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const formatted = formatPhoneNumber(e.target.value);
    setPhone(formatted);
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onLogin();
  };

  return (
    <div className="flex h-screen flex-col items-center justify-center p-4 bg-background-light">
      <div className="w-full max-w-[440px] bg-white rounded-2xl shadow-xl border border-primary/5 overflow-hidden">
        <div className="flex items-center p-4 border-b border-primary/5">
          <button onClick={onBack} className="text-primary hover:bg-primary/5 p-2 rounded-full transition-colors">
            <span className="material-symbols-outlined">arrow_back</span>
          </button>
          <h2 className="text-[#101818] text-base font-bold flex-1 text-center pr-10">Conserta Pra Mim</h2>
        </div>
        <div className="p-8">
          <div className="mb-8">
            <h1 className="text-[#101818] text-3xl font-bold mb-3">Bem-vindo!</h1>
            <p className="text-[#4a5e5e] text-base font-normal leading-relaxed">
              Digite seu número de celular para entrar ou se cadastrar.
            </p>
          </div>
          <form onSubmit={handleSubmit} className="space-y-6">
            <div className="space-y-2">
              <label className="text-[#101818] text-sm font-semibold ml-1">Número de celular</label>
              <div className="relative flex w-full items-stretch rounded-xl border border-[#dae7e7] focus-within:ring-2 focus-within:ring-primary/20 transition-all overflow-hidden">
                <div className="flex items-center gap-2 px-4 border-r border-[#dae7e7] bg-background-light shrink-0">
                  <div className="w-6 h-4 overflow-hidden rounded-sm shadow-sm">
                    <svg viewBox="0 0 720 504" className="w-full h-full">
                      <rect width="720" height="504" fill="#009b3a"/>
                      <path d="M360 48L672 252L360 456L48 252L360 48Z" fill="#fedf00"/>
                      <circle cx="360" cy="252" r="105" fill="#002776"/>
                      <path d="M255 252C255 264 260 275 268 284C286 276 312 268 360 268C408 268 434 276 452 284C460 275 465 264 465 252C465 240 460 229 452 220C434 228 408 236 360 236C312 236 286 228 268 220C260 229 255 240 255 252Z" fill="white"/>
                    </svg>
                  </div>
                  <span className="text-[#101818] font-medium">+55</span>
                </div>
                <input 
                  type="tel" 
                  value={phone}
                  onChange={handlePhoneChange}
                  className="flex-1 h-14 border-0 focus:ring-0 px-4 text-base bg-white placeholder:text-[#5e8d8d]" 
                  placeholder="(11) 99999-9999" 
                  required 
                />
              </div>
            </div>
            <button className="w-full flex items-center justify-center rounded-xl h-14 bg-primary hover:bg-primary/90 text-white font-bold transition-all shadow-lg shadow-primary/20 active:scale-[0.98]">
              Enviar Código
            </button>
          </form>
          <div className="mt-8 text-[#5e8d8d] text-xs text-center">
            Ao continuar, você concorda com nossos <a href="#" className="text-primary font-semibold underline">Termos</a> e <a href="#" className="text-primary font-semibold underline">Privacidade</a>.
          </div>
        </div>
      </div>
      <div className="mt-8 text-[#5e8d8d] text-sm flex items-center gap-2">
        <span className="material-symbols-outlined text-sm">verified_user</span>
        <span>Ambiente seguro e criptografado</span>
      </div>
    </div>
  );
};

export default Auth;
