import React, { useEffect, useMemo, useState } from 'react';
import { AuthSession, ServiceRequestCategoryOption } from '../types';
import { fetchMobileServiceRequestCategories, MobileServiceRequestError } from '../services/mobileServiceRequests';

interface Props {
  authSession: AuthSession | null;
  onBack: () => void;
  onSelectCategory: (id: string) => void;
}

const CategoryList: React.FC<Props> = ({ authSession, onBack, onSelectCategory }) => {
  const [searchTerm, setSearchTerm] = useState('');
  const [categories, setCategories] = useState<ServiceRequestCategoryOption[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');
  const [retryNonce, setRetryNonce] = useState(0);

  useEffect(() => {
    let isActive = true;

    const loadCategories = async () => {
      if (!authSession?.token) {
        setCategories([]);
        setErrorMessage('Sessao invalida para carregar categorias.');
        return;
      }

      setIsLoading(true);
      setErrorMessage('');

      try {
        const result = await fetchMobileServiceRequestCategories(authSession.token);
        if (!isActive) {
          return;
        }

        setCategories(result);
      } catch (error) {
        if (!isActive) {
          return;
        }

        if (error instanceof MobileServiceRequestError) {
          setErrorMessage(error.message);
        } else {
          setErrorMessage('Nao foi possivel carregar as categorias agora.');
        }
      } finally {
        if (isActive) {
          setIsLoading(false);
        }
      }
    };

    void loadCategories();
    return () => {
      isActive = false;
    };
  }, [authSession?.token, retryNonce]);

  const filteredCategories = useMemo(
    () => categories.filter(cat => cat.name.toLowerCase().includes(searchTerm.toLowerCase())),
    [categories, searchTerm]);

  return (
    <div className="flex flex-col h-screen bg-white overflow-hidden">
      <header className="flex flex-col bg-white px-4 pt-6 pb-4 sticky top-0 z-20 border-b border-primary/5 shadow-sm">
        <div className="flex items-center justify-between mb-6">
          <button onClick={onBack} className="p-2 -ml-2 text-primary hover:bg-primary/5 rounded-full transition-colors">
            <span className="material-symbols-outlined">arrow_back</span>
          </button>
          <h1 className="text-xl font-bold text-[#101818]">Todas Categorias</h1>
          <div className="w-10"></div>
        </div>

        <div className="relative">
          <span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-[#5e8d8d] text-xl">search</span>
          <input
            type="text"
            placeholder="Qual servico voce procura?"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="w-full h-12 bg-background-light border-none rounded-xl pl-10 pr-4 text-sm focus:ring-2 focus:ring-primary/20 placeholder:text-[#5e8d8d]"
          />
        </div>
      </header>

      <div className="flex-1 overflow-y-auto no-scrollbar p-4 pb-10 bg-background-light/30">
        {isLoading ? (
          <div className="flex flex-col items-center justify-center py-20 text-[#5e8d8d]">
            <span className="material-symbols-outlined text-5xl mb-2 animate-spin">progress_activity</span>
            <p className="font-medium">Carregando categorias...</p>
          </div>
        ) : errorMessage ? (
          <div className="flex flex-col items-center justify-center py-20 text-center text-[#5e8d8d]">
            <span className="material-symbols-outlined text-5xl mb-2 text-orange-500">warning</span>
            <p className="font-bold text-[#101818]">Falha ao carregar categorias</p>
            <p className="text-xs mt-1 mb-4 max-w-[260px]">{errorMessage}</p>
            <button
              onClick={() => setRetryNonce(previous => previous + 1)}
              className="px-4 py-2 text-xs font-bold rounded-full bg-primary text-white active:scale-95 transition-transform">
              Tentar novamente
            </button>
          </div>
        ) : filteredCategories.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-20 text-[#5e8d8d]">
            <span className="material-symbols-outlined text-5xl mb-2">search_off</span>
            <p className="font-medium">Nenhuma categoria encontrada</p>
          </div>
        ) : (
          <div className="grid grid-cols-3 gap-3">
            {filteredCategories.map((cat) => (
              <button
                key={cat.id}
                onClick={() => onSelectCategory(cat.id)}
                className="flex flex-col items-center justify-center gap-3 p-4 bg-white rounded-2xl border border-primary/5 shadow-sm hover:border-primary/20 active:scale-95 transition-all group"
              >
                <div className="size-14 rounded-full bg-primary/5 text-primary flex items-center justify-center group-hover:bg-primary group-hover:text-white transition-colors">
                  <span className="material-symbols-outlined text-2xl">{cat.icon}</span>
                </div>
                <span className="text-[11px] font-bold text-[#101818] text-center leading-tight">
                  {cat.name}
                </span>
              </button>
            ))}
          </div>
        )}
      </div>

      <div className="p-4 bg-white border-t border-primary/5 text-center">
        <p className="text-[10px] text-[#5e8d8d] font-medium">
          Nao encontrou o que precisava? <button className="text-primary font-bold">Fale conosco</button>
        </p>
      </div>
    </div>
  );
};

export default CategoryList;
