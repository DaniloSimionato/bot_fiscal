# Modo de aplicação controlada

O padrão permanece `SIMULACAO`, com `PermitirAplicacao=false` e sem escrita no Firebird. Nesse modo o serviço apenas lê, calcula e registra o que faria.

## Habilitar somente a nota 234

No `appsettings.json` local, que não deve ser versionado, configure:

```json
{
  "FriporaFiscalBot": {
    "Mode": "HOMOLOGACAO",
    "SeriePermitida": 3,
    "AmbientePermitido": 2,
    "PermitirProducao": false,
    "PermitirTransmissaoAutomatica": false,
    "ModoSimulacao": false,
    "ModoOperacao": "APLICACAO",
    "PermitirAplicacao": true,
    "NotaAlvo": 234
  }
}
```

A credencial deve continuar protegida via DPAPI no modo de aplicação. Senha em texto é rejeitada quando `ModoSimulacao=false`. O serviço relê a nota 234 série 3 dentro de uma transação, exige homologação, ausência de autorização/cancelamento/protocolo, dois itens e os valores originais aprovados. Qualquer divergência gera `ROLLBACK`.

Após `COMMIT`, a execução é marcada como concluída e não reaplica a nota em ciclos seguintes. Se os valores finais já estiverem corretos, registra `NOTA JÁ CORRIGIDA` sem novo `UPDATE`.

## Conferência e retorno à simulação

Confira no log os valores antigos, novos, itens alterados e `COMMIT`. Para desabilitar a escrita, pare o serviço e volte a configurar:

```json
"ModoSimulacao": true,
"ModoOperacao": "SIMULACAO",
"PermitirAplicacao": false,
"NotaAlvo": null
```

Não há geração de XML, assinatura, transmissão ou alteração de `NFE_ACAO` nesta fase.

## Comandos Windows

```powershell
git pull origin main
dotnet restore .\FriporaFiscalBot.sln
dotnet build .\FriporaFiscalBot.sln -c Release
dotnet test .\FriporaFiscalBot.sln -c Release
dotnet publish .\src\FriporaFiscalBot.Service\FriporaFiscalBot.Service.csproj -c Release -r win-x64 --self-contained true -o .\publish\Service
```

Faça backup da cópia do banco antes de habilitar a aplicação e mantenha a transmissão automática desativada.
