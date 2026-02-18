import React, { useEffect, useMemo, useState } from 'react';
import {
  ProviderAuthSession,
  ProviderProfileSettings,
  ProviderProfileSettingsSaveResult,
  ProviderResolveZipResult
} from '../types';

interface ProfileFormState {
  operationalStatus: number;
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
  const [radiusKm, setRadiusKm] = useState<number>(1);
  const [baseZipCode, setBaseZipCode] = useState<string>('');
  const [baseLatitude, setBaseLatitude] = useState<number | undefined>(undefined);
  const [baseLongitude, setBaseLongitude] = useState<number | undefined>(undefined);
  const [selectedCategories, setSelectedCategories] = useState<number[]>([]);
  const [localMessage, setLocalMessage] = useState<{ type: 'info' | 'error' | 'success'; text: string } | null>(null);
  const [zipMessage, setZipMessage] = useState<{ type: 'info' | 'error' | 'success'; text: string } | null>(null);

  useEffect(() => {
    if (!settings) {
      return;
    }

    const selectedStatus = settings.operationalStatuses.find((item) => item.selected)?.value
      ?? settings.operationalStatuses[0]?.value
      ?? 0;
    setOperationalStatus(selectedStatus);
    setRadiusKm(Math.max(1, Math.round(settings.radiusKm)));
    setBaseZipCode(formatZip(settings.baseZipCode));
    setBaseLatitude(settings.baseLatitude);
    setBaseLongitude(settings.baseLongitude);
    setSelectedCategories(settings.categories.filter((item) => item.selected).map((item) => item.value));
  }, [settings]);

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
      setZipMessage({
        type: 'success',
        text: result.address || 'Localizacao encontrada com sucesso.'
      });
    } catch (lookupError) {
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

        <button
          type="button"
          onClick={onLogout}
          className="w-full rounded-xl bg-red-600 text-white py-3 font-bold"
        >
          Sair da conta
        </button>
      </main>
    </div>
  );
};

export default Profile;
