import React, { useEffect, useMemo, useState } from 'react';
import jsPDF from 'jspdf';
import {
  acceptMobileProviderProfileLegalTerms,
  fetchMobileProviderProfileLegalTermsStatus
} from '../services/mobileProvider';
import {
  ProviderAuthSession,
  ProviderProfileLegalTermsStatus,
  ProviderProfileSettings,
  ProviderProfileSettingsSaveResult,
  ProviderResolveZipResult
} from '../types';

interface ProfileFormState {
  operationalStatus: number;
  clientPreference: number;
  radiusKm: number;
  baseZipCode: string;
  baseLatitude?: number;
  baseLongitude?: number;
  categories: number[];
}

interface Props {
  session: ProviderAuthSession | null;
  settings: ProviderProfileSettings | null;
  loading: boolean;
  error: string;
  saving: boolean;
  updatingStatus: boolean;
  resolvingZip: boolean;
  successMessage: string;
  onBack: () => void;
  onLogout: () => void;
  onRefresh: () => Promise<void>;
  onResolveZip: (zipCode: string) => Promise<ProviderResolveZipResult>;
  onUpdateOperationalStatus: (operationalStatus: number) => Promise<ProviderProfileSettingsSaveResult>;
  onSave: (state: ProfileFormState) => Promise<ProviderProfileSettingsSaveResult>;
}

