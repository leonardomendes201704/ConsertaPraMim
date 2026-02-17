import React, { useState } from 'react';
import {
  ProviderAgendaData,
  ProviderAgendaItem,
  ProviderAppointmentChecklist,
  ProviderChecklistEvidenceUploadResult,
  ProviderChecklistItem,
  ProviderChecklistItemUpsertPayload
} from '../types';

interface Props {
  agenda: ProviderAgendaData | null;
  checklists: Record<string, ProviderAppointmentChecklist | undefined>;
  loading: boolean;
  error: string;
  actionLoadingKey: string | null;
  onBack: () => void;
  onRefresh: () => Promise<void>;
  onOpenRequest: (requestId: string) => void;
  onConfirm: (appointmentId: string) => Promise<void>;
  onReject: (appointmentId: string, reason: string) => Promise<void>;
  onRespondReschedule: (appointmentId: string, accept: boolean, reason?: string) => Promise<void>;
  onMarkArrival: (appointmentId: string, payload?: { latitude?: number; longitude?: number; accuracyMeters?: number; manualReason?: string }) => Promise<void>;
  onStartExecution: (appointmentId: string, reason?: string) => Promise<void>;
  onUpdateOperationalStatus: (appointmentId: string, operationalStatus: string, reason?: string) => Promise<void>;
  onLoadChecklist: (appointmentId: string) => Promise<void>;
  onUpdateChecklistItem: (appointmentId: string, payload: ProviderChecklistItemUpsertPayload) => Promise<void>;
  onUploadChecklistEvidence: (appointmentId: string, file: File) => Promise<ProviderChecklistEvidenceUploadResult>;
}

const CLOSED_APPOINTMENT_STATUSES = new Set([
  'RejectedByProvider',
  'CancelledByClient',
  'CancelledByProvider',
  'ExpiredWithoutProviderAction'
]);

const OPERATIONAL_STATUS_OPTIONS: Array<{ value: string; label: string }> = [
  { value: 'OnTheWay', label: 'A caminho' },
  { value: 'OnSite', label: 'No local' },
  { value: 'InService', label: 'Em atendimento' },
  { value: 'WaitingParts', label: 'Aguardando peca' },
  { value: 'Completed', label: 'Concluido' }
];

const MAX_EVIDENCE_SIZE_BYTES = 25_000_000;
const ALLOWED_EVIDENCE_EXTENSIONS = new Set(['.jpg', '.jpeg', '.png', '.webp', '.mp4', '.webm', '.mov']);
const ALLOWED_EVIDENCE_CONTENT_TYPES = new Set([
  'image/jpeg',
  'image/png',
  'image/webp',
  'video/mp4',
  'video/webm',
  'video/quicktime'
]);

function normalizeId(value?: string | null): string {
  return String(value || '').trim().toLowerCase();
}

function canMarkArrival(item: ProviderAgendaItem): boolean {
  return item.appointmentStatus === 'Confirmed' || item.appointmentStatus === 'RescheduleConfirmed';
}

function canStartExecution(item: ProviderAgendaItem): boolean {
  return item.appointmentStatus === 'Arrived';
}

function canUpdateOperationalStatus(item: ProviderAgendaItem): boolean {
  return !CLOSED_APPOINTMENT_STATUSES.has(item.appointmentStatus);
}

function isActionBusy(actionLoadingKey: string | null, appointmentId: string, scope?: string): boolean {
  if (!actionLoadingKey) {
    return false;
  }

  const normalizedAppointmentId = normalizeId(appointmentId);
  const normalizedAction = normalizeId(actionLoadingKey);
  if (!normalizedAction.startsWith(`${normalizedAppointmentId}:`)) {
    return false;
  }

  if (!scope) {
    return true;
  }

  return normalizedAction.startsWith(`${normalizedAppointmentId}:${normalizeId(scope)}`);
}

