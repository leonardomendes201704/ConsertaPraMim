# Checklist QA E2E - Conserta Pra Mim App (Web React)

## Objetivo

Validar os fluxos principais do app cliente web, com foco em navegacao, estabilidade visual, integracao IA e consistencia de estados locais.

## Ambiente

- Node.js instalado
- App rodando em `npm run dev`
- `.env.local` com `GEMINI_API_KEY` valida para testes de IA

## Bloco A - Inicializacao

- [ ] A1. App abre na tela splash.
- [ ] A2. Splash avanca automaticamente para onboarding em ~2.5s.
- [ ] A3. Layout inicial respeita viewport mobile (max width).

## Bloco B - Onboarding e Auth

- [ ] B1. Navegacao dos 3 slides funciona.
- [ ] B2. Botao "Pular" abre Auth.
- [ ] B3. Tela de Auth abre com e-mail e senha (sem telefone/codigo).
- [ ] B4. Submit de Auth autentica na API e redireciona para dashboard.
- [ ] B5. Em API indisponivel, Auth mostra tela de manutencao com codigo tecnico.

## Bloco C - Dashboard e categorias

- [ ] C1. Dashboard mostra cards e menu inferior.
- [ ] C2. Contador de notificacoes nao lidas aparece no sino.
- [ ] C3. "Ver todas" abre lista completa de categorias.
- [ ] C4. Busca de categoria filtra corretamente.
- [ ] C5. Ao chegar proposta nova em tempo real, contador do sino incrementa sem refresh.

## Bloco D - Novo pedido (paridade com portal)

- [ ] D1. "Pedir um Servico" abre fluxo de criacao.
- [ ] D2. Etapa 1 exige categoria valida + descricao minima.
- [ ] D3. Etapa 2 resolve CEP via API mobile e preenche rua/cidade.
- [ ] D4. Etapa 3 exibe revisao final antes de publicar chamado.
- [ ] D5. Publicacao usa `POST /api/mobile/client/service-requests` e retorna protocolo.
- [ ] D6. Apos sucesso, pedido aparece em "Pedidos" sem depender de mock.

## Bloco E - Detalhes e chat

- [ ] E1. Abertura de detalhes pelo dashboard funciona.
- [ ] E2. Fluxo operacional (`flowSteps`) e exibido no detalhe.
- [ ] E3. Historico cronologico (`timeline`) e exibido no detalhe.
- [ ] E4. Em falha no detalhe, app exibe mensagem + botao de recarregar historico.
- [ ] E5. Botao chat abre conversa vinculada ao pedido.
- [ ] E6. Envio de mensagem no chat gera resposta IA do prestador.
- [ ] E7. Evento "Proposta recebida" na timeline e clicavel e abre tela de detalhes da proposta.
- [ ] E8. Tela de detalhes da proposta mostra prestador, valor, mensagem e status comercial.
- [ ] E9. Em detalhes da proposta, botao "Conversar com o prestador" abre chat com o prestador correto.
- [ ] E10. Em detalhes da proposta, botao "Aceitar proposta" chama endpoint mobile dedicado e atualiza status para `Aceita`.

## Bloco F - Pedidos

- [ ] F1. Tela "Pedidos" abre com tabs Ativos/Historico.
- [ ] F2. A listagem de "Pedidos" vem da API (`GET /api/mobile/client/orders`).
- [ ] F3. Contagem da aba Ativos bate com `openOrders`.
- [ ] F4. Contagem da aba Historico bate com `finalizedOrders`.
- [ ] F5. Card de pedido e clicavel e abre detalhes.
- [ ] F6. Status visual no card bate com estado do pedido.
- [ ] F7. Em erro da API de pedidos, a tela mostra mensagem amigavel com retry.
- [ ] F8. Badge de propostas por pedido exibe mesmo valor retornado em `proposalCount`.

## Bloco G - Finalizacao de servico

- [ ] G1. Em pedido "EM_ANDAMENTO", botao "Finalizar e Pagar" aparece.
- [ ] G2. Fluxo de finalizacao segue passos 1/2/3 sem quebra.
- [ ] G3. Selecao de metodo de pagamento obrigatoria para avancar.
- [ ] G4. Avaliacao com estrela obrigatoria para concluir.
- [ ] G5. Ao concluir, pedido muda para `CONCLUIDO`.

## Bloco H - Notificacoes

- [ ] H1. Tela de notificacoes lista itens por tipo.
- [ ] H2. Clique em notificacao marca como lida.
- [ ] H3. Se notificacao tiver `requestId`, abre detalhes do pedido.
- [ ] H4. Botao "Limpar" remove todas.
- [ ] H5. Quando prestador envia proposta, app exibe toast realtime e cria item no sino.

## Bloco I - Perfil

- [ ] I1. Perfil abre e permite editar campos locais.
- [ ] I2. Busca de CEP simulado atualiza endereco apos delay.
- [ ] I3. Toggle de periodos (manha/tarde/noite) funciona.
- [ ] I4. Logout retorna para tela Auth.

## Bloco J - Regressao visual e usabilidade

- [ ] J1. Nenhuma tela sobrepoe footer/navigation de forma indevida.
- [ ] J2. Scroll interno funciona sem quebrar header fixo.
- [ ] J3. Botoes principais tem feedback visual (active/hover).
- [ ] J4. Nao ha travamentos ao trocar rapido entre telas.

## Bloco K - Riscos conhecidos para registrar

- [ ] K1. Confirmar se ainda existe texto com encoding incorreto (mojibake).
- [ ] K2. Confirmar que a chave Gemini nao aparece em logs/screenshots de QA.
- [ ] K3. Confirmar fallback em caso de erro Gemini (mensagens de erro tratadas).

## Resultado final

- [ ] Aprovado para demo interna.
- [ ] Aprovado para integracao backend.
- [ ] Pendente de correcao critica.

## Data da revisao

- 2026-02-17