function formatZip(value?: string): string {
  const digits = String(value || '').replace(/\D/g, '').slice(0, 8);
  if (digits.length <= 5) {
    return digits;
  }

  return `${digits.slice(0, 5)}-${digits.slice(5, 8)}`;
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

function formatTermsDate(value?: string | null): string {
  if (!value) {
    return 'nao disponivel';
  }

  const parsed = new Date(value);
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
  session,
  settings,
  loading,
  error,
  saving,
  updatingStatus,
  resolvingZip,
  successMessage,
  onBack,
  onLogout,
  onRefresh,
  onResolveZip,
  onUpdateOperationalStatus,
  onSave
}) => {
  const [operationalStatus, setOperationalStatus] = useState<number>(0);
  const [clientPreference, setClientPreference] = useState<number>(0);
  const [radiusKm, setRadiusKm] = useState<number>(1);
  const [baseZipCode, setBaseZipCode] = useState<string>('');
  const [baseLatitude, setBaseLatitude] = useState<number | undefined>(undefined);
  const [baseLongitude, setBaseLongitude] = useState<number | undefined>(undefined);
  const [selectedCategories, setSelectedCategories] = useState<number[]>([]);
  const [resolvedAddress, setResolvedAddress] = useState<string>('');
  const [localMessage, setLocalMessage] = useState<{ type: 'info' | 'error' | 'success'; text: string } | null>(null);
  const [zipMessage, setZipMessage] = useState<{ type: 'info' | 'error' | 'success'; text: string } | null>(null);
  const [termsStatus, setTermsStatus] = useState<ProviderProfileLegalTermsStatus | null>(null);
  const [termsLoading, setTermsLoading] = useState(false);
  const [termsError, setTermsError] = useState('');
  const [termsAcceptedCheckbox, setTermsAcceptedCheckbox] = useState(false);
  const [termsAccepting, setTermsAccepting] = useState(false);
  const [showTermsModal, setShowTermsModal] = useState(false);

  useEffect(() => {
    if (!settings) {
      return;
    }

    const selectedStatus = settings.operationalStatuses.find((item) => item.selected)?.value
      ?? settings.operationalStatuses[0]?.value
      ?? 0;
    setOperationalStatus(selectedStatus);
    const selectedClientPreference = settings.clientPreferences.find((item) => item.selected)?.value
      ?? settings.clientPreference
      ?? settings.clientPreferences[0]?.value
      ?? 0;
    setClientPreference(selectedClientPreference);
    setRadiusKm(Math.max(1, Math.round(settings.radiusKm)));
    setBaseZipCode(formatZip(settings.baseZipCode));
    setBaseLatitude(settings.baseLatitude);
    setBaseLongitude(settings.baseLongitude);
    setResolvedAddress('');
    setSelectedCategories(settings.categories.filter((item) => item.selected).map((item) => item.value));
  }, [settings]);

  useEffect(() => {
    if (!session?.token) {
      setTermsStatus(null);
      setTermsAcceptedCheckbox(false);
      setTermsError('');
      setTermsLoading(false);
      return;
    }

    let cancelled = false;
    setTermsLoading(true);
    setTermsError('');

    void fetchMobileProviderProfileLegalTermsStatus(session.token)
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
  }, [session?.token]);

  const statusLabelByValue = useMemo(() => {
    const map = new Map<number, string>();
    (settings?.operationalStatuses || []).forEach((item) => {
      map.set(item.value, item.label || item.name);
    });
    return map;
  }, [settings?.operationalStatuses]);

  const maxPlanCategories = Math.max(1, settings?.planMaxAllowedCategories || 1);
  const maxPlanRadius = Math.max(1, Math.round(settings?.planMaxRadiusKm || 1));

  const complianceWarning = settings?.hasOperationalCompliancePending
    ? (settings.operationalComplianceNotes || 'Seu perfil precisa de ajustes para ficar dentro dos limites do plano.')
    : '';

  const handleToggleCategory = (categoryValue: number) => {
    setLocalMessage(null);
    setSelectedCategories((current) => {
      if (current.includes(categoryValue)) {
        return current.filter((value) => value !== categoryValue);
      }

      if (current.length >= maxPlanCategories) {
        setLocalMessage({
          type: 'error',
          text: `Seu plano permite no maximo ${maxPlanCategories} categoria(s).`
        });
        return current;
      }

      return [...current, categoryValue];
    });
  };

  const handleLookupZip = async () => {
    const digits = String(baseZipCode || '').replace(/\D/g, '');
    if (digits.length !== 8) {
      setZipMessage({
        type: 'error',
        text: 'Informe um CEP valido com 8 digitos.'
      });
      return;
    }

    setZipMessage({
      type: 'info',
      text: 'Buscando localizacao do CEP...'
    });

    try {
      const result = await onResolveZip(digits);
      setBaseZipCode(formatZip(result.zipCode));
      setBaseLatitude(result.latitude);
      setBaseLongitude(result.longitude);
      setResolvedAddress(result.address || '');
      setZipMessage({
        type: 'success',
        text: result.address || 'Localizacao encontrada com sucesso.'
      });
    } catch (lookupError) {
      setResolvedAddress('');
      setZipMessage({
        type: 'error',
        text: lookupError instanceof Error ? lookupError.message : 'Nao foi possivel localizar esse CEP.'
      });
    }
  };

  const handleUpdateStatusNow = async () => {
    setLocalMessage({
      type: 'info',
      text: 'Atualizando status operacional...'
    });

    try {
      const result = await onUpdateOperationalStatus(operationalStatus);
      const statusLabel = statusLabelByValue.get(operationalStatus) || 'Atualizado';
      setLocalMessage({
        type: 'success',
        text: result.message || `Status atualizado para "${statusLabel}".`
      });
    } catch (statusError) {
      setLocalMessage({
        type: 'error',
        text: statusError instanceof Error ? statusError.message : 'Nao foi possivel atualizar o status.'
      });
    }
  };

  const handleSave = async () => {
    if (selectedCategories.length <= 0) {
      setLocalMessage({
        type: 'error',
        text: 'Selecione pelo menos uma especialidade.'
      });
      return;
    }

    if (selectedCategories.length > maxPlanCategories) {
      setLocalMessage({
        type: 'error',
        text: `Seu plano permite no maximo ${maxPlanCategories} categoria(s).`
      });
      return;
    }

    setLocalMessage({
      type: 'info',
      text: 'Salvando configuracoes...'
    });

    try {
      const result = await onSave({
        operationalStatus,
        clientPreference,
        radiusKm,
        baseZipCode: String(baseZipCode || '').replace(/\D/g, '') || undefined,
        baseLatitude,
        baseLongitude,
        categories: selectedCategories
      });

      setLocalMessage({
        type: 'success',
        text: result.message || 'Perfil atualizado com sucesso.'
      });
    } catch (saveError) {
      setLocalMessage({
        type: 'error',
        text: saveError instanceof Error ? saveError.message : 'Nao foi possivel salvar as alteracoes.'
      });
    }
  };

  const handleAcceptTerms = async () => {
    if (!session?.token || !termsStatus || termsStatus.accepted || termsAccepting) {
      return;
    }

    if (!termsAcceptedCheckbox) {
      setTermsError('Marque o checkbox para registrar o aceite do termo.');
      return;
    }

    setTermsAccepting(true);
    setTermsError('');

    try {
      const status = await acceptMobileProviderProfileLegalTerms(session.token, 'mobile_provider_profile');
      setTermsStatus(status);
      setTermsAcceptedCheckbox(Boolean(status.accepted));
      setLocalMessage({
        type: 'success',
        text: 'Aceite do termo registrado com sucesso.'
      });
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

  return (
    <div className="min-h-screen bg-[#f4f7fb] pb-8">
      <header className="bg-white border-b border-[#e4e7ec] sticky top-0 z-10">
        <div className="max-w-md mx-auto px-4 py-4 flex items-center justify-between gap-2">
          <button type="button" onClick={onBack} className="text-sm font-semibold text-[#344054] flex items-center gap-1">
            <span className="material-symbols-outlined text-base">arrow_back</span>
            Voltar
          </button>
          <button
            type="button"
            onClick={() => void onRefresh()}
            className="text-sm font-semibold text-primary disabled:opacity-50"
            disabled={loading}
          >
            Atualizar
          </button>
        </div>
      </header>

      <main className="max-w-md mx-auto px-4 py-5 space-y-4">
        <section className="rounded-2xl bg-white border border-[#e4e7ec] p-5 shadow-sm">
          <h1 className="text-xl font-bold text-[#101828]">Configuracoes do Perfil</h1>
          <p className="text-sm text-[#667085] mt-1">Gerencie status operacional, area de atendimento e especialidades.</p>

          <div className="mt-4 space-y-1 text-sm text-[#344054]">
            <p><span className="font-semibold">Nome:</span> {settings?.name || session?.userName || '-'}</p>
            <p><span className="font-semibold">E-mail:</span> {settings?.email || session?.email || '-'}</p>
            <p><span className="font-semibold">Telefone:</span> {settings?.phone || '-'}</p>
            <p><span className="font-semibold">Plano:</span> {settings?.plan || '-'}</p>
          </div>
        </section>

        {complianceWarning ? (
          <div className="rounded-2xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800">
            {complianceWarning}
          </div>
        ) : null}

        {error ? (
          <div className="rounded-2xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">{error}</div>
        ) : null}

        {successMessage ? (
          <div className="rounded-2xl border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-700">{successMessage}</div>
        ) : null}

        {localMessage ? (
          <div className={`rounded-2xl border p-4 text-sm ${
            localMessage.type === 'success'
              ? 'border-emerald-200 bg-emerald-50 text-emerald-700'
              : localMessage.type === 'error'
                ? 'border-red-200 bg-red-50 text-red-700'
                : 'border-blue-200 bg-blue-50 text-blue-700'
          }`}>
            {localMessage.text}
          </div>
        ) : null}

        <section className="rounded-2xl bg-white border border-[#e4e7ec] p-5 shadow-sm space-y-5">
          <div>
            <label className="text-sm font-semibold text-[#344054] block mb-2">Status operacional</label>
            <div className="flex gap-2">
              <select
                value={operationalStatus}
                onChange={(event) => {
                  setOperationalStatus(Number(event.target.value));
                  setLocalMessage({
                    type: 'info',
                    text: 'Clique em "Atualizar agora" para propagar em tempo real.'
                  });
                }}
                className="flex-1 rounded-xl border border-[#d0d5dd] bg-[#f8fafc] px-3 py-3 text-sm text-[#344054]"
                disabled={loading || !settings}
              >
                {(settings?.operationalStatuses || []).map((status) => (
                  <option key={status.value} value={status.value}>
                    {status.label || status.name}
                  </option>
                ))}
              </select>
              <button
                type="button"
                onClick={() => void handleUpdateStatusNow()}
                className="rounded-xl border border-primary px-4 text-sm font-semibold text-primary disabled:opacity-50"
                disabled={loading || updatingStatus || !settings}
              >
                Atualizar agora
              </button>
            </div>
            <p className="text-xs text-[#667085] mt-2">Esse status aparece em tempo real para clientes.</p>
          </div>

          <div>
            <label className="text-sm font-semibold text-[#344054] block mb-2">Preferencia de atendimento</label>
            <select
              value={clientPreference}
              onChange={(event) => setClientPreference(Number(event.target.value))}
              className="w-full rounded-xl border border-[#d0d5dd] bg-[#f8fafc] px-3 py-3 text-sm text-[#344054]"
              disabled={loading || !settings}
            >
              {(settings?.clientPreferences || []).map((item) => (
                <option key={item.value} value={item.value}>
                  {item.label || item.name}
                </option>
              ))}
            </select>
            <p className="text-xs text-[#667085] mt-2">Defina se deseja atender clientes PF, PJ ou ambos.</p>
          </div>

          <div>
            <label className="text-sm font-semibold text-[#344054] block mb-2">CEP base de atendimento</label>
            <div className="flex gap-2">
              <input
                value={baseZipCode}
                onChange={(event) => setBaseZipCode(formatZip(event.target.value))}
                onBlur={() => {
                  if (String(baseZipCode || '').replace(/\D/g, '').length === 8) {
                    void handleLookupZip();
                  }
                }}
                placeholder="00000-000"
                className="flex-1 rounded-xl border border-[#d0d5dd] bg-[#f8fafc] px-3 py-3 text-sm text-[#344054]"
                disabled={loading || !settings}
              />
              <button
                type="button"
                onClick={() => void handleLookupZip()}
                className="rounded-xl border border-primary px-4 text-sm font-semibold text-primary disabled:opacity-50"
                disabled={loading || resolvingZip || !settings}
              >
                Buscar
              </button>
            </div>
            {zipMessage ? (
              <p className={`text-xs mt-2 ${
                zipMessage.type === 'success'
                  ? 'text-emerald-700'
                  : zipMessage.type === 'error'
                    ? 'text-red-700'
                    : 'text-[#667085]'
              }`}>
                {zipMessage.text}
              </p>
            ) : (
              <p className="text-xs text-[#667085] mt-2">Esse CEP sera o centro do seu raio de atendimento.</p>
            )}
            {resolvedAddress ? (
              <p className="text-xs text-[#344054] mt-2">
                <span className="font-semibold">Endereco:</span> {resolvedAddress}
              </p>
            ) : null}
            {baseLatitude !== undefined && baseLongitude !== undefined ? (
              <div className="mt-3 overflow-hidden rounded-xl border border-[#d0d5dd] bg-white">
                <iframe
                  title="Mapa da localizacao base"
                  src={buildOsmEmbedUrl(baseLatitude, baseLongitude)}
                  className="h-56 w-full border-0"
                  loading="lazy"
                  referrerPolicy="no-referrer-when-downgrade"
                />
                <div className="border-t border-[#e4e7ec] bg-[#f8fafc] px-3 py-2 text-[11px] text-[#667085]">
                  Pin da localizacao base selecionada.
                </div>
              </div>
            ) : null}
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="text-xs font-semibold text-[#667085] block mb-1">Latitude (auto)</label>
              <input
                value={baseLatitude !== undefined ? baseLatitude.toFixed(6) : ''}
                readOnly
                className="w-full rounded-xl border border-[#d0d5dd] bg-[#eef2f6] px-3 py-2 text-xs text-[#344054]"
              />
            </div>
            <div>
              <label className="text-xs font-semibold text-[#667085] block mb-1">Longitude (auto)</label>
              <input
                value={baseLongitude !== undefined ? baseLongitude.toFixed(6) : ''}
                readOnly
                className="w-full rounded-xl border border-[#d0d5dd] bg-[#eef2f6] px-3 py-2 text-xs text-[#344054]"
              />
            </div>
          </div>

          <div>
            <label className="text-sm font-semibold text-[#344054] block mb-2">Raio de atendimento</label>
            <div className="flex items-center gap-3">
              <input
                type="range"
                min={1}
                max={maxPlanRadius}
                step={1}
                value={radiusKm}
                onChange={(event) => setRadiusKm(Number(event.target.value))}
                className="flex-1"
                disabled={loading || !settings}
              />
              <span className="rounded-lg bg-primary text-white text-xs font-bold px-2 py-1 min-w-[52px] text-center">
                {radiusKm}km
              </span>
            </div>
            <p className="text-xs text-[#667085] mt-2">Limite do plano: ate {maxPlanRadius} km.</p>
          </div>

          <div>
            <label className="text-sm font-semibold text-[#344054] block">Especialidades</label>
            <p className="text-xs text-[#667085] mb-2">Limite do plano: ate {maxPlanCategories} categoria(s).</p>
            <div className="grid grid-cols-2 gap-2">
              {(settings?.categories || []).map((category) => {
                const checked = selectedCategories.includes(category.value);
                return (
                  <button
                    type="button"
                    key={category.value}
                    onClick={() => handleToggleCategory(category.value)}
                    className={`rounded-xl border px-3 py-2 text-left text-xs ${
                      checked
                        ? 'border-primary bg-primary/10 text-primary'
                        : 'border-[#d0d5dd] bg-[#f8fafc] text-[#344054]'
                    }`}
                    disabled={loading || !settings}
                  >
                    <span className="material-symbols-outlined text-sm align-middle mr-1">{category.icon || 'build_circle'}</span>
                    {category.label}
                  </button>
                );
              })}
            </div>
          </div>

          <button
            type="button"
            onClick={() => void handleSave()}
            className="w-full rounded-xl bg-primary text-white py-3 font-bold disabled:opacity-50"
            disabled={loading || saving || !settings}
          >
            Salvar alteracoes
          </button>
        </section>

        <section className="rounded-2xl bg-white border border-[#e4e7ec] p-5 shadow-sm space-y-3">
          <h2 className="text-base font-bold text-[#101828]">Termo de Aceite</h2>

          {termsLoading ? (
            <div className="rounded-xl border border-blue-100 bg-blue-50 p-3 text-xs text-blue-700">
              Carregando termo ativo...
            </div>
          ) : null}

          {termsError ? (
            <div className="rounded-xl border border-red-200 bg-red-50 p-3 text-xs text-red-700">
              {termsError}
            </div>
          ) : null}

          {termsStatus ? (
            <>
              <div className="rounded-xl border border-[#d0d5dd] bg-[#f8fafc] p-3 text-xs text-[#344054] space-y-1">
                <p className="font-semibold text-[#101828]">{termsStatus.title}</p>
                <p>Versao ativa: v{termsStatus.activeVersion}</p>
                <p>Publicado em: {formatTermsDate(termsStatus.publishedAtUtc)}</p>
                <p>
                  Status: {termsStatus.accepted
                    ? `aceito em ${formatTermsDate(termsStatus.acceptedAtUtc)}`
                    : 'pendente'}
                </p>
              </div>

              <label className="flex items-start gap-2 text-sm text-[#344054]">
                <input
                  type="checkbox"
                  className="mt-1 size-4 accent-primary"
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
                  className="w-full rounded-xl border border-primary px-4 py-2 text-sm font-semibold text-primary"
                >
                  Visualizar termo
                </button>
                <button
                  type="button"
                  onClick={handleDownloadTermsPdf}
                  className="w-full rounded-xl border border-primary px-4 py-2 text-sm font-semibold text-primary"
                >
                  Baixar termo em PDF
                </button>
                <button
                  type="button"
                  onClick={() => void handleAcceptTerms()}
                  disabled={termsStatus.accepted || termsAccepting || !termsAcceptedCheckbox}
                  className="w-full rounded-xl bg-primary text-white py-3 text-sm font-bold disabled:opacity-50"
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
        </section>

        <button
          type="button"
          onClick={onLogout}
          className="w-full rounded-xl bg-red-600 text-white py-3 font-bold"
        >
          Sair da conta
        </button>

        {showTermsModal && termsStatus ? (
          <div
            className="fixed inset-0 z-[120] flex items-end bg-black/60 p-0 sm:items-center sm:justify-center sm:p-4"
            onClick={() => setShowTermsModal(false)}
          >
            <div
              className="w-full max-w-2xl max-h-[90vh] overflow-hidden rounded-t-2xl bg-white shadow-2xl sm:rounded-2xl"
              onClick={(event) => event.stopPropagation()}
            >
              <div className="flex items-center justify-between border-b border-[#e4e7ec] px-4 py-3">
                <div>
                  <h4 className="text-sm font-bold text-[#101828]">{termsStatus.title}</h4>
                  <p className="text-[11px] text-[#667085]">
                    v{termsStatus.activeVersion} | Publicado em {formatTermsDate(termsStatus.publishedAtUtc)}
                  </p>
                </div>
                <button
                  type="button"
                  className="rounded-full p-1 text-[#667085] hover:bg-[#f2f4f7]"
                  onClick={() => setShowTermsModal(false)}
                >
                  <span className="material-symbols-outlined">close</span>
                </button>
              </div>
              <div className="max-h-[75vh] overflow-y-auto px-4 py-4 text-sm text-[#344054]">
                <div className="prose prose-sm max-w-none" dangerouslySetInnerHTML={{ __html: termsStatus.htmlContent }} />
              </div>
            </div>
          </div>
        ) : null}
      </main>
    </div>
  );
};

export default Profile;
