
import React, { useState, useRef, useEffect } from 'react';
import { ServiceRequest, Message } from '../types';
import { getProviderReply } from '../services/gemini';

interface Props {
  request: ServiceRequest;
  onBack: () => void;
}

const Chat: React.FC<Props> = ({ request, onBack }) => {
  const [messages, setMessages] = useState<Message[]>([
    { role: 'model', text: `Olá João! Sou o ${request.provider?.name}. Acabei de receber seu pedido para "${request.title}". Já estou separando o material e chego em breve!` }
  ]);
  const [inputText, setInputText] = useState('');
  const [isTyping, setIsTyping] = useState(false);
  const scrollRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
    }
  }, [messages, isTyping]);

  const handleSend = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!inputText.trim()) return;

    const userMsg: Message = { role: 'user', text: inputText };
    setMessages(prev => [...prev, userMsg]);
    setInputText('');
    setIsTyping(true);

    // Simulação de resposta via Gemini
    const replyText = await getProviderReply(
      request.provider?.name || 'Prestador',
      request.title,
      inputText,
      messages
    );

    setIsTyping(false);
    setMessages(prev => [...prev, { role: 'model', text: replyText }]);
  };

  return (
    <div className="flex flex-col h-screen bg-[#f0f4f4] overflow-hidden">
      {/* Header */}
      <header className="flex items-center bg-white p-4 border-b border-primary/10 sticky top-0 z-20 shadow-sm">
        <button onClick={onBack} className="p-2 -ml-2 text-primary hover:bg-primary/5 rounded-full transition-colors">
          <span className="material-symbols-outlined">arrow_back</span>
        </button>
        <div className="flex items-center gap-3 flex-1 ml-2">
          <div className="relative">
            <img src={request.provider?.avatar} className="size-10 rounded-full object-cover border border-primary/10" alt={request.provider?.name} />
            <div className="absolute bottom-0 right-0 size-3 bg-green-500 rounded-full border-2 border-white"></div>
          </div>
          <div>
            <h2 className="text-[#101818] text-sm font-bold">{request.provider?.name}</h2>
            <p className="text-[10px] text-green-600 font-bold uppercase tracking-wider">Online agora</p>
          </div>
        </div>
        <button className="p-2 text-primary hover:bg-primary/5 rounded-full">
          <span className="material-symbols-outlined">call</span>
        </button>
      </header>

      {/* Messages Area */}
      <div 
        ref={scrollRef}
        className="flex-1 overflow-y-auto p-4 space-y-4 no-scrollbar"
      >
        <div className="flex justify-center my-4">
          <span className="text-[10px] font-bold text-[#5e8d8d] bg-white px-3 py-1 rounded-full shadow-sm border border-primary/5">
            HOJE, {request.date}
          </span>
        </div>

        {messages.map((msg, i) => (
          <div 
            key={i} 
            className={`flex w-full ${msg.role === 'user' ? 'justify-end' : 'justify-start'} animate-fadeIn`}
          >
            <div className={`max-w-[80%] p-3 rounded-2xl text-sm shadow-sm ${
              msg.role === 'user' 
                ? 'bg-primary text-white rounded-tr-none' 
                : 'bg-white text-[#101818] rounded-tl-none border border-primary/5'
            }`}>
              <p className="leading-relaxed">{msg.text}</p>
              <div className={`text-[9px] mt-1 font-medium ${msg.role === 'user' ? 'text-white/70' : 'text-[#5e8d8d]'}`}>
                11:45 • Lido
              </div>
            </div>
          </div>
        ))}

        {isTyping && (
          <div className="flex justify-start">
            <div className="bg-white border border-primary/5 p-3 rounded-2xl rounded-tl-none shadow-sm flex gap-1">
              <div className="size-1.5 bg-primary/40 rounded-full animate-bounce"></div>
              <div className="size-1.5 bg-primary/60 rounded-full animate-bounce [animation-delay:0.2s]"></div>
              <div className="size-1.5 bg-primary/80 rounded-full animate-bounce [animation-delay:0.4s]"></div>
            </div>
          </div>
        )}
      </div>

      {/* Input Area */}
      <div className="p-4 bg-white border-t border-primary/10 pb-8">
        <form onSubmit={handleSend} className="flex items-center gap-2">
          <button type="button" className="p-2 text-[#5e8d8d] hover:text-primary transition-colors">
            <span className="material-symbols-outlined">add_circle</span>
          </button>
          <div className="flex-1 relative">
            <input 
              type="text" 
              value={inputText}
              onChange={(e) => setInputText(e.target.value)}
              placeholder="Digite sua mensagem..." 
              className="w-full h-12 bg-[#f5f8f8] border-none rounded-full px-4 pr-10 text-sm focus:ring-2 focus:ring-primary/20 placeholder:text-[#5e8d8d]"
            />
            <button type="button" className="absolute right-3 top-1/2 -translate-y-1/2 text-[#5e8d8d]">
              <span className="material-symbols-outlined text-xl">image</span>
            </button>
          </div>
          <button 
            type="submit"
            disabled={!inputText.trim()}
            className="size-12 bg-primary text-white rounded-full flex items-center justify-center shadow-lg shadow-primary/20 disabled:opacity-50 active:scale-90 transition-all"
          >
            <span className="material-symbols-outlined">send</span>
          </button>
        </form>
      </div>
    </div>
  );
};

export default Chat;
