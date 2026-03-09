# ST-005 - Sessao engajada, atribuicao de CTA e contexto de secao da landing

## Como
Time de growth/operacao

## Eu quero
medir melhor o contexto de cada sessao da landing, sabendo origem do interesse, CTA acionado, secao navegada e tempo engajado real

## Para
parar de olhar apenas visitas e cliques brutos, conseguindo separar trafego frio de trafego realmente qualificado.

## Criterios de aceite

1. Cada sessao da landing deve registrar `sessionId`, `visitorId`, secao inicial e secao dominante, com classificacao clara de origem (`Cliente`, `Prestador`, `Neutra`).
2. O browser deve calcular `tempo engajado` usando `visibilitychange`, `pagehide`, heartbeat e envio final por `sendBeacon`, evitando inflar sessoes abandonadas em segundo plano.
3. O funil de CTA deve distinguir pelo menos `visualizacao`, `clique CTA`, `abertura do modal`, `interacao no formulario`, `erro de submit` e `submit com sucesso`.
4. A classificacao de dispositivo e origem do trafego deve ficar disponivel para analise (`desktop/mobile/tablet`, `organico`, `direto`, `utm`, `referer externo`, `deep link /Cliente`, `deep link /Prestador`).
5. Todos os parametros desta captura (intervalo de heartbeat, limites de sessao, amostragem, debounce e chaves de CTA rastreaveis) devem ser persistidos em banco e editaveis no Admin `Configuracoes`.

## Tasks

- [ ] definir extensoes do modelo de sessao e eventos para armazenar `sectionKey`, `entryPoint`, `trafficSource`, `deviceClass` e `engagedTimeSeconds`;
- [ ] mapear no front as secoes relevantes da landing (`hero`, `clientes`, `prestadores`, `sobre`, `testemunhos`, `modal-lead-cliente`, `modal-lead-prestador`);
- [ ] criar nomenclatura padrao dos CTAs e elementos com `elementKey` consistente para analise posterior;
- [ ] registrar evento de `modal_open`, `form_focus`, `form_submit_error` e `form_submit_success` por origem;
- [ ] melhorar o consolidado de `tempo engajado` por sessao com heartbeat + eventos finais de descarte/fechamento;
- [ ] criar parametros runtime em `SystemSettings` para heartbeat, debounce, amostragem e timeout maximo de sessao;
- [ ] expor/ajustar APIs publicas e internas mantendo compatibilidade retroativa com a fase 1;
- [ ] adicionar testes unitarios/integracao para atribuicao de CTA, secao e tempo engajado;
- [ ] atualizar manual tecnico/operacional quando a implementacao acontecer.