function validateEvidenceFile(file?: File): string | null {
  if (!file) {
    return 'Selecione um arquivo de evidencia.';
  }

  if (file.size > MAX_EVIDENCE_SIZE_BYTES) {
    return 'Arquivo acima do limite de 25MB.';
  }

  const lowerName = file.name.toLowerCase();
  const extension = lowerName.includes('.') ? lowerName.slice(lowerName.lastIndexOf('.')) : '';
  if (!ALLOWED_EVIDENCE_EXTENSIONS.has(extension)) {
    return 'Formato nao permitido. Use JPG, PNG, WEBP, MP4, WEBM ou MOV.';
  }

  if (file.type && !ALLOWED_EVIDENCE_CONTENT_TYPES.has(file.type.toLowerCase())) {
    return 'Tipo de arquivo nao permitido para evidencia.';
  }

  return null;
}

async function resolveCurrentLocation(): Promise<{ latitude?: number; longitude?: number; accuracyMeters?: number }> {
  if (!navigator.geolocation) {
    return {};
  }

  return new Promise((resolve) => {
    navigator.geolocation.getCurrentPosition(
      (position) => {
        resolve({
          latitude: position.coords.latitude,
          longitude: position.coords.longitude,
          accuracyMeters: Number.isFinite(position.coords.accuracy) ? position.coords.accuracy : undefined
        });
      },
      () => resolve({}),
      {
        enableHighAccuracy: true,
        timeout: 7000,
        maximumAge: 0
      }
    );
  });
}

