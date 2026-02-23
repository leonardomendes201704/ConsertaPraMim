import React, { useEffect, useMemo, useRef, useState } from 'react';
import {
  ClientProfileLegalTermsStatus,
  acceptClientProfileLegalTerms,
  fetchClientProfile,
  fetchClientProfileLegalTermsStatus,
  resolveClientProfileCurrentLocation,
  resolveClientProfileZip,
  resolveProfilePictureUrl,
  updateClientProfile,
  updateClientProfilePicture,
  uploadClientProfilePicture
} from '../services/profile';
import { CLIENT_PJ_TYPE_OPTIONS, CLIENT_PROFILE_TYPES } from '../constants/clientProfile';
import jsPDF from 'jspdf';

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

function onlyDigits(value: string): string {
  return String(value || '').replace(/\D/g, '');
}

function formatZip(value: string): string {
  const digits = onlyDigits(value).slice(0, 8);
  if (digits.length <= 5) {
    return digits;
  }

  return `${digits.slice(0, 5)}-${digits.slice(5)}`;
}

function normalizeOptionalText(value: string): string {
  return String(value || '').trim();
}

function coordinatesAreEqual(first: number | null, second: number | null): boolean {
  if (first === null && second === null) {
    return true;
  }

  if (first === null || second === null) {
    return false;
  }

  return Math.abs(first - second) <= 0.0000005;
}

function buildOsmEmbedUrl(latitude: number, longitude: number): string {
  const delta = 0.006;
  const left = (longitude - delta).toFixed(6);
  const right = (longitude + delta).toFixed(6);
  const bottom = (latitude - delta).toFixed(6);
  const top = (latitude + delta).toFixed(6);
  const marker = `${latitude.toFixed(6)},${longitude.toFixed(6)}`;
  return `https://www.openstreetmap.org/export/embed.html?bbox=${left}%2C${bottom}%2C${right}%2C${top}&layer=mapnik&marker=${encodeURIComponent(marker)}`;
}

function getCurrentPosition(): Promise<GeolocationPosition> {
  return new Promise((resolve, reject) => {
    if (!navigator.geolocation) {
      reject(new Error('Geolocalizacao nao suportada neste dispositivo.'));
      return;
    }

    navigator.geolocation.getCurrentPosition(resolve, reject, {
      enableHighAccuracy: true,
      timeout: 12000,
      maximumAge: 0
    });
  });
}

function mapGeolocationError(error: unknown): string {
  const maybeError = error as { code?: number } | undefined;
  switch (maybeError?.code) {
    case 1:
      return 'Permita o acesso a localizacao para usar essa opcao.';
    case 2:
      return 'Localizacao indisponivel no momento.';
    case 3:
      return 'Tempo esgotado ao tentar obter localizacao atual.';
    default:
      return 'Nao foi possivel obter a localizacao atual.';
  }
}

function normalizeClientPjType(value: number | '' | null | undefined): number | null {
  const numeric = Number(value);
  if (!Number.isFinite(numeric) || numeric <= 0) {
    return null;
  }

  return numeric;
}

function formatTermsDate(value?: string | null): string {
  if (!value) {
    return 'nao disponivel';
  }

  const normalized = String(value).trim();
  const hasTimezone = /(?:Z|[+-]\d{2}:\d{2})$/i.test(normalized);
  const parsed = new Date(hasTimezone ? normalized : `${normalized}Z`);
  if (Number.isNaN(parsed.getTime())) {
    return 'nao disponivel';
  }

  return parsed.toLocaleString('pt-BR');
}

