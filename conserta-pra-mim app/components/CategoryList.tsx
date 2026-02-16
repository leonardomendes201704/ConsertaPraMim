
import React, { useState } from 'react';

const ALL_CATEGORIES = [
  { id: '1', name: 'Elétrica', icon: 'bolt' },
  { id: '2', name: 'Hidráulica', icon: 'water_drop' },
  { id: '3', name: 'Montagem', icon: 'construction' },
  { id: '4', name: 'Pintura', icon: 'format_paint' },
  { id: '5', name: 'Limpeza', icon: 'cleaning_services' },
  { id: '6', name: 'Ar Condicionado', icon: 'ac_unit' },
  { id: '7', name: 'Jardinagem', icon: 'yard' },
  { id: '8', name: 'Pedreiro', icon: 'foundation' },
  { id: '9', name: 'Chaveiro', icon: 'vpn_key' },
  { id: '10', name: 'Eletrodomésticos', icon: 'kitchen' },
  { id: '11', name: 'Informática', icon: 'laptop_mac' },
  { id: '12', name: 'Faz Tudo', icon: 'handyman' },
  { id: '13', name: 'Pisos e Azulejos', icon: 'grid_view' },
  { id: '14', name: 'Gesso e Drywall', icon: 'layers' },
  { id: '15', name: 'Vidraçaria', icon: 'window' },
  { id: '16', name: 'Segurança', icon: 'videocam' },
  { id: '17', name: 'Dedetização', icon: 'pest_control' },
  { id: '18', name: 'Fretes e Mudanças', icon: 'local_shipping' }
];

interface Props {
  onBack: () => void;
  onSelectCategory: (id: string) => void;
}

const CategoryList: React.FC<Props> = ({ onBack, onSelectCategory }) => {
  const [searchTerm, setSearchTerm] = useState('');

  const filteredCategories = ALL_CATEGORIES.filter(cat => 
    cat.name.toLowerCase().includes(searchTerm.toLowerCase())
  );

  return (
    <div className="flex flex-col h-screen bg-white overflow-hidden">
      {/* Header */}
      <header className="flex flex-col bg-white px-4 pt-6 pb-4 sticky top-0 z-20 border-b border-primary/5 shadow-sm">
        <div className="flex items-center justify-between mb-6">
          <button onClick={onBack} className="p-2 -ml-2 text-primary hover:bg-primary/5 rounded-full transition-colors">
            <span className="material-symbols-outlined">arrow_back</span>
          </button>
          <h1 className="text-xl font-bold text-[#101818]">Todas Categorias</h1>
          <div className="w-10"></div> {/* Spacer for alignment */}
        </div>
        
        {/* Search Bar */}
        <div className="relative">
          <span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-[#5e8d8d] text-xl">search</span>
          <input 
            type="text" 
            placeholder="Qual serviço você procura?"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="w-full h-12 bg-background-light border-none rounded-xl pl-10 pr-4 text-sm focus:ring-2 focus:ring-primary/20 placeholder:text-[#5e8d8d]"
          />
        </div>
      </header>

      {/* Grid Area */}
      <div className="flex-1 overflow-y-auto no-scrollbar p-4 pb-10 bg-background-light/30">
        {filteredCategories.length === 0 ? (
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

      {/* Info Footer */}
      <div className="p-4 bg-white border-t border-primary/5 text-center">
        <p className="text-[10px] text-[#5e8d8d] font-medium">
          Não encontrou o que precisava? <button className="text-primary font-bold">Fale conosco</button>
        </p>
      </div>
    </div>
  );
};

export default CategoryList;
