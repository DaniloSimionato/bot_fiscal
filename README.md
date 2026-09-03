# Fripora Fiscal Bot

Primeira fase do serviço Windows local para o banco Firebird.

## Estado desta fase

- somente leitura;
- modo simulação;
- homologação (`NFE_TPAMB = 2`);
- série permitida `3`;
- produção bloqueada;
- sem geração de XML;
- sem assinatura;
- sem transmissão;
- sem métodos de escrita no repositório Firebird.

## Estrutura

- `src/FriporaFiscalBot.Service`: Worker Service Windows, heartbeat e Named Pipe local;
- `src/FriporaFiscalBot.Monitor`: monitor WinForms local, sem servidor e sem porta de rede;
- `tests/FriporaFiscalBot.Tests`: testes do cálculo decimal;
- `scripts`: instalação e remoção do serviço;
- `docs`: arquitetura, operação e riscos.

## Execução no Windows

1. Publicar o projeto para `win-x64`.
2. Configurar `DatabasePath`.
3. Proteger a senha com DPAPI; nunca preencher senha em texto puro.
4. Manter `ModoSimulacao=true`.
5. Instalar com privilégios administrativos usando `scripts/install-service.ps1`.

O `appsettings.json` da primeira fase não possui senha utilizável por padrão. A configuração segura da credencial será feita por uma ferramenta administrativa separada, no Windows.
