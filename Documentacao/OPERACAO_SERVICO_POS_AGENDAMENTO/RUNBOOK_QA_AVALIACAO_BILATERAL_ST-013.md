# RUNBOOK QA - AVALIACAO BILATERAL E REPUTACAO (ST-013)

## 1. Objetivo

Padronizar a validacao funcional e tecnica do fluxo de avaliacao bilateral (cliente -> prestador e prestador -> cliente), incluindo reputacao publica, denuncia de comentario e moderacao administrativa.

## 2. Escopo

Esta versao cobre:

- envio de avaliacao pelo cliente para o prestador vencedor;
- envio de avaliacao pelo prestador para o cliente do pedido;
- bloqueio por inelegibilidade (status, pagamento, janela e autoria);
- bloqueio de duplicidade por parte/revisor;
- denuncia de comentario e fila de moderacao;
- decisao de moderacao com manutencao de comentario ou ocultacao;
- reflexo de reputacao em perfis publicos e dashboard admin.

## 3. Endpoints principais

Base de API: `/api/reviews`

- `POST /api/reviews/client`
  - role: `Client`
  - uso: cliente avalia prestador
- `POST /api/reviews/provider`
  - role: `Provider`
  - uso: prestador avalia cliente
- `GET /api/reviews/provider/{providerId}`
  - role: autenticado
  - uso: lista avaliacoes recebidas pelo prestador
- `GET /api/reviews/summary/provider/{providerId}`
  - role: autenticado
  - uso: resumo de notas do prestador
- `GET /api/reviews/summary/client/{clientId}`
  - role: autenticado
  - uso: resumo de notas do cliente
- `POST /api/reviews/{reviewId}/report`
  - role: `Client`, `Provider` ou `Admin`
  - uso: denuncia de comentario

Base de moderacao admin: `/api/adminreviews`

- `GET /api/adminreviews/reported`
  - role: `Admin`
  - uso: listar denuncias pendentes
- `POST /api/adminreviews/{reviewId}/moderate`
  - role: `Admin`
  - uso: decidir `KeepVisible` ou `HideComment`

Portais web usados no QA E2E:

- cliente: `POST /ServiceRequests/SubmitProviderReview`
- prestador: `POST /ServiceRequests/SubmitClientReview`

## 4. Regras de negocio obrigatorias

1. Pedido precisa estar `Completed` ou `Validated`.
2. Pedido precisa ter pagamento `Paid`.
3. Janela de avaliacao precisa estar aberta (config `Reviews:EvaluationWindowDays`, fallback 30 dias).
4. Cada parte so pode avaliar 1 vez por pedido.
5. Cliente so avalia o prestador com proposta aceita.
6. Prestador so avalia o cliente do pedido dele.
7. Denuncia nao pode ser feita pelo autor da propria avaliacao.
8. Comentario ocultado por moderacao deve aparecer publicamente como: `Comentario removido pela moderacao.`

## 5. Pre-condicoes de ambiente

1. Banco com seed aplicado e usuarios ativos: `Client`, `Provider`, `Admin`.
2. Pedido de teste com proposta aceita, atendimento concluido e pagamento `Paid`.
3. Sessoes autenticadas nos 3 portais (cliente, prestador, admin).
4. Swagger/Postman disponivel para validacao direta dos endpoints.

## 6. Roteiro QA E2E

### 6.1 Fluxo feliz completo

1. Cliente abre `ServiceRequests/Details/{id}` e envia avaliacao para prestador (nota + comentario).
2. Prestador abre `ServiceRequests/Details/{id}` e envia avaliacao para cliente.
3. Validar retorno de sucesso nas duas acoes.
4. Abrir perfil publico do prestador e do cliente.
5. Validar exibicao da reputacao e comentario.
6. No dashboard admin, validar atualizacao de ranking e outliers.

Resultado esperado: avaliacao bilateral concluida, reputacao visivel e dados refletidos no admin.

### 6.2 Duplicidade bloqueada por parte

1. Repetir envio de avaliacao do mesmo ator para o mesmo pedido.
2. Resultado esperado: erro de regra (`400`), sem segunda avaliacao persistida.

### 6.3 Inelegibilidade por pagamento ausente

1. Usar pedido concluido sem transacao `Paid`.
2. Tentar enviar avaliacao no cliente e no prestador.
3. Resultado esperado: bloqueio (`400`) em ambos os lados.

### 6.4 Inelegibilidade por janela expirada

1. Usar pedido com referencia de conclusao fora da janela configurada.
2. Tentar enviar avaliacao.
3. Resultado esperado: bloqueio (`400`) por prazo expirado.

### 6.5 Seguranca de autoria

1. Cliente A tenta avaliar pedido do Cliente B.
2. Prestador X tenta avaliar pedido cuja proposta aceita e de Prestador Y.
3. Resultado esperado: bloqueio (`400`), sem persistencia.

### 6.6 Denuncia e moderacao

1. Cliente ou prestador denuncia avaliacao com motivo valido.
2. Admin lista fila em `GET /api/adminreviews/reported`.
3. Admin modera com `KeepVisible`.
4. Repetir ciclo e moderar com `HideComment`.
5. Resultado esperado:
   - status de moderacao atualizado;
   - comentario ocultado no caso `HideComment`;
   - fila de pendentes reduzida apos decisao.

### 6.7 Regras de denuncia invalidas

1. Autor da propria avaliacao tenta denunciar.
2. Usuario nao relacionado tenta denunciar sem ser admin.
3. Denuncia com motivo vazio.
4. Resultado esperado: bloqueio (`400`) em todos os cenarios.

### 6.8 Ranking e outliers no dashboard admin

1. Gerar massa com pelo menos 5 avaliacoes para 1 prestador ruim (media <= 2.5) e 15 para 1 prestador excelente (media >= 4.9).
2. Abrir dashboard admin.
3. Resultado esperado:
   - ranking ordenado por media e volume;
   - outliers classificados conforme regra;
   - clientes tambem aparecem em ranking proprio.

## 7. Evidencias minimas para aceite QA

- print do envio de avaliacao no cliente;
- print do envio de avaliacao no prestador;
- print do perfil publico com reputacao;
- print do dashboard admin com ranking/outliers;
- payload e resposta dos endpoints de denuncia e moderacao;
- evidencia de erro nos cenarios de bloqueio (duplicidade, inelegibilidade e autoria).

## 8. Checklist de saida

- [ ] Avaliacao bilateral validada ponta a ponta.
- [ ] Duplicidade bloqueada por regra.
- [ ] Elegibilidade por pagamento/janela validada.
- [ ] Denuncia e moderacao validadas com trilha.
- [ ] Reputacao refletida em perfis e dashboard admin.
- [ ] Evidencias anexadas no dossie de QA da release.
