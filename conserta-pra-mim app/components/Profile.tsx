import React, { useEffect, useMemo, useRef, useState } from 'react';
import {
  fetchClientProfile,
  resolveProfilePictureUrl,
  updateClientProfile,
  updateClientProfilePicture,
  uploadClientProfilePicture
} from '../services/profile';
import { CLIENT_PJ_TYPE_OPTIONS, CLIENT_PROFILE_TYPES } from '../constants/clientProfile';

interface Props {
  authToken?: string;
  userName?: string;
  userEmail?: string;
  onUserNameUpdated?: (nextName: string) => void;
  onBack: () => void;
  onLogout: () => void;
  onGoToHome: () => void;
  onGoToOrders: () => void;
  onGoToChat: () => void;
}

function getInitials(name: string): string {
  const normalized = String(name || '').trim();
  if (!normalized) {
    return 'CP';
  }

  const parts = normalized.split(/\s+/).filter(Boolean);
  if (parts.length === 1) {
    return parts[0].slice(0, 2).toUpperCase();
  }

  return `${parts[0][0] || ''}${parts[1][0] || ''}`.toUpperCase();
}

function formatPhoneForDisplay(raw: string): string {
  const digits = String(raw || '').replace(/\D/g, '');
  if (!digits) {
    return 'Nao informado';
  }

  if (digits.length === 10) {
    return `(${digits.slice(0, 2)}) ${digits.slice(2, 6)}-${digits.slice(6)}`;
  }

  if (digits.length >= 11) {
    return `(${digits.slice(0, 2)}) ${digits.slice(2, 7)}-${digits.slice(7, 11)}`;
  }

  return digits;
}

function normalizeClientPjType(value: number | '' | null | undefined): number | null {
  const numeric = Number(value);
  if (!Number.isFinite(numeric) || numeric <= 0) {
    return null;
  }

  return numeric;
}

