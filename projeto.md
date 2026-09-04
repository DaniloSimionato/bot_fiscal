# Fripora Fiscal Bot

## Ideia do projeto

O Fripora Fiscal Bot é um serviço Windows local criado para auxiliar o sistema emissor de NF-e da Fripora na preparação de notas fiscais em homologação.

O serviço conecta-se diretamente ao Firebird instalado no computador, identifica notas pendentes que atendem a regras específicas e calcula o ICMS-ST conforme a regra fiscal aprovada pela contabilidade. O projeto não utiliza inteligência artificial, nuvem, APIs externas ou comunicação com servidores remotos.

O objetivo é automatizar uma tarefa repetitiva com regras determinísticas, cálculos reproduzíveis, validações rigorosas, logs e possibilidade de auditoria.

## Escopo atual

A primeira versão é restrita ao ambiente de testes:

- série 3;
- ambiente de homologação (`NFE_TPAMB = 2`);
- notas sem autorização, protocolo ou cancelamento;
- produção bloqueada;
- transmissão automática desativada;
- comunicação exclusivamente local com o Firebird;
- monitor visual por Named Pipe local.

O modo padrão é `SIMULACAO`. Nesse modo o serviço apenas lê os dados, calcula os valores esperados e registra o resultado. Nenhuma alteração é gravada.

Existe também um modo de aplicação controlada, desabilitado por padrão, limitado à nota de teste `NOTA_ID = 234`. Esse modo exige habilitação explícita e valida todos os dados antes de iniciar uma transação.

## Regra fiscal

Para cada item compatível com a operação configurada, o cálculo utiliza valores decimais:

```text
ICMS sobre a base ST = round(VALOR_BASE_ST × PER_ICMS_ST / 100, 2)
Crédito presumido = round(VALOR_ICMS × percentual_credito / 100, 2)
Novo ICMS-ST = ICMS sobre a base ST − crédito presumido
Novo total = total atual − ICMS-ST atual + novo ICMS-ST
```

O arredondamento utiliza duas casas para os valores monetários e quatro casas para `PRECO_FINAL`, reproduzindo o comportamento validado nas notas de homologação.

## Funcionamento

O serviço executa continuamente o seguinte ciclo:

1. inicia com o Windows;
2. valida a configuração de segurança;
3. conecta-se ao Firebird local;
4. procura notas elegíveis;
5. realiza duas leituras para verificar estabilidade;
6. calcula os valores fiscais;
7. registra o resultado em simulação ou aplica a transação controlada;
8. atualiza o heartbeat e o monitor local;
9. aguarda o próximo ciclo.

O serviço não gera XML, não assina documentos e não transmite NF-e. A geração e a autorização continuam sendo responsabilidades do sistema emissor oficial, após a conferência do usuário.

## Componentes

### Serviço Windows

Executa em segundo plano, sem depender de uma janela aberta ou de um usuário conectado. Mantém conexão local com o Firebird, processa as notas elegíveis, grava logs rotativos e publica o status por Named Pipe.

### Monitor local

Aplicativo WinForms que mostra o estado do serviço, conexão com o banco, modo de operação, série, nota alvo, última verificação, última ação, quantidade processada e último erro.

O monitor não abre servidor web nem porta de rede.

### Testes

O projeto possui testes unitários para o cálculo decimal, arredondamento, credenciais, montagem da conexão Firebird, consulta dos itens e validações do modo de aplicação.

## Segurança

O projeto foi desenhado para impedir alterações indevidas:

- simulação é o padrão;
- produção permanece bloqueada;
- aplicação exige múltiplas condições simultâneas;
- somente os campos fiscais previamente autorizados podem ser alterados;
- número, série, chave, ambiente, status, protocolo e dados cadastrais não são modificados;
- a senha preferencialmente utiliza DPAPI;
- senha em texto só é permitida em simulação, homologação e série 3;
- `appsettings.json`, bancos, XMLs, certificados e logs ficam fora do Git;
- não existe transmissão automática para a SEFAZ.

## Configuração

O arquivo `appsettings.example.json` apresenta os valores e nomes esperados. A configuração real deve ser mantida somente no `appsettings.json` local do Windows.

O modo normal é:

```json
"ModoOperacao": "SIMULACAO",
"PermitirAplicacao": false,
"NotaAlvo": null
```

O modo de aplicação controlada deve ser habilitado somente em uma cópia de testes, após backup e conferência dos valores da nota alvo.

## O que o projeto não faz

O Fripora Fiscal Bot não:

- altera a rotina global de tributação do emissor;
- processa notas reais de produção;
- modifica notas autorizadas ou canceladas;
- gera ou assina XML;
- transmite documentos à SEFAZ;
- armazena certificados ou chaves privadas;
- envia informações para a internet, nuvem ou banco remoto.

## Evolução planejada

As próximas etapas devem priorizar:

- validação de integração em cópia do banco Firebird;
- confirmação do comportamento das triggers do emissor;
- testes de rollback e concorrência;
- operação controlada de novas notas de homologação;
- integração documentada com o fluxo oficial de geração do XML;
- revisão contábil e técnica antes de qualquer possibilidade de produção.

Qualquer mudança de escopo deverá preservar o bloqueio de produção e a exigência de confirmação explícita para alterações no banco.
