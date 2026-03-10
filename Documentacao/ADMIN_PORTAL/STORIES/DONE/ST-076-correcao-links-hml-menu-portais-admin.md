# ST-076 - Correcao dos links HML de Portal Cliente/Prestador no menu Admin

## Como
operador(a) do portal admin em homologacao

## Eu quero
que os atalhos `Portal Cliente` e `Portal Prestador` abram os subdominios HML corretos

## Para
evitar redirecionamento para hosts invalidos (`cliente.admin.*` / `prestador.admin.*`) durante testes de homologacao.

## Criterios de aceite

1. Acessando o admin por `https://hml.admin.consertapramim.com`, o atalho `Portal Cliente` deve abrir `https://hml.cliente.consertapramim.com/`.
2. Acessando o admin por `https://hml.admin.consertapramim.com`, o atalho `Portal Prestador` deve abrir `https://hml.prestador.consertapramim.com/`.
3. Em producao, os atalhos continuam abrindo `https://cliente.consertapramim.com/` e `https://prestador.consertapramim.com/`.
4. Deve existir teste unitario de regressao cobrindo inferencia HML no resolvedor de URL publica do Admin.
5. Manual QA/Operacao do Admin deve registrar o comportamento esperado para HML.

## Tasks

- [x] ajustar `AdminPublicUrlResolver` para reconhecer prefixo de ambiente (`hml/dev/qa/stg`) e montar host irmao corretamente;
- [x] adicionar teste unitario para `hml.admin.consertapramim.com` no `AdminPublicUrlResolverTests`;
- [x] atualizar manual `AdminManual` com regra explicita de links por ambiente;
- [x] registrar mudanca no changelog em `Released`.