const Profile: React.FC<Props> = ({
  authToken,
  userName,
  userEmail,
  onUserNameUpdated,
  onBack,
  onLogout,
  onGoToHome,
  onGoToOrders,
  onGoToChat
}) => {
  const initialName = String(userName || '').trim() || 'Cliente';
  const [name, setName] = useState(initialName);
  const [savedName, setSavedName] = useState(initialName);
  const [email, setEmail] = useState(String(userEmail || '').trim() || 'cliente@exemplo.com');
  const [phone, setPhone] = useState('Nao informado');
  const [clientProfileType, setClientProfileType] = useState<number>(CLIENT_PROFILE_TYPES.PF);
  const [savedClientProfileType, setSavedClientProfileType] = useState<number>(CLIENT_PROFILE_TYPES.PF);
  const [clientPjType, setClientPjType] = useState<number | ''>('');
  const [savedClientPjType, setSavedClientPjType] = useState<number | ''>('');
  const [profilePictureUrl, setProfilePictureUrl] = useState('');
  const [loadingProfile, setLoadingProfile] = useState(false);
  const [savingName, setSavingName] = useState(false);
  const [updatingPicture, setUpdatingPicture] = useState(false);
  const [feedbackError, setFeedbackError] = useState('');
  const [feedbackSuccess, setFeedbackSuccess] = useState('');
  const [periods, setPeriods] = useState({
    manha: true,
    tarde: true,
    noite: false
  });

  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const isProfileDirty = useMemo(() => {
    const nameChanged = name.trim() !== savedName.trim();
    const profileTypeChanged = clientProfileType !== savedClientProfileType;
    const clientPjTypeChanged =
      normalizeClientPjType(clientPjType) !== normalizeClientPjType(savedClientPjType);

    return nameChanged || profileTypeChanged || clientPjTypeChanged;
  }, [clientPjType, clientProfileType, name, savedClientPjType, savedClientProfileType, savedName]);
  const initials = useMemo(() => getInitials(name), [name]);

  useEffect(() => {
    if (!authToken) {
      return;
    }

    let cancelled = false;
    setLoadingProfile(true);
    setFeedbackError('');

    void fetchClientProfile(authToken)
      .then((profile) => {
        if (cancelled) {
          return;
        }

        const normalizedName = String(profile.name || '').trim() || 'Cliente';
        setName(normalizedName);
        setSavedName(normalizedName);
        setEmail(String(profile.email || '').trim() || 'cliente@exemplo.com');
        setPhone(formatPhoneForDisplay(profile.phone));
        const nextClientProfileType = Number(profile.clientProfileType) === CLIENT_PROFILE_TYPES.PJ
          ? CLIENT_PROFILE_TYPES.PJ
          : CLIENT_PROFILE_TYPES.PF;
        const nextClientPjType = normalizeClientPjType(profile.clientPjType);
        setClientProfileType(nextClientProfileType);
        setSavedClientProfileType(nextClientProfileType);
        setClientPjType(nextClientPjType ?? '');
        setSavedClientPjType(nextClientPjType ?? '');
        setProfilePictureUrl(resolveProfilePictureUrl(profile.profilePictureUrl));
        onUserNameUpdated?.(normalizedName);
      })
      .catch((error) => {
        if (cancelled) {
          return;
        }

        setFeedbackError(error instanceof Error ? error.message : 'Nao foi possivel carregar seus dados.');
      })
      .finally(() => {
        if (!cancelled) {
          setLoadingProfile(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [authToken, onUserNameUpdated]);

  const handleSaveName = async () => {
    if (!authToken || !isProfileDirty || savingName) {
      return;
    }

    const normalizedName = name.trim();
    if (!normalizedName) {
      setFeedbackError('Informe um nome valido para salvar.');
      return;
    }

    setSavingName(true);
    setFeedbackError('');
    setFeedbackSuccess('');

    try {
      if (clientProfileType === CLIENT_PROFILE_TYPES.PJ && !normalizeClientPjType(clientPjType)) {
        setFeedbackError('Selecione o tipo de cliente PJ para salvar.');
        return;
      }

      const updatedProfile = await updateClientProfile(authToken, {
        name: normalizedName,
        clientProfileType,
        clientPjType: clientProfileType === CLIENT_PROFILE_TYPES.PJ
          ? (normalizeClientPjType(clientPjType) || undefined)
          : undefined
      });
      const nextName = String(updatedProfile.name || normalizedName).trim() || normalizedName;
      setName(nextName);
      setSavedName(nextName);
      setEmail(String(updatedProfile.email || email).trim() || email);
      setPhone(formatPhoneForDisplay(updatedProfile.phone));
      const nextClientProfileType = Number(updatedProfile.clientProfileType) === CLIENT_PROFILE_TYPES.PJ
        ? CLIENT_PROFILE_TYPES.PJ
        : CLIENT_PROFILE_TYPES.PF;
      const nextClientPjType = normalizeClientPjType(updatedProfile.clientPjType);
      setClientProfileType(nextClientProfileType);
      setSavedClientProfileType(nextClientProfileType);
      setClientPjType(nextClientPjType ?? '');
      setSavedClientPjType(nextClientPjType ?? '');
      setProfilePictureUrl(resolveProfilePictureUrl(updatedProfile.profilePictureUrl));
      setFeedbackSuccess('Perfil atualizado com sucesso.');
      onUserNameUpdated?.(nextName);
    } catch (error) {
      setFeedbackError(error instanceof Error ? error.message : 'Nao foi possivel salvar seu nome.');
    } finally {
      setSavingName(false);
    }
  };

  const openPicturePicker = () => {
    if (!authToken || updatingPicture) {
      return;
    }

    fileInputRef.current?.click();
  };

  const handlePictureSelected = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const selectedFile = event.target.files?.[0];
    event.target.value = '';

    if (!authToken || !selectedFile) {
      return;
    }

    setUpdatingPicture(true);
    setFeedbackError('');
    setFeedbackSuccess('');

    try {
      const uploadedUrl = await uploadClientProfilePicture(authToken, selectedFile);
      await updateClientProfilePicture(authToken, uploadedUrl);
      setProfilePictureUrl(resolveProfilePictureUrl(uploadedUrl));
      setFeedbackSuccess('Foto atualizada com sucesso.');
    } catch (error) {
      setFeedbackError(error instanceof Error ? error.message : 'Nao foi possivel atualizar sua foto.');
    } finally {
      setUpdatingPicture(false);
    }
  };

  const handleRemovePicture = async () => {
    if (!authToken || updatingPicture || !profilePictureUrl) {
      return;
    }

    setUpdatingPicture(true);
    setFeedbackError('');
    setFeedbackSuccess('');

    try {
      await updateClientProfilePicture(authToken, '');
      setProfilePictureUrl('');
      setFeedbackSuccess('Foto removida. Exibindo iniciais do nome.');
    } catch (error) {
      setFeedbackError(error instanceof Error ? error.message : 'Nao foi possivel remover sua foto.');
    } finally {
      setUpdatingPicture(false);
    }
  };

  const togglePeriod = (key: keyof typeof periods) => {
    setPeriods((prev) => ({ ...prev, [key]: !prev[key] }));
  };

  return (
    <div className="flex flex-col h-screen bg-background-light overflow-hidden">
      <header className="bg-white px-4 pt-6 pb-4 sticky top-0 z-20 border-b border-primary/10 flex items-center justify-between">
        <button onClick={onBack} className="p-2 -ml-2 text-primary hover:bg-primary/5 rounded-full transition-colors">
          <span className="material-symbols-outlined">arrow_back</span>
        </button>
        <h1 className="text-lg font-bold text-[#101818]">Meu Perfil</h1>
        <button
          type="button"
          onClick={() => void handleSaveName()}
          disabled={!authToken || savingName || !isProfileDirty}
          className="text-primary text-sm font-bold disabled:text-primary/40 disabled:cursor-not-allowed"
        >
          {savingName ? 'Salvando...' : 'Salvar'}
        </button>
      </header>

      <div className="flex-1 overflow-y-auto no-scrollbar pb-24">
        <section className="bg-white p-6 flex flex-col items-center border-b border-primary/5">
          <div className="relative">
            <div className="size-24 rounded-full border-4 border-primary/10 overflow-hidden bg-primary/10 flex items-center justify-center">
              {profilePictureUrl ? (
                <img src={profilePictureUrl} alt={name} className="w-full h-full object-cover" />
              ) : (
                <span className="text-2xl font-bold text-primary tracking-wide">{initials}</span>
              )}
            </div>
            <button
              type="button"
              onClick={openPicturePicker}
              disabled={!authToken || updatingPicture}
              className="absolute bottom-0 right-0 size-8 bg-primary text-white rounded-full flex items-center justify-center shadow-md border-2 border-white disabled:opacity-60 disabled:cursor-not-allowed"
            >
              <span className="material-symbols-outlined text-sm">photo_camera</span>
            </button>
            <input
              ref={fileInputRef}
              type="file"
              accept="image/jpeg,image/png,image/webp"
              className="hidden"
              onChange={(event) => void handlePictureSelected(event)}
            />
          </div>
          <h2 className="mt-4 text-xl font-bold text-[#101818]">{name}</h2>
          <p className="text-sm text-[#5e8d8d]">
            {updatingPicture ? 'Atualizando foto...' : 'Membro da plataforma'}
          </p>
          {profilePictureUrl ? (
            <button
              type="button"
              onClick={() => void handleRemovePicture()}
              disabled={!authToken || updatingPicture}
              className="mt-3 text-xs font-bold text-red-600 disabled:text-red-300 disabled:cursor-not-allowed"
            >
              Remover foto
            </button>
          ) : null}
        </section>

        {loadingProfile ? (
          <section className="px-4 pt-4">
            <div className="rounded-xl border border-primary/10 bg-white px-3 py-2 text-xs text-[#5e8d8d]">
              Carregando dados do perfil...
            </div>
          </section>
        ) : null}

        {feedbackError ? (
          <section className="px-4 pt-4">
            <div className="rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">
              {feedbackError}
            </div>
          </section>
        ) : null}

        {feedbackSuccess ? (
          <section className="px-4 pt-4">
            <div className="rounded-xl border border-emerald-200 bg-emerald-50 px-3 py-2 text-xs text-emerald-700">
              {feedbackSuccess}
            </div>
          </section>
        ) : null}

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
              <label className="text-[10px] font-bold text-primary uppercase ml-1">Tipo de cliente</label>
              <select
                value={clientProfileType}
                onChange={(event) => {
                  const nextType = Number(event.target.value) === CLIENT_PROFILE_TYPES.PJ
                    ? CLIENT_PROFILE_TYPES.PJ
                    : CLIENT_PROFILE_TYPES.PF;
                  setClientProfileType(nextType);
                  if (nextType !== CLIENT_PROFILE_TYPES.PJ) {
                    setClientPjType('');
                  }
                }}
                className="w-full h-11 bg-background-light border-none rounded-xl px-4 text-sm focus:ring-2 focus:ring-primary/20 text-[#101828]"
              >
                <option value={CLIENT_PROFILE_TYPES.PF}>PF - Pessoa Fisica</option>
                <option value={CLIENT_PROFILE_TYPES.PJ}>PJ - Pessoa Juridica</option>
              </select>
            </div>
            {clientProfileType === CLIENT_PROFILE_TYPES.PJ ? (
              <div className="space-y-1">
                <label className="text-[10px] font-bold text-primary uppercase ml-1">Segmento PJ</label>
                <select
                  value={clientPjType}
                  onChange={(event) => {
                    const nextValue = Number(event.target.value);
                    setClientPjType(Number.isFinite(nextValue) && nextValue > 0 ? nextValue : '');
                  }}
                  className="w-full h-11 bg-background-light border-none rounded-xl px-4 text-sm focus:ring-2 focus:ring-primary/20 text-[#101828]"
                >
                  <option value="">Selecione um segmento</option>
                  {CLIENT_PJ_TYPE_OPTIONS.map((option) => (
                    <option key={option.value} value={option.value}>
                      {option.label}
                    </option>
                  ))}
                </select>
              </div>
            ) : null}
            <div className="space-y-1">
              <label className="text-[10px] font-bold text-primary uppercase ml-1">E-mail</label>
              <input
                type="email"
                value={email}
                disabled
                className="w-full h-11 bg-slate-100 border-none rounded-xl px-4 text-sm text-[#64748b] cursor-not-allowed"
              />
            </div>
            <div className="space-y-1">
              <label className="text-[10px] font-bold text-primary uppercase ml-1">Telefone</label>
              <input
                type="text"
                value={phone}
                disabled
                className="w-full h-11 bg-slate-100 border-none rounded-xl px-4 text-sm text-[#64748b] cursor-not-allowed"
              />
            </div>
          </div>
        </section>

        <section className="p-4 space-y-4">
          <h3 className="text-xs font-bold text-[#5e8d8d] uppercase tracking-wider ml-1">Preferencias de Atendimento</h3>
          <div className="bg-white rounded-2xl border border-primary/5 shadow-sm p-4">
            <p className="text-xs text-[#5e8d8d] mb-4">Escolha os periodos em que costuma estar disponivel.</p>
            <div className="flex gap-2">
              <button
                type="button"
                onClick={() => togglePeriod('manha')}
                className={`flex-1 py-3 rounded-xl border-2 font-bold text-[10px] uppercase transition-all flex flex-col items-center gap-1 ${
                  periods.manha ? 'bg-primary border-primary text-white shadow-lg shadow-primary/20' : 'bg-white border-primary/5 text-[#5e8d8d]'
                }`}
              >
                <span className="material-symbols-outlined">light_mode</span>
                Manha
              </button>
              <button
                type="button"
                onClick={() => togglePeriod('tarde')}
                className={`flex-1 py-3 rounded-xl border-2 font-bold text-[10px] uppercase transition-all flex flex-col items-center gap-1 ${
                  periods.tarde ? 'bg-primary border-primary text-white shadow-lg shadow-primary/20' : 'bg-white border-primary/5 text-[#5e8d8d]'
                }`}
              >
                <span className="material-symbols-outlined">sunny</span>
                Tarde
              </button>
              <button
                type="button"
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

        <section className="p-4 mb-10">
          <button
            type="button"
            onClick={onLogout}
            className="w-full h-14 bg-red-50 text-red-600 rounded-2xl font-bold flex items-center justify-center gap-2 border border-red-100 hover:bg-red-100 transition-colors"
          >
            <span className="material-symbols-outlined">logout</span>
            Sair da conta
          </button>
        </section>
      </div>

      <nav className="fixed bottom-0 left-0 right-0 z-50 bg-white border-t border-primary/10 px-4 pb-4 pt-2 max-w-md mx-auto">
        <div className="flex items-center justify-between mb-2">
          <NavItem icon="home" label="Inicio" onClick={onGoToHome} />
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