function termsHtmlToText(html: string): string {
  const normalizedHtml = String(html || '')
    .replace(/<(br|\/p|\/div|\/li|\/h[1-6])\s*\/?>/gi, '\n')
    .replace(/<li[^>]*>/gi, '- ');

  const container = document.createElement('div');
  container.innerHTML = normalizedHtml;
  return (container.textContent || container.innerText || '')
    .replace(/\u00a0/g, ' ')
    .replace(/\r/g, '')
    .replace(/\n{3,}/g, '\n\n')
    .trim();
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
  const [baseZipCode, setBaseZipCode] = useState('');
  const [savedBaseZipCode, setSavedBaseZipCode] = useState('');
  const [baseStreet, setBaseStreet] = useState('');
  const [savedBaseStreet, setSavedBaseStreet] = useState('');
  const [baseCity, setBaseCity] = useState('');
  const [savedBaseCity, setSavedBaseCity] = useState('');
  const [baseLatitude, setBaseLatitude] = useState<number | null>(null);
  const [savedBaseLatitude, setSavedBaseLatitude] = useState<number | null>(null);
  const [baseLongitude, setBaseLongitude] = useState<number | null>(null);
  const [savedBaseLongitude, setSavedBaseLongitude] = useState<number | null>(null);
  const [profilePictureUrl, setProfilePictureUrl] = useState('');
  const [loadingProfile, setLoadingProfile] = useState(false);
  const [savingName, setSavingName] = useState(false);
  const [updatingPicture, setUpdatingPicture] = useState(false);
  const [locationLoading, setLocationLoading] = useState(false);
  const [locationMode, setLocationMode] = useState<'zip' | 'current' | null>(null);
  const [locationMessage, setLocationMessage] = useState<{ type: 'info' | 'error' | 'success'; text: string } | null>(null);
  const [feedbackError, setFeedbackError] = useState('');
  const [feedbackSuccess, setFeedbackSuccess] = useState('');
  const [termsStatus, setTermsStatus] = useState<ClientProfileLegalTermsStatus | null>(null);
  const [termsLoading, setTermsLoading] = useState(false);
  const [termsError, setTermsError] = useState('');
  const [termsAcceptedCheckbox, setTermsAcceptedCheckbox] = useState(false);
  const [termsAccepting, setTermsAccepting] = useState(false);
  const [showTermsModal, setShowTermsModal] = useState(false);
  const [periods, setPeriods] = useState({
    manha: true,
    tarde: true,
    noite: false
  });

  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const hasLocationCoordinates = baseLatitude !== null && baseLongitude !== null;
  const mapEmbedUrl = useMemo(() => {
    if (!hasLocationCoordinates) {
      return '';
    }

    return buildOsmEmbedUrl(baseLatitude as number, baseLongitude as number);
  }, [baseLatitude, baseLongitude, hasLocationCoordinates]);
  const isProfileDirty = useMemo(() => {
    const nameChanged = name.trim() !== savedName.trim();
    const profileTypeChanged = clientProfileType !== savedClientProfileType;
    const clientPjTypeChanged =
      normalizeClientPjType(clientPjType) !== normalizeClientPjType(savedClientPjType);
    const locationChanged =
      onlyDigits(baseZipCode) !== onlyDigits(savedBaseZipCode) ||
      normalizeOptionalText(baseStreet) !== normalizeOptionalText(savedBaseStreet) ||
      normalizeOptionalText(baseCity) !== normalizeOptionalText(savedBaseCity) ||
      !coordinatesAreEqual(baseLatitude, savedBaseLatitude) ||
      !coordinatesAreEqual(baseLongitude, savedBaseLongitude);

    return nameChanged || profileTypeChanged || clientPjTypeChanged || locationChanged;
  }, [
    baseCity,
    baseLatitude,
    baseLongitude,
    baseStreet,
    baseZipCode,
    clientPjType,
    clientProfileType,
    name,
    savedBaseCity,
    savedBaseLatitude,
    savedBaseLongitude,
    savedBaseStreet,
    savedBaseZipCode,
    savedClientPjType,
    savedClientProfileType,
    savedName
  ]);
  const initials = useMemo(() => getInitials(name), [name]);

  useEffect(() => {
    if (!authToken) {
      return;
    }

    let cancelled = false;
    setLoadingProfile(true);
    setFeedbackError('');
    setTermsLoading(true);
    setTermsError('');

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
        const nextBaseZipCode = formatZip(String(profile.clientBaseZipCode || '').trim());
        const nextBaseStreet = String(profile.clientBaseStreet || '').trim();
        const nextBaseCity = String(profile.clientBaseCity || '').trim();
        const nextBaseLatitude = Number.isFinite(Number(profile.clientBaseLatitude))
          ? Number(profile.clientBaseLatitude)
          : null;
        const nextBaseLongitude = Number.isFinite(Number(profile.clientBaseLongitude))
          ? Number(profile.clientBaseLongitude)
          : null;
        setBaseZipCode(nextBaseZipCode);
        setSavedBaseZipCode(nextBaseZipCode);
        setBaseStreet(nextBaseStreet);
        setSavedBaseStreet(nextBaseStreet);
        setBaseCity(nextBaseCity);
        setSavedBaseCity(nextBaseCity);
        setBaseLatitude(nextBaseLatitude);
        setSavedBaseLatitude(nextBaseLatitude);
        setBaseLongitude(nextBaseLongitude);
        setSavedBaseLongitude(nextBaseLongitude);
        setLocationMessage(null);
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

    void fetchClientProfileLegalTermsStatus(authToken)
      .then((status) => {
        if (cancelled) {
          return;
        }

        setTermsStatus(status);
        setTermsAcceptedCheckbox(Boolean(status.accepted));
      })
      .catch((error) => {
        if (cancelled) {
          return;
        }

        setTermsError(error instanceof Error ? error.message : 'Nao foi possivel carregar o termo de aceite.');
      })
      .finally(() => {
        if (!cancelled) {
          setTermsLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [authToken, onUserNameUpdated]);

  const applyResolvedLocation = (
    location: { zipCode: string; street: string; city: string; latitude: number; longitude: number },
    options?: { keepManualAddress?: boolean }) => {
    const keepManualAddress = options?.keepManualAddress ?? false;
    setBaseZipCode(formatZip(location.zipCode));
    if (!keepManualAddress || !normalizeOptionalText(baseStreet)) {
      setBaseStreet(location.street);
    }
    if (!keepManualAddress || !normalizeOptionalText(baseCity)) {
      setBaseCity(location.city);
    }
    setBaseLatitude(location.latitude);
    setBaseLongitude(location.longitude);
  };

  const handleResolveZip = async (options?: { keepManualAddress?: boolean }): Promise<boolean> => {
    if (!authToken) {
      setLocationMessage({ type: 'error', text: 'Sessao invalida para consultar CEP.' });
      return false;
    }

    const normalizedZip = onlyDigits(baseZipCode);
    if (normalizedZip.length !== 8) {
      setLocationMessage({ type: 'error', text: 'Informe um CEP valido com 8 digitos.' });
      return false;
    }

    setLocationLoading(true);
    setLocationMode('zip');
    setLocationMessage({ type: 'info', text: 'Buscando localizacao do CEP...' });

    try {
      const resolved = await resolveClientProfileZip(authToken, normalizedZip);
      applyResolvedLocation(resolved, { keepManualAddress: options?.keepManualAddress });
      setLocationMessage({ type: 'success', text: 'Localizacao atualizada pelo CEP.' });
      return true;
    } catch (error) {
      setLocationMessage({
        type: 'error',
        text: error instanceof Error ? error.message : 'Nao foi possivel localizar esse CEP.'
      });
      return false;
    } finally {
      setLocationLoading(false);
      setLocationMode(null);
    }
  };

  const handleUseCurrentLocation = async (): Promise<void> => {
    if (!authToken) {
      setLocationMessage({ type: 'error', text: 'Sessao invalida para usar localizacao atual.' });
      return;
    }

    setLocationLoading(true);
    setLocationMode('current');
    setLocationMessage({ type: 'info', text: 'Obtendo localizacao atual...' });

    try {
      const position = await getCurrentPosition();
      const resolved = await resolveClientProfileCurrentLocation(
        authToken,
        position.coords.latitude,
        position.coords.longitude);

      applyResolvedLocation(resolved);
      setLocationMessage({ type: 'success', text: 'Localizacao atual aplicada.' });
    } catch (error) {
      if (error instanceof Error && error.name === 'ClientProfileApiError') {
        setLocationMessage({ type: 'error', text: error.message });
      } else {
        setLocationMessage({ type: 'error', text: mapGeolocationError(error) });
      }
    } finally {
      setLocationLoading(false);
      setLocationMode(null);
    }
  };

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

      const locationChanged =
        onlyDigits(baseZipCode) !== onlyDigits(savedBaseZipCode) ||
        normalizeOptionalText(baseStreet) !== normalizeOptionalText(savedBaseStreet) ||
        normalizeOptionalText(baseCity) !== normalizeOptionalText(savedBaseCity) ||
        !coordinatesAreEqual(baseLatitude, savedBaseLatitude) ||
        !coordinatesAreEqual(baseLongitude, savedBaseLongitude);

      const normalizedZip = onlyDigits(baseZipCode);
      if (locationChanged) {
        if (normalizedZip.length !== 8) {
          setFeedbackError('Informe um CEP valido para salvar a localizacao.');
          return;
        }

        if (baseLatitude === null || baseLongitude === null) {
          const resolved = await handleResolveZip({ keepManualAddress: true });
          if (!resolved) {
            setFeedbackError('Nao foi possivel validar a localizacao. Revise CEP/endereco.');
            return;
          }
        }
      }

      const updatedProfile = await updateClientProfile(authToken, {
        name: normalizedName,
        clientProfileType,
        clientPjType: clientProfileType === CLIENT_PROFILE_TYPES.PJ
          ? (normalizeClientPjType(clientPjType) || undefined)
          : undefined,
        clientBaseZipCode: normalizedZip || undefined,
        clientBaseStreet: normalizeOptionalText(baseStreet) || undefined,
        clientBaseCity: normalizeOptionalText(baseCity) || undefined,
        clientBaseLatitude: baseLatitude ?? undefined,
        clientBaseLongitude: baseLongitude ?? undefined
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
      const nextBaseZipCode = formatZip(String(updatedProfile.clientBaseZipCode || normalizedZip).trim());
      const nextBaseStreet = String(updatedProfile.clientBaseStreet || normalizeOptionalText(baseStreet)).trim();
      const nextBaseCity = String(updatedProfile.clientBaseCity || normalizeOptionalText(baseCity)).trim();
      const nextBaseLatitude = Number.isFinite(Number(updatedProfile.clientBaseLatitude))
        ? Number(updatedProfile.clientBaseLatitude)
        : (baseLatitude ?? null);
      const nextBaseLongitude = Number.isFinite(Number(updatedProfile.clientBaseLongitude))
        ? Number(updatedProfile.clientBaseLongitude)
        : (baseLongitude ?? null);
      setBaseZipCode(nextBaseZipCode);
      setSavedBaseZipCode(nextBaseZipCode);
      setBaseStreet(nextBaseStreet);
      setSavedBaseStreet(nextBaseStreet);
      setBaseCity(nextBaseCity);
      setSavedBaseCity(nextBaseCity);
      setBaseLatitude(nextBaseLatitude);
      setSavedBaseLatitude(nextBaseLatitude);
      setBaseLongitude(nextBaseLongitude);
      setSavedBaseLongitude(nextBaseLongitude);
      setProfilePictureUrl(resolveProfilePictureUrl(updatedProfile.profilePictureUrl));
      setFeedbackSuccess('Perfil atualizado com sucesso.');
      onUserNameUpdated?.(nextName);
    } catch (error) {
      setFeedbackError(error instanceof Error ? error.message : 'Nao foi possivel salvar seu nome.');
    } finally {
      setSavingName(false);
    }
  };

  const handleAcceptTerms = async () => {
    if (!authToken || termsAccepting || !termsStatus || termsStatus.accepted) {
      return;
    }

    if (!termsAcceptedCheckbox) {
      setTermsError('Marque o checkbox para registrar o aceite do termo.');
      return;
    }

    setTermsAccepting(true);
    setTermsError('');

    try {
      const updatedStatus = await acceptClientProfileLegalTerms(authToken, 'mobile_client_profile');
      setTermsStatus(updatedStatus);
      setTermsAcceptedCheckbox(Boolean(updatedStatus.accepted));
      setFeedbackSuccess('Aceite do termo registrado com sucesso.');
    } catch (error) {
      setTermsError(error instanceof Error ? error.message : 'Nao foi possivel registrar o aceite do termo.');
    } finally {
      setTermsAccepting(false);
    }
  };

  const handleDownloadTermsPdf = () => {
    if (!termsStatus) {
      return;
    }

    const plainText = termsHtmlToText(termsStatus.htmlContent);
    if (!plainText) {
      setTermsError('Conteudo do termo indisponivel para download.');
      return;
    }

    const pdf = new jsPDF({
      unit: 'pt',
      format: 'a4'
    });

    const margin = 42;
    const maxWidth = 595.28 - (margin * 2);
    let cursorY = margin;

    pdf.setFontSize(15);
    pdf.setFont('helvetica', 'bold');
    pdf.text(`${termsStatus.title} (v${termsStatus.activeVersion})`, margin, cursorY);
    cursorY += 26;

    pdf.setFontSize(10);
    pdf.setFont('helvetica', 'normal');
    pdf.text(`Publicado em: ${formatTermsDate(termsStatus.publishedAtUtc)}`, margin, cursorY);
    cursorY += 20;

    const lines = pdf.splitTextToSize(plainText, maxWidth) as string[];
    const lineHeight = 14;

    lines.forEach((line) => {
      if (cursorY > 800) {
        pdf.addPage();
        cursorY = margin;
      }

      pdf.text(line, margin, cursorY);
      cursorY += lineHeight;
    });

    const fileName = `ConsertaPraMim-Termo-${termsStatus.audience}-v${termsStatus.activeVersion}.pdf`;
    pdf.save(fileName);
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
              <label className="text-[10px] font-bold text-primary uppercase ml-1">CEP principal</label>
              <div className="flex items-stretch gap-2">
                <input
                  type="text"
                  value={baseZipCode}
                  onChange={(event) => {
                    const formatted = formatZip(event.target.value);
                    setBaseZipCode(formatted);
                    if (onlyDigits(formatted).length < 8) {
                      setBaseLatitude(null);
                      setBaseLongitude(null);
                    }
                  }}
                  maxLength={9}
                  inputMode="numeric"
                  className="flex-1 h-11 bg-background-light border-none rounded-xl px-4 text-sm focus:ring-2 focus:ring-primary/20"
                  placeholder="00000-000"
                />
                <button
                  type="button"
                  onClick={() => void handleResolveZip()}
                  disabled={!authToken || locationLoading}
                  className="h-11 px-3 rounded-xl bg-primary text-white text-xs font-bold disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {locationLoading && locationMode === 'zip' ? 'Buscando...' : 'Buscar'}
                </button>
              </div>
            </div>
            <button
              type="button"
              onClick={() => void handleUseCurrentLocation()}
              disabled={!authToken || locationLoading}
              className="w-full h-11 rounded-xl border border-primary/20 text-primary text-sm font-bold disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {locationLoading && locationMode === 'current' ? 'Localizando...' : 'Usar localizacao atual'}
            </button>
            <div className="space-y-1">
              <label className="text-[10px] font-bold text-primary uppercase ml-1">Rua / Logradouro</label>
              <input
                type="text"
                value={baseStreet}
                onChange={(event) => {
                  setBaseStreet(event.target.value);
                  setBaseLatitude(null);
                  setBaseLongitude(null);
                }}
                className="w-full h-11 bg-background-light border-none rounded-xl px-4 text-sm focus:ring-2 focus:ring-primary/20"
                placeholder="Rua, numero e complemento"
              />
            </div>
            <div className="space-y-1">
              <label className="text-[10px] font-bold text-primary uppercase ml-1">Cidade</label>
              <input
                type="text"
                value={baseCity}
                onChange={(event) => {
                  setBaseCity(event.target.value);
                  setBaseLatitude(null);
                  setBaseLongitude(null);
                }}
                className="w-full h-11 bg-background-light border-none rounded-xl px-4 text-sm focus:ring-2 focus:ring-primary/20"
                placeholder="Cidade"
              />
            </div>
            {locationMessage ? (
              <div className={`rounded-xl px-3 py-2 text-xs ${
                locationMessage.type === 'success'
                  ? 'border border-emerald-200 bg-emerald-50 text-emerald-700'
                  : locationMessage.type === 'error'
                    ? 'border border-red-200 bg-red-50 text-red-700'
                    : 'border border-blue-200 bg-blue-50 text-blue-700'
              }`}>
                {locationMessage.text}
              </div>
            ) : null}
            {hasLocationCoordinates && mapEmbedUrl ? (
              <div className="space-y-1">
                <label className="text-[10px] font-bold text-primary uppercase ml-1">Mapa da localizacao</label>
                <div className="overflow-hidden rounded-xl border border-primary/10 bg-white">
                  <iframe
                    title="Mapa da localizacao principal do cliente"
                    src={mapEmbedUrl}
                    className="w-full h-48 border-0"
                    loading="lazy"
                    referrerPolicy="no-referrer-when-downgrade"
                  />
                </div>
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
          <h3 className="text-xs font-bold text-[#5e8d8d] uppercase tracking-wider ml-1">Termo de Aceite</h3>
          <div className="bg-white rounded-2xl border border-primary/5 shadow-sm p-4 space-y-3">
            {termsLoading ? (
              <div className="rounded-xl border border-primary/10 bg-background-light px-3 py-2 text-xs text-[#5e8d8d]">
                Carregando termo de aceite...
              </div>
            ) : null}

            {termsError ? (
              <div className="rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">
                {termsError}
              </div>
            ) : null}

            {termsStatus ? (
              <>
                <div className="rounded-xl border border-primary/10 bg-background-light px-3 py-2 text-xs text-[#375757]">
                  <div className="font-semibold text-[#101818]">{termsStatus.title}</div>
                  <div className="mt-1">Versao ativa: v{termsStatus.activeVersion}</div>
                  <div>Publicado em: {formatTermsDate(termsStatus.publishedAtUtc)}</div>
                  <div>
                    Status do aceite: {termsStatus.accepted
                      ? `aceito em ${formatTermsDate(termsStatus.acceptedAtUtc)}`
                      : 'pendente'}
                  </div>
                </div>

                <label className="flex items-start gap-2 text-xs text-[#375757]">
                  <input
                    type="checkbox"
                    className="mt-0.5 size-4 accent-primary"
                    checked={termsAcceptedCheckbox}
                    disabled={termsStatus.accepted}
                    onChange={(event) => setTermsAcceptedCheckbox(event.target.checked)}
                  />
                  <span>
                    Li e aceito o termo de uso e a clausula de isencao de responsabilidade da plataforma.
                  </span>
                </label>

                <div className="grid grid-cols-1 gap-2">
                  <button
                    type="button"
                    onClick={() => setShowTermsModal(true)}
                    className="w-full h-10 rounded-xl border border-primary/20 text-primary text-sm font-bold"
                  >
                    Visualizar termo
                  </button>
                  <button
                    type="button"
                    onClick={handleDownloadTermsPdf}
                    className="w-full h-10 rounded-xl border border-primary/20 text-primary text-sm font-bold"
                  >
                    Baixar termo em PDF
                  </button>
                  <button
                    type="button"
                    onClick={() => void handleAcceptTerms()}
                    disabled={termsStatus.accepted || termsAccepting || !termsAcceptedCheckbox}
                    className="w-full h-10 rounded-xl bg-primary text-white text-sm font-bold disabled:opacity-50 disabled:cursor-not-allowed"
                  >
                    {termsStatus.accepted
                      ? 'Termo ja aceito'
                      : termsAccepting
                        ? 'Registrando aceite...'
                        : 'Registrar aceite'}
                  </button>
                </div>
              </>
            ) : null}
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

      {showTermsModal && termsStatus ? (
        <div
          className="fixed inset-0 z-[120] flex items-end bg-black/60 p-0 sm:items-center sm:justify-center sm:p-4"
          onClick={() => setShowTermsModal(false)}
        >
          <div
            className="w-full max-w-2xl max-h-[90vh] overflow-hidden rounded-t-2xl bg-white shadow-2xl sm:rounded-2xl"
            onClick={(event) => event.stopPropagation()}
          >
            <div className="flex items-center justify-between border-b border-primary/10 px-4 py-3">
              <div>
                <h4 className="text-sm font-bold text-[#101818]">{termsStatus.title}</h4>
                <p className="text-[11px] text-[#5e8d8d]">
                  v{termsStatus.activeVersion} | Publicado em {formatTermsDate(termsStatus.publishedAtUtc)}
                </p>
              </div>
              <button
                type="button"
                className="rounded-full p-1 text-[#5e8d8d] hover:bg-primary/5"
                onClick={() => setShowTermsModal(false)}
              >
                <span className="material-symbols-outlined">close</span>
              </button>
            </div>
            <div className="max-h-[75vh] overflow-y-auto px-4 py-4 text-sm text-[#263535]">
              <div className="prose prose-sm max-w-none" dangerouslySetInnerHTML={{ __html: termsStatus.htmlContent }} />
            </div>
          </div>
        </div>
      ) : null}

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