const Agenda: React.FC<Props> = ({
  agenda,
  checklists,
  loading,
  error,
  actionLoadingKey,
  onBack,
  onRefresh,
  onOpenRequest,
  onConfirm,
  onReject,
  onRespondReschedule,
  onMarkArrival,
  onStartExecution,
  onUpdateOperationalStatus,
  onLoadChecklist,
  onUpdateChecklistItem,
  onUploadChecklistEvidence
}) => {
  const handleReject = async (item: ProviderAgendaItem) => {
    const reason = window.prompt('Informe o motivo da recusa:')?.trim();
    if (!reason) {
      return;
    }

    await onReject(item.appointmentId, reason);
  };

  const handleRejectReschedule = async (item: ProviderAgendaItem) => {
    const reason = window.prompt('Informe o motivo da recusa do reagendamento (opcional):')?.trim();
    await onRespondReschedule(item.appointmentId, false, reason || undefined);
  };

  return (
    <div className="min-h-screen bg-[#f4f7fb] pb-8">
      <header className="bg-white border-b border-[#e4e7ec] sticky top-0 z-10">
        <div className="max-w-md mx-auto px-4 py-4 flex items-center justify-between">
          <button type="button" onClick={onBack} className="text-sm font-semibold text-[#344054] flex items-center gap-1">
            <span className="material-symbols-outlined text-base">arrow_back</span>
            Voltar
          </button>
          <button type="button" onClick={() => void onRefresh()} className="text-sm font-semibold text-primary">Atualizar</button>
        </div>
      </header>

      <main className="max-w-md mx-auto px-4 py-5">
        {error && <div className="mb-4 rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{error}</div>}

        <section className="grid grid-cols-2 gap-2 mb-4">
          <KpiCard title="Pendencias" value={agenda?.pendingCount ?? 0} />
          <KpiCard title="Proximas visitas" value={agenda?.upcomingCount ?? 0} />
        </section>

        <section className="rounded-2xl bg-white border border-[#e4e7ec] p-4 shadow-sm mb-4">
          <h1 className="font-bold text-[#101828] mb-3">Aguardando sua acao</h1>
          {loading ? (
            <p className="text-sm text-[#667085]">Carregando agenda...</p>
          ) : !agenda?.pendingItems.length ? (
            <p className="text-sm text-[#667085]">Sem pendencias de confirmacao ou reagendamento.</p>
          ) : (
            <div className="space-y-3">
              {agenda.pendingItems.map((item) => (
                <AgendaPendingCard
                  key={item.appointmentId}
                  item={item}
                  loading={isActionBusy(actionLoadingKey, item.appointmentId)}
                  onOpenRequest={onOpenRequest}
                  onConfirm={onConfirm}
                  onReject={handleReject}
                  onAcceptReschedule={(agendaItem) => onRespondReschedule(agendaItem.appointmentId, true)}
                  onRejectReschedule={handleRejectReschedule}
                />
              ))}
            </div>
          )}
        </section>

        <section className="rounded-2xl bg-white border border-[#e4e7ec] p-4 shadow-sm">
          <h2 className="font-bold text-[#101828] mb-3">Proximas visitas</h2>
          {loading ? (
            <p className="text-sm text-[#667085]">Carregando agenda...</p>
          ) : !agenda?.upcomingItems.length ? (
            <p className="text-sm text-[#667085]">Sem visitas confirmadas no periodo atual.</p>
          ) : (
            <div className="space-y-3">
              {agenda.upcomingItems.map((item) => (
                <AgendaOperationalCard
                  key={item.appointmentId}
                  item={item}
                  checklist={checklists[normalizeId(item.appointmentId)]}
                  actionLoadingKey={actionLoadingKey}
                  onOpenRequest={onOpenRequest}
                  onMarkArrival={onMarkArrival}
                  onStartExecution={onStartExecution}
                  onUpdateOperationalStatus={onUpdateOperationalStatus}
                  onLoadChecklist={onLoadChecklist}
                  onUpdateChecklistItem={onUpdateChecklistItem}
                  onUploadChecklistEvidence={onUploadChecklistEvidence}
                />
              ))}
            </div>
          )}
        </section>
      </main>
    </div>
  );
};

const AgendaPendingCard: React.FC<{
  item: ProviderAgendaItem;
  loading: boolean;
  onOpenRequest: (requestId: string) => void;
  onConfirm: (item: ProviderAgendaItem) => Promise<void>;
  onReject: (item: ProviderAgendaItem) => Promise<void>;
  onAcceptReschedule: (item: ProviderAgendaItem) => Promise<void>;
  onRejectReschedule: (item: ProviderAgendaItem) => Promise<void>;
}> = ({
  item,
  loading,
  onOpenRequest,
  onConfirm,
  onReject,
  onAcceptReschedule,
  onRejectReschedule
}) => {
  return (
    <div className="rounded-xl border border-[#eaecf0] p-3 bg-[#fcfcfd]">
      <div className="flex items-start justify-between gap-2">
        <div>
          <p className="text-sm font-semibold text-[#101828]">{item.category || 'Servico'}</p>
          <p className="text-xs text-[#475467] mt-1">{item.clientName || 'Cliente'} - {item.windowLabel}</p>
          <p className="text-xs text-[#667085] mt-1">{item.street}, {item.city}</p>
        </div>
        <span className="text-[10px] px-2 py-1 rounded-full bg-amber-100 text-amber-700 font-bold uppercase">
          {item.appointmentStatusLabel}
        </span>
      </div>

      <div className="mt-3 flex flex-wrap gap-2">
        {item.canConfirm && (
          <button
            type="button"
            disabled={loading}
            onClick={() => void onConfirm(item)}
            className="rounded-lg bg-emerald-600 text-white text-xs font-semibold px-3 py-2 disabled:opacity-60"
          >
            Confirmar
          </button>
        )}

        {item.canReject && (
          <button
            type="button"
            disabled={loading}
            onClick={() => void onReject(item)}
            className="rounded-lg border border-red-300 text-red-700 text-xs font-semibold px-3 py-2 disabled:opacity-60"
          >
            Recusar
          </button>
        )}

        {item.canRespondReschedule && (
          <button
            type="button"
            disabled={loading}
            onClick={() => void onAcceptReschedule(item)}
            className="rounded-lg bg-primary text-white text-xs font-semibold px-3 py-2 disabled:opacity-60"
          >
            Aceitar reagendamento
          </button>
        )}

        {item.canRespondReschedule && (
          <button
            type="button"
            disabled={loading}
            onClick={() => void onRejectReschedule(item)}
            className="rounded-lg border border-red-300 text-red-700 text-xs font-semibold px-3 py-2 disabled:opacity-60"
          >
            Recusar reagendamento
          </button>
        )}

        <button
          type="button"
          onClick={() => onOpenRequest(item.serviceRequestId)}
          className="rounded-lg border border-[#d0d5dd] text-[#344054] text-xs font-semibold px-3 py-2"
        >
          Ver pedido
        </button>
      </div>
    </div>
  );
};

const AgendaOperationalCard: React.FC<{
  item: ProviderAgendaItem;
  checklist?: ProviderAppointmentChecklist;
  actionLoadingKey: string | null;
  onOpenRequest: (requestId: string) => void;
  onMarkArrival: (appointmentId: string, payload?: { latitude?: number; longitude?: number; accuracyMeters?: number; manualReason?: string }) => Promise<void>;
  onStartExecution: (appointmentId: string, reason?: string) => Promise<void>;
  onUpdateOperationalStatus: (appointmentId: string, operationalStatus: string, reason?: string) => Promise<void>;
  onLoadChecklist: (appointmentId: string) => Promise<void>;
  onUpdateChecklistItem: (appointmentId: string, payload: ProviderChecklistItemUpsertPayload) => Promise<void>;
  onUploadChecklistEvidence: (appointmentId: string, file: File) => Promise<ProviderChecklistEvidenceUploadResult>;
}> = ({
  item,
  checklist,
  actionLoadingKey,
  onOpenRequest,
  onMarkArrival,
  onStartExecution,
  onUpdateOperationalStatus,
  onLoadChecklist,
  onUpdateChecklistItem,
  onUploadChecklistEvidence
}) => {
  const [selectedOperationalStatus, setSelectedOperationalStatus] = useState('OnSite');
  const [operationalReason, setOperationalReason] = useState('');
  const [checkedByItem, setCheckedByItem] = useState<Record<string, boolean>>({});
  const [noteByItem, setNoteByItem] = useState<Record<string, string>>({});
  const [evidenceFileByItem, setEvidenceFileByItem] = useState<Record<string, File | undefined>>({});
  const [uploadedEvidenceByItem, setUploadedEvidenceByItem] = useState<Record<string, ProviderChecklistEvidenceUploadResult | undefined>>({});
  const [localError, setLocalError] = useState('');

  const appointmentBusy = isActionBusy(actionLoadingKey, item.appointmentId);

  const getChecked = (checklistItem: ProviderChecklistItem): boolean => {
    const cached = checkedByItem[checklistItem.templateItemId];
    return typeof cached === 'boolean' ? cached : checklistItem.isChecked;
  };

  const getNote = (checklistItem: ProviderChecklistItem): string => {
    const cached = noteByItem[checklistItem.templateItemId];
    return typeof cached === 'string' ? cached : (checklistItem.note || '');
  };

  const handleMarkArrival = async () => {
    const confirm = window.confirm(`Registrar chegada para ${item.windowLabel}?`);
    if (!confirm) {
      return;
    }

    const manualReason = window.prompt('Motivo manual (opcional):')?.trim();
    const location = await resolveCurrentLocation();

    await onMarkArrival(item.appointmentId, {
      latitude: location.latitude,
      longitude: location.longitude,
      accuracyMeters: location.accuracyMeters,
      manualReason: manualReason || undefined
    });
  };

  const handleStartExecution = async () => {
    const confirm = window.confirm('Confirmar inicio do atendimento?');
    if (!confirm) {
      return;
    }

    const reason = window.prompt('Observacao de inicio (opcional):')?.trim();
    await onStartExecution(item.appointmentId, reason || undefined);
  };

  const handleUpdateOperationalStatus = async () => {
    const normalizedReason = operationalReason.trim();
    if (selectedOperationalStatus === 'WaitingParts' && !normalizedReason) {
      setLocalError('Informe o motivo ao marcar status Aguardando peca.');
      return;
    }

    setLocalError('');
    await onUpdateOperationalStatus(
      item.appointmentId,
      selectedOperationalStatus,
      normalizedReason || undefined
    );
  };

  const handleUploadEvidence = async (checklistItem: ProviderChecklistItem) => {
    const file = evidenceFileByItem[checklistItem.templateItemId];
    const validationError = validateEvidenceFile(file);
    if (validationError) {
      setLocalError(validationError);
      return;
    }

    try {
      setLocalError('');
      const uploaded = await onUploadChecklistEvidence(item.appointmentId, file);
      setUploadedEvidenceByItem((current) => ({
        ...current,
        [checklistItem.templateItemId]: uploaded
      }));
    } catch {
      // erro tratado no estado global do app
    }
  };

  const handleSaveChecklistItem = async (checklistItem: ProviderChecklistItem) => {
    const uploadedEvidence = uploadedEvidenceByItem[checklistItem.templateItemId];
    const checked = getChecked(checklistItem);
    const hasEvidence = Boolean(uploadedEvidence?.fileUrl || checklistItem.evidenceUrl);
    if (checklistItem.requiresEvidence && checked && !hasEvidence) {
      setLocalError('Este item exige evidencia antes de salvar.');
      return;
    }

    setLocalError('');
    await onUpdateChecklistItem(item.appointmentId, {
      templateItemId: checklistItem.templateItemId,
      isChecked: checked,
      note: getNote(checklistItem).trim() || undefined,
      evidenceUrl: uploadedEvidence?.fileUrl ?? checklistItem.evidenceUrl,
      evidenceFileName: uploadedEvidence?.fileName ?? checklistItem.evidenceFileName,
      evidenceContentType: uploadedEvidence?.contentType ?? checklistItem.evidenceContentType,
      evidenceSizeBytes: uploadedEvidence?.sizeBytes ?? checklistItem.evidenceSizeBytes,
      clearEvidence: false
    });

    setUploadedEvidenceByItem((current) => {
      const next = { ...current };
      delete next[checklistItem.templateItemId];
      return next;
    });

    setEvidenceFileByItem((current) => {
      const next = { ...current };
      delete next[checklistItem.templateItemId];
      return next;
    });
  };

  const handleClearEvidence = async (checklistItem: ProviderChecklistItem) => {
    setLocalError('');
    await onUpdateChecklistItem(item.appointmentId, {
      templateItemId: checklistItem.templateItemId,
      isChecked: getChecked(checklistItem),
      note: getNote(checklistItem).trim() || undefined,
      clearEvidence: true
    });

    setUploadedEvidenceByItem((current) => {
      const next = { ...current };
      delete next[checklistItem.templateItemId];
      return next;
    });
  };

  const checklistBusy = isActionBusy(actionLoadingKey, item.appointmentId, 'checklist');

  return (
    <div className="rounded-xl border border-[#eaecf0] p-3 bg-[#fcfcfd]">
      <div className="flex items-start justify-between gap-2">
        <div>
          <p className="text-sm font-semibold text-[#101828]">{item.category || 'Servico'}</p>
          <p className="text-xs text-[#475467] mt-1">{item.clientName || 'Cliente'} - {item.windowLabel}</p>
          <p className="text-xs text-[#667085] mt-1">{item.street}, {item.city}</p>
        </div>
        <span className="text-[10px] px-2 py-1 rounded-full bg-blue-100 text-blue-700 font-bold uppercase">
          {item.appointmentStatusLabel}
        </span>
      </div>

      <div className="mt-3 flex flex-wrap gap-2">
        {canMarkArrival(item) && (
          <button
            type="button"
            disabled={appointmentBusy}
            onClick={() => void handleMarkArrival()}
            className="rounded-lg bg-[#0c887e] text-white text-xs font-semibold px-3 py-2 disabled:opacity-60"
          >
            Registrar chegada
          </button>
        )}

        {canStartExecution(item) && (
          <button
            type="button"
            disabled={appointmentBusy}
            onClick={() => void handleStartExecution()}
            className="rounded-lg bg-primary text-white text-xs font-semibold px-3 py-2 disabled:opacity-60"
          >
            Iniciar atendimento
          </button>
        )}

        <button
          type="button"
          onClick={() => onOpenRequest(item.serviceRequestId)}
          className="rounded-lg border border-[#d0d5dd] text-[#344054] text-xs font-semibold px-3 py-2"
        >
          Ver pedido
        </button>
      </div>

      {localError ? (
        <div className="mt-3 rounded-lg border border-red-200 bg-red-50 px-2.5 py-2 text-[11px] text-red-700">
          {localError}
        </div>
      ) : null}

      {canUpdateOperationalStatus(item) && (
        <div className="mt-3 rounded-xl border border-[#d0d5dd] bg-white p-3">
          <p className="text-xs font-semibold text-[#101828] mb-2">Status operacional</p>
          <div className="grid grid-cols-1 gap-2">
            <select
              value={selectedOperationalStatus}
              onChange={(event) => setSelectedOperationalStatus(event.target.value)}
              className="rounded-lg border border-[#d0d5dd] px-3 py-2 text-xs text-[#101828]"
              disabled={appointmentBusy}
            >
              {OPERATIONAL_STATUS_OPTIONS.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
            <input
              type="text"
              value={operationalReason}
              onChange={(event) => setOperationalReason(event.target.value)}
              placeholder="Motivo (opcional)"
              className="rounded-lg border border-[#d0d5dd] px-3 py-2 text-xs text-[#101828]"
              disabled={appointmentBusy}
            />
            <button
              type="button"
              disabled={appointmentBusy}
              onClick={() => void handleUpdateOperationalStatus()}
              className="rounded-lg border border-primary text-primary text-xs font-semibold px-3 py-2 disabled:opacity-60"
            >
              Atualizar status
            </button>
          </div>
        </div>
      )}

      <div className="mt-3 rounded-xl border border-[#d0d5dd] bg-white p-3">
        <div className="flex items-center justify-between gap-2 mb-2">
          <p className="text-xs font-semibold text-[#101828]">Checklist tecnico</p>
          {!checklist && (
            <button
              type="button"
              disabled={checklistBusy}
              onClick={() => void onLoadChecklist(item.appointmentId)}
              className="text-[11px] font-semibold text-primary disabled:opacity-60"
            >
              Carregar checklist
            </button>
          )}
        </div>

        {!checklist ? (
          <p className="text-xs text-[#667085]">Checklist ainda nao carregado para este agendamento.</p>
        ) : (
          <>
            <p className="text-[11px] text-[#475467] mb-2">
              {checklist.templateName || 'Checklist'} - {checklist.requiredCompletedCount}/{checklist.requiredItemsCount} itens obrigatorios concluidos.
            </p>

            <div className="space-y-2">
              {checklist.items.map((checklistItem) => {
                const uploadedEvidence = uploadedEvidenceByItem[checklistItem.templateItemId];

                return (
                  <div key={checklistItem.templateItemId} className="rounded-lg border border-[#eaecf0] bg-[#f9fafb] p-2">
                    <label className="flex items-start gap-2 text-xs text-[#101828] font-semibold">
                      <input
                        type="checkbox"
                        checked={getChecked(checklistItem)}
                        onChange={(event) => {
                          const checked = event.target.checked;
                          setCheckedByItem((current) => ({
                            ...current,
                            [checklistItem.templateItemId]: checked
                          }));
                        }}
                      />
                      <span>
                        {checklistItem.title}
                        {checklistItem.isRequired ? <span className="text-red-600"> *</span> : null}
                      </span>
                    </label>

                    {checklistItem.helpText ? (
                      <p className="mt-1 text-[11px] text-[#667085]">{checklistItem.helpText}</p>
                    ) : null}

                    {checklistItem.allowNote ? (
                      <input
                        type="text"
                        value={getNote(checklistItem)}
                        onChange={(event) => {
                          const note = event.target.value;
                          setNoteByItem((current) => ({
                            ...current,
                            [checklistItem.templateItemId]: note
                          }));
                        }}
                        placeholder="Observacao (opcional)"
                        className="mt-2 w-full rounded-lg border border-[#d0d5dd] px-2 py-1.5 text-xs text-[#101828]"
                      />
                    ) : null}

                    {checklistItem.evidenceUrl ? (
                      <p className="mt-2 text-[11px] text-[#475467]">
                        Evidencia atual:{' '}
                        <a
                          href={checklistItem.evidenceUrl}
                          target="_blank"
                          rel="noreferrer"
                          className="font-semibold text-primary"
                        >
                          {checklistItem.evidenceFileName || 'Abrir arquivo'}
                        </a>
                      </p>
                    ) : null}

                    {(checklistItem.requiresEvidence || checklistItem.evidenceUrl) ? (
                      <div className="mt-2 space-y-2">
                        <input
                          type="file"
                          accept=".jpg,.jpeg,.png,.webp,.mp4,.webm,.mov"
                          onChange={(event) => {
                            const file = event.target.files?.[0];
                            const validationError = validateEvidenceFile(file);
                            if (validationError) {
                              setLocalError(validationError);
                              setEvidenceFileByItem((current) => ({
                                ...current,
                                [checklistItem.templateItemId]: undefined
                              }));
                              return;
                            }

                            setLocalError('');
                            setEvidenceFileByItem((current) => ({
                              ...current,
                              [checklistItem.templateItemId]: file
                            }));
                          }}
                          className="block w-full text-[11px] text-[#475467]"
                        />
                        <button
                          type="button"
                          disabled={!evidenceFileByItem[checklistItem.templateItemId] || appointmentBusy}
                          onClick={() => void handleUploadEvidence(checklistItem)}
                          className="rounded-lg border border-primary text-primary text-[11px] font-semibold px-2 py-1 disabled:opacity-60"
                        >
                          Enviar evidencia
                        </button>
                        {uploadedEvidence ? (
                          <p className="text-[11px] text-emerald-700">
                            Upload pronto: {uploadedEvidence.fileName}
                          </p>
                        ) : null}
                      </div>
                    ) : null}

                    <div className="mt-2 flex flex-wrap gap-2">
                      <button
                        type="button"
                        disabled={appointmentBusy}
                        onClick={() => void handleSaveChecklistItem(checklistItem)}
                        className="rounded-lg bg-primary text-white text-[11px] font-semibold px-2.5 py-1.5 disabled:opacity-60"
                      >
                        Salvar item
                      </button>

                      {(checklistItem.evidenceUrl || uploadedEvidence) && (
                        <button
                          type="button"
                          disabled={appointmentBusy}
                          onClick={() => void handleClearEvidence(checklistItem)}
                          className="rounded-lg border border-red-300 text-red-700 text-[11px] font-semibold px-2.5 py-1.5 disabled:opacity-60"
                        >
                          Limpar evidencia
                        </button>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
          </>
        )}
      </div>
    </div>
  );
};

const KpiCard: React.FC<{ title: string; value: number }> = ({ title, value }) => (
  <div className="rounded-xl bg-white border border-[#e4e7ec] py-2 px-3 text-center">
    <p className="text-[11px] uppercase text-[#667085] font-semibold tracking-wide">{title}</p>
    <p className="text-lg font-bold text-[#101828]">{value}</p>
  </div>
);

export default Agenda;
