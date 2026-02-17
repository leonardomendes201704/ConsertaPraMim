import React, { useEffect, useMemo, useState } from 'react';
import { AuthSession, ServiceRequest, ServiceRequestCategoryOption } from '../types';
import {
  createMobileServiceRequest,
  fetchMobileServiceRequestCategories,
  MobileServiceRequestError,
  resolveMobileServiceRequestZip
} from '../services/mobileServiceRequests';

interface Props {
  authSession: AuthSession | null;
  categoryId: string | null;
  onCancel: () => void;
  onFinish: (newRequest?: ServiceRequest) => void;
}

const MIN_DESCRIPTION_LENGTH = 8;

function onlyDigits(value: string): string {
  return (value || '').replace(/\D/g, '');
}

function formatZip(value: string): string {
  const digits = onlyDigits(value).slice(0, 8);
  if (digits.length <= 5) {
    return digits;
  }

  return `${digits.slice(0, 5)}-${digits.slice(5)}`;
}

const ServiceRequestFlow: React.FC<Props> = ({ authSession, categoryId, onCancel, onFinish }) => {
  const [step, setStep] = useState(1);
  const [categories, setCategories] = useState<ServiceRequestCategoryOption[]>([]);
  const [categoriesLoading, setCategoriesLoading] = useState(false);

  const [selectedCategoryId, setSelectedCategoryId] = useState('');
  const [description, setDescription] = useState('');
  const [zipCode, setZipCode] = useState('');
  const [street, setStreet] = useState('');
  const [city, setCity] = useState('');

  const [zipStatus, setZipStatus] = useState('Informe o CEP para preencher o endereco automaticamente.');
  const [zipStatusError, setZipStatusError] = useState(false);
  const [zipLoading, setZipLoading] = useState(false);

  const [submitting, setSubmitting] = useState(false);
  const [globalError, setGlobalError] = useState('');
  const [successMessage, setSuccessMessage] = useState('Pedido criado com sucesso!');
  const [createdRequest, setCreatedRequest] = useState<ServiceRequest | null>(null);

  useEffect(() => {
    let isActive = true;

    const loadCategories = async () => {
      if (!authSession?.token) {
        setGlobalError('Sessao invalida para abrir pedido. Faca login novamente.');
        return;
      }

      setCategoriesLoading(true);
      setGlobalError('');

      try {
        const result = await fetchMobileServiceRequestCategories(authSession.token);
        if (!isActive) {
          return;
        }

        setCategories(result);
        if (result.length === 0) {
          setGlobalError('Nenhuma categoria ativa disponivel no momento.');
          setSelectedCategoryId('');
          return;
        }

        if (categoryId && result.some(category => category.id === categoryId)) {
          setSelectedCategoryId(categoryId);
          return;
        }

        setSelectedCategoryId(result[0].id);
      } catch (error) {
        if (!isActive) {
          return;
        }

        if (error instanceof MobileServiceRequestError) {
          setGlobalError(error.message);
        } else {
          setGlobalError('Nao foi possivel carregar as categorias agora.');
        }
      } finally {
        if (isActive) {
          setCategoriesLoading(false);
        }
      }
    };

    void loadCategories();
    return () => {
      isActive = false;
    };
  }, [authSession?.token, categoryId]);

  const selectedCategory = useMemo(
    () => categories.find(category => category.id === selectedCategoryId) || null,
    [categories, selectedCategoryId]);

  const canProceedStep1 = selectedCategoryId.length > 0 && description.trim().length >= MIN_DESCRIPTION_LENGTH;

  const setZipStatusMessage = (message: string, isError: boolean) => {
    setZipStatus(message);
    setZipStatusError(isError);
  };

  const clearResolvedAddress = () => {
    setStreet('');
    setCity('');
  };

  const resolveZip = async (): Promise<boolean> => {
    if (!authSession?.token) {
      setZipStatusMessage('Sessao invalida para consultar CEP.', true);
      return false;
    }

    const normalizedZip = onlyDigits(zipCode);
    if (normalizedZip.length !== 8) {
      clearResolvedAddress();
      setZipStatusMessage('Informe um CEP valido com 8 digitos.', true);
      return false;
    }

    setZipLoading(true);
    setZipStatusMessage('Buscando endereco...', false);

    try {
      const resolved = await resolveMobileServiceRequestZip(authSession.token, normalizedZip);
      setZipCode(formatZip(resolved.zipCode));
      setStreet(resolved.street);
      setCity(resolved.city);
      setZipStatusMessage('Endereco preenchido automaticamente.', false);
      return true;
    } catch (error) {
      clearResolvedAddress();

      if (error instanceof MobileServiceRequestError) {
        setZipStatusMessage(error.message, true);
      } else {
        setZipStatusMessage('Erro ao consultar CEP. Tente novamente.', true);
      }

      return false;
    } finally {
      setZipLoading(false);
    }
  };

  const goBack = () => {
    if (step === 1) {
      onCancel();
      return;
    }

    setStep(previous => Math.max(1, previous - 1));
    setGlobalError('');
  };

  const goNextFromStep1 = () => {
    if (!canProceedStep1) {
      setGlobalError('Selecione uma categoria e descreva o problema com mais detalhes.');
      return;
    }

    setGlobalError('');
    setStep(2);
  };

  const goNextFromStep2 = async () => {
    const resolved = await resolveZip();
    if (!resolved) {
      return;
    }

    setGlobalError('');
    setStep(3);
  };

  const handleSubmit = async () => {
    if (!authSession?.token) {
      setGlobalError('Sessao invalida para criar pedido. Faca login novamente.');
      return;
    }

    setSubmitting(true);
    setGlobalError('');

    try {
      const result = await createMobileServiceRequest(authSession.token, {
        categoryId: selectedCategoryId,
        description: description.trim(),
        zipCode,
        street,
        city
      });

      setSuccessMessage(result.message);
      setCreatedRequest(result.order);
      setStep(4);
    } catch (error) {
      if (error instanceof MobileServiceRequestError) {
        setGlobalError(error.message);
      } else {
        setGlobalError('Nao foi possivel criar o pedido agora.');
      }
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="flex flex-col h-screen bg-white">
      <header className="flex items-center p-4 border-b border-primary/5 sticky top-0 bg-white z-10">
        <button onClick={goBack} className="text-primary hover:bg-primary/5 p-2 rounded-full transition-colors">
          <span className="material-symbols-outlined">arrow_back</span>
        </button>
        <h2 className="text-[#101818] text-base font-bold flex-1 text-center pr-10">
          Novo pedido
        </h2>
      </header>

      <div className="px-6 pt-5">
        <div className="flex items-center justify-between mb-2">
          <span className={`text-xs font-bold ${step === 1 ? 'text-primary' : 'text-[#5e8d8d]'}`}>1. O que precisa?</span>
          <span className={`text-xs font-bold ${step === 2 ? 'text-primary' : 'text-[#5e8d8d]'}`}>2. Onde?</span>
          <span className={`text-xs font-bold ${step === 3 ? 'text-primary' : 'text-[#5e8d8d]'}`}>3. Revisar</span>
        </div>
        <div className="h-1.5 rounded-full bg-primary/10 overflow-hidden">
          <div
            className="h-full bg-primary transition-all duration-300"
            style={{ width: `${Math.min(100, Math.max(0, (step / 3) * 100))}%` }}
          />
        </div>
      </div>

      <div className="flex-1 p-6 overflow-y-auto no-scrollbar">
        {globalError && step !== 4 && (
          <div className="mb-4 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
            {globalError}
          </div>
        )}

        {step === 1 && (
          <div className="space-y-6 pb-10">
            <div>
              <h1 className="text-2xl font-bold text-primary mb-2">Descreva seu problema</h1>
              <p className="text-sm text-[#5e8d8d]">Escolha a categoria e conte o que precisa ser feito.</p>
            </div>

            <div className="space-y-3">
              <h3 className="text-xs font-bold text-[#5e8d8d] uppercase tracking-wider">Categoria</h3>
              {categoriesLoading ? (
                <div className="flex items-center gap-2 text-sm text-[#5e8d8d]">
                  <span className="material-symbols-outlined animate-spin">progress_activity</span>
                  Carregando categorias...
                </div>
              ) : (
                <div className="grid grid-cols-2 gap-3">
                  {categories.map(category => (
                    <button
                      key={category.id}
                      onClick={() => setSelectedCategoryId(category.id)}
                      className={`rounded-2xl border p-3 flex items-center gap-3 text-left transition-all active:scale-95 ${
                        selectedCategoryId === category.id
                          ? 'border-primary bg-primary/5'
                          : 'border-primary/10 bg-white hover:border-primary/30'
                      }`}
                    >
                      <span className="size-10 rounded-xl bg-primary/10 text-primary flex items-center justify-center shrink-0">
                        <span className="material-symbols-outlined">{category.icon}</span>
                      </span>
                      <span className="text-xs font-bold text-[#101818] leading-tight">{category.name}</span>
                    </button>
                  ))}
                </div>
              )}
            </div>

            <div className="space-y-2">
              <h3 className="text-xs font-bold text-[#5e8d8d] uppercase tracking-wider">Descricao</h3>
              <textarea
                className="w-full h-40 p-4 border border-[#dae7e7] rounded-xl focus:ring-2 focus:ring-primary/20 focus:border-primary resize-none text-sm placeholder:text-[#5e8d8d]"
                placeholder="Ex: Torneira da cozinha pingando ha dois dias, preciso de troca do reparo..."
                value={description}
                onChange={(event) => setDescription(event.target.value)}
              />
              <p className="text-[11px] text-[#5e8d8d]">
                Minimo de {MIN_DESCRIPTION_LENGTH} caracteres. Atual: {description.trim().length}
              </p>
            </div>

            <button
              onClick={goNextFromStep1}
              disabled={!canProceedStep1}
              className="w-full bg-primary text-white h-14 rounded-xl font-bold disabled:opacity-50 shadow-lg shadow-primary/20 active:scale-[0.98] transition-all"
            >
              Continuar
            </button>
          </div>
        )}

        {step === 2 && (
          <div className="space-y-6 pb-10">
            <div>
              <h1 className="text-2xl font-bold text-primary mb-2">Onde o servico sera realizado?</h1>
              <p className="text-sm text-[#5e8d8d]">Informe o CEP para preencher endereco automaticamente.</p>
            </div>

            <div className="space-y-2">
              <label className="text-xs font-bold text-[#5e8d8d] uppercase tracking-wider">CEP</label>
              <div className="flex items-stretch gap-2">
                <input
                  type="text"
                  value={zipCode}
                  onChange={(event) => {
                    const formatted = formatZip(event.target.value);
                    setZipCode(formatted);

                    if (onlyDigits(formatted).length < 8) {
                      clearResolvedAddress();
                      setZipStatusMessage('Informe o CEP para preencher o endereco automaticamente.', false);
                    }
                  }}
                  maxLength={9}
                  inputMode="numeric"
                  className="flex-1 h-12 p-3 border border-[#dae7e7] rounded-xl text-sm focus:ring-2 focus:ring-primary/20 focus:border-primary"
                  placeholder="00000-000"
                />
                <button
                  type="button"
                  onClick={() => void resolveZip()}
                  disabled={zipLoading}
                  className="h-12 px-4 rounded-xl bg-primary text-white text-sm font-bold disabled:opacity-50 disabled:cursor-not-allowed active:scale-[0.98] transition-all"
                >
                  {zipLoading ? 'Buscando...' : 'Buscar'}
                </button>
              </div>
              <p className={`text-xs ${zipStatusError ? 'text-red-600' : 'text-[#5e8d8d]'}`}>{zipStatus}</p>
            </div>

            <div className="space-y-2">
              <label className="text-xs font-bold text-[#5e8d8d] uppercase tracking-wider">Rua / Logradouro</label>
              <input
                type="text"
                value={street}
                readOnly
                className="w-full h-12 p-3 border border-[#dae7e7] rounded-xl text-sm bg-background-light/60"
                placeholder="Preenchido automaticamente"
              />
            </div>

            <div className="space-y-2">
              <label className="text-xs font-bold text-[#5e8d8d] uppercase tracking-wider">Cidade</label>
              <input
                type="text"
                value={city}
                readOnly
                className="w-full h-12 p-3 border border-[#dae7e7] rounded-xl text-sm bg-background-light/60"
                placeholder="Preenchido automaticamente"
              />
            </div>

            <button
              onClick={goNextFromStep2}
              disabled={zipLoading}
              className="w-full bg-primary text-white h-14 rounded-xl font-bold disabled:opacity-50 shadow-lg shadow-primary/20 active:scale-[0.98] transition-all"
            >
              {zipLoading ? 'Buscando endereco...' : 'Revisar solicitacao'}
            </button>
          </div>
        )}

        {step === 3 && (
          <div className="space-y-6 pb-10">
            <div>
              <h1 className="text-2xl font-bold text-primary mb-2">Quase la! Revise os detalhes.</h1>
              <p className="text-sm text-[#5e8d8d]">Confirme as informacoes antes de publicar seu chamado.</p>
            </div>

            <div className="rounded-2xl border border-primary/10 bg-background-light/40 p-4 space-y-4">
              <div>
                <p className="text-[11px] font-bold uppercase tracking-wider text-[#5e8d8d] mb-1">Categoria</p>
                <p className="text-sm font-semibold text-[#101818]">{selectedCategory?.name || 'Nao informada'}</p>
              </div>

              <div>
                <p className="text-[11px] font-bold uppercase tracking-wider text-[#5e8d8d] mb-1">Problema</p>
                <p className="text-sm text-[#3f4f4f]">{description.trim()}</p>
              </div>

              <div>
                <p className="text-[11px] font-bold uppercase tracking-wider text-[#5e8d8d] mb-1">Endereco</p>
                <p className="text-sm text-[#3f4f4f]">{street}, {city} - CEP {zipCode}</p>
              </div>
            </div>

            <div className="rounded-xl border border-blue-100 bg-blue-50 px-4 py-3 text-sm text-blue-700">
              Apos publicar, profissionais proximos receberao seu chamado e enviarao propostas.
            </div>

            <button
              onClick={handleSubmit}
              disabled={submitting}
              className="w-full bg-emerald-600 text-white h-14 rounded-xl font-bold disabled:opacity-50 shadow-lg shadow-emerald-500/20 active:scale-[0.98] transition-all"
            >
              {submitting ? 'Publicando pedido...' : 'Publicar chamado'}
            </button>
          </div>
        )}

        {step === 4 && (
          <div className="flex flex-col items-center justify-center h-full text-center space-y-8 animate-fadeIn">
            <div className="relative">
              <div className="absolute inset-0 bg-green-500/20 rounded-full blur-2xl animate-pulse"></div>
              <div className="size-28 bg-green-100 text-green-600 rounded-full flex items-center justify-center relative border-4 border-white shadow-xl">
                <span className="material-symbols-outlined text-6xl material-symbols-fill">check_circle</span>
              </div>
            </div>

            <div className="space-y-2">
              <h1 className="text-2xl font-bold text-gray-900">Pedido enviado!</h1>
              <p className="text-gray-600 text-sm leading-relaxed px-4">{successMessage}</p>
            </div>

            <div className="w-full max-w-[280px] bg-background-light p-4 rounded-2xl border border-primary/5 space-y-3">
              <div className="flex items-center justify-between text-xs">
                <span className="text-[#5e8d8d] font-medium">Protocolo:</span>
                <span className="text-primary font-bold">#{createdRequest?.id}</span>
              </div>
              <div className="flex items-center justify-between text-xs">
                <span className="text-[#5e8d8d] font-medium">Status:</span>
                <span className="bg-blue-100 text-blue-600 px-2 py-0.5 rounded-full font-bold uppercase text-[9px]">{createdRequest?.status || 'AGUARDANDO'}</span>
              </div>
            </div>

            <button
              onClick={() => onFinish(createdRequest || undefined)}
              className="w-full bg-primary text-white h-14 rounded-xl font-bold shadow-lg shadow-primary/20 active:scale-[0.98] transition-all"
            >
              Voltar ao inicio
            </button>
          </div>
        )}
      </div>
    </div>
  );
};

export default ServiceRequestFlow;
