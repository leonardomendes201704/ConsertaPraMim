
import { GoogleGenAI, Type } from "@google/genai";

// Fix: Moved GoogleGenAI instantiation inside functions to ensure it uses the current environment context/API key.
export const getAIDiagnostic = async (userDescription: string) => {
  const ai = new GoogleGenAI({ apiKey: process.env.API_KEY });
  try {
    const response = await ai.models.generateContent({
      model: "gemini-3-flash-preview",
      contents: `O usuário descreveu o seguinte problema doméstico: "${userDescription}". 
      Por favor, forneça:
      1. Um resumo técnico curto e profissional para o prestador de serviço.
      2. Uma lista curta de possíveis causas.
      3. Instruções de segurança rápidas.
      
      Responda em formato JSON.`,
      config: {
        responseMimeType: "application/json",
        responseSchema: {
          type: Type.OBJECT,
          properties: {
            summary: { type: Type.STRING },
            possibleCauses: { type: Type.ARRAY, items: { type: Type.STRING } },
            safetyInstructions: { type: Type.ARRAY, items: { type: Type.STRING } }
          },
          required: ["summary", "possibleCauses", "safetyInstructions"]
        }
      }
    });

    // Fix: Access the .text property directly (not a method). Added safe fallback for JSON parsing.
    const text = response.text?.trim() || '{}';
    return JSON.parse(text);
  } catch (error) {
    console.error("Gemini Error:", error);
    return null;
  }
};

export const getProviderReply = async (providerName: string, serviceTitle: string, userMessage: string, history: {role: 'user' | 'model', text: string}[]) => {
  const ai = new GoogleGenAI({ apiKey: process.env.API_KEY });
  try {
    const chat = ai.chats.create({
      model: "gemini-3-flash-preview",
      config: {
        systemInstruction: `Você é ${providerName}, um profissional experiente em ${serviceTitle}. 
        Um cliente está falando com você pelo chat do app "Conserta Pra Mim".
        Seja educado, profissional e direto. 
        Se o cliente perguntar algo técnico, dê uma dica rápida. 
        Se perguntar onde você está, diga que está a caminho ou organizando as ferramentas.
        Mantenha as respostas curtas (máximo 2 parágrafos).`,
      }
    });

    // Fix: Using the correct sendMessage structure for the @google/genai SDK.
    const response = await chat.sendMessage({ message: userMessage });
    // Fix: Accessing .text as a property.
    return response.text || "Olá! Tive um problema na conexão, mas estou aqui.";
  } catch (error) {
    console.error("Chat Error:", error);
    return "Olá! Tive um problema na conexão, mas já estou visualizando sua mensagem. Chego em breve!";
  }
};