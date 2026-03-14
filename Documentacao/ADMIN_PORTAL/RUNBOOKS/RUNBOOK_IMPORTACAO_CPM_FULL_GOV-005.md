# Runbook GOV-005 - Importacao do projeto CPM Full para a solution

## Objetivo

Registrar como o projeto legado `cpm-full` foi incorporado a solution `ConsertaPraMim` para analise e migracao gradual.

## Caminhos envolvidos

- Projeto importado: `Backend/src/ConsertaPraMim.Web.CpmFull`
- Solution principal: `Backend/ConsertaPraMim.sln`
- Solution de desenvolvimento: `Backend/src/src.sln`

## Decisoes da importacao

- O projeto foi importado como app standalone, sem mesclar controllers, views ou services com os portais atuais.
- O nome de pasta/projeto na solution ficou `ConsertaPraMim.Web.CpmFull`.
- O namespace legado `AppMobileCPM` foi preservado temporariamente para evitar refactor massivo no mesmo ciclo.
- A dependencia de CDN para `bootstrap-icons` e `SortableJS` foi substituida por assets locais versionados.
- O projeto agora aceita override local via `appsettings.Local.json`, carregado em runtime e ignorado pelo Git.

## Connection string temporaria local

Se for necessario usar a connection string temporaria do projeto original:

1. Criar ou manter `Backend/src/ConsertaPraMim.Web.CpmFull/appsettings.Local.json`.
2. Preencher `ConnectionStrings:DefaultConnection` nesse arquivo local.
3. Nao adicionar esse arquivo ao Git.

Exemplo minimo:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=...;User Id=...;Password=...;"
  }
}
```

## Validacao local

1. Restaurar/buildar o projeto importado:

```powershell
dotnet build C:\Leonardo\Labs\ConsertaPraMimWeb\Backend\src\ConsertaPraMim.Web.CpmFull\ConsertaPraMim.Web.CpmFull.csproj
```

2. Confirmar presenca do projeto nas duas solutions:

```powershell
dotnet sln C:\Leonardo\Labs\ConsertaPraMimWeb\Backend\ConsertaPraMim.sln list
dotnet sln C:\Leonardo\Labs\ConsertaPraMimWeb\Backend\src\src.sln list
```

## Pontos de atencao

- O projeto continua funcionalmente isolado; nao ha compartilhamento de dominio/aplicacao com `ConsertaPraMim.API` ou com os portais atuais.
- Chamadas externas de CEP/geolocalizacao permanecem no codigo legado e devem ser revisitadas quando houver migracao funcional.
- Antes de promover qualquer tela/fluxo desse projeto para os portais oficiais, revisar CSP, autenticacao, persistencia e padroes visuais do ecossistema atual.
