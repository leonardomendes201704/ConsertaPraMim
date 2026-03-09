# ST-070 - Realtime, health check e atualizacao a cada 5s no Fire TV

## Como
operacao/lideranca

## Eu quero
que o app Fire TV receba pulsos realtime e revalide a saude da plataforma continuamente

## Para
usar a TV como cockpit vivo, com percepcao imediata de indisponibilidade ou mudanca operacional sem precisar forcar refresh manual.

## Criterios de aceite

1. O backend expõe um hub SignalR dedicado para o app Fire TV, protegido por autenticacao admin.
2. Um worker server-side publica pulsos periodicos no hub conforme `FireTvDashboard.SignalRPulseSeconds`, com default seguro de 5 segundos.
3. A API inclui health checks configuraveis para `API`, `Portal Admin`, `Portal Cliente` e `Portal Prestador`, com URL, rotulo e timeout persistidos em banco.
4. A tela operacional do app reflete:
   - estado realtime conectado/desconectado;
   - latencia media;
   - quantidade de alvos saudaveis;
   - lista detalhada de health targets.
5. O app continua com fallback de refresh por timer caso o realtime perca conexao temporariamente.

## Tasks

- [x] criar `FireTvDashboardHub` com auto-join do grupo admin;
- [x] permitir `access_token` no `Program.cs` para `/fireTvDashboardHub`;
- [x] criar `FireTvDashboardPulseWorker` com broadcast a cada 5 segundos por default;
- [x] criar `IFireTvDashboardHealthProbe` + implementacao HTTP para alvos configuraveis;
- [x] expor o resumo de health e latencia no payload operacional;
- [x] conectar o app TV ao hub SignalR e atualizar a view operacional a cada pulso.
