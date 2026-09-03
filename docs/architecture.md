# Arquitetura da primeira fase

## Componentes

### Serviço Windows

Worker Service .NET 8 instalado como serviço nativo. Executa sem usuário logado, reinicia após falha e consulta o Firebird em intervalos configuráveis.

### Monitor local

O serviço publica um snapshot por Named Pipe `FriporaFiscalBot.Status`. Um monitor WinForms/WPF será conectado a esse pipe na próxima etapa; não há servidor web nem porta TCP.

## Elegibilidade

Uma nota só é elegível quando todas as condições são verdadeiras:

```text
SERIE = 3
NFE_TPAMB = 2
NFE_CSTAT IS NULL
NFE_PROT IS NULL
IDN_CANCELADA = 'N'
```

Também será exigida a regra fiscal configurada: NCM `02012090`, CFOP `5403`, CST `070` e alíquota ST de `17%`.

## Cálculo

O cálculo usa `decimal` e arredonda a duas casas cada componente monetário, usando arredondamento de meio valor afastando-se de zero para acompanhar `NUMERIC/DECIMAL` do Firebird:

```text
ICMS sobre base = round(VALOR_BASE_ST × PER_ICMS_ST / 100, 2)
crédito = round(VALOR_ICMS × 50 / 100, 2)
novo ICMS-ST = round(ICMS sobre base − crédito, 2)
novo total = round(total atual − ICMS-ST atual + novo ICMS-ST, 2)
```

Esse arredondamento deverá ser confirmado contra o comportamento do Firebird e as notas 5, 6 e 7 no Windows antes da ativação do modo de aplicação.

## Transação e idempotência da próxima fase

A fase de aplicação deverá reconsultar a nota dentro da transação, conferir os valores originais, bloquear a nota por chave lógica e atualizar itens/cabeçalho. Uma segunda execução deverá reconhecer os valores já corrigidos e ignorar a nota.

Esta fase não contém `UPDATE`, `DELETE`, `INSERT`, `COMMIT` ou transmissão.
