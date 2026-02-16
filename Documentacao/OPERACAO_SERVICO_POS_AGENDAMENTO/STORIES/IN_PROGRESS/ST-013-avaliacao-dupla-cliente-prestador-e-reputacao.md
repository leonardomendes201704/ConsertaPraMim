# ST-013 - Avaliacao dupla (cliente/prestador) e reputacao

Status: In Progress  
Epic: EPIC-005

## Objetivo

Coletar feedback bilateral apos conclusao para elevar qualidade da comunidade e apoiar mecanismos de confianca no matching.

## Criterios de aceite

- Cliente avalia prestador com nota e comentario opcional.
- Prestador avalia cliente com nota e comentario opcional.
- Avaliacao so e permitida para pedido concluido e pago.
- Sistema impede avaliacao duplicada por parte para o mesmo pedido.
- Dashboard exibe medias e distribuicao de notas.
- Comentarios com abuso podem ser sinalizados e moderados por admin.

## Tasks

- [x] Evoluir modelo de review para suportar avaliacao bilateral.
- [x] Criar endpoints separados para avaliacao de cliente e prestador.
- [x] Implementar regras de elegibilidade e janela de avaliacao.
- [x] Exibir modal/CTA de avaliacao apos conclusao do servico.
- [ ] Implementar calculo de score medio e contagem de notas.
- [ ] Criar mecanismo de denuncia/moderacao de comentario.
- [ ] Exibir reputacao no perfil publico do prestador e cliente.
- [ ] Criar testes para evitar duplicidade e fraude basica.
- [ ] Atualizar dashboard admin com ranking e outliers.
- [ ] Atualizar manual QA para cenarios de avaliacao bilateral.

