# Riscos e pendências

- O executável DFe `1.2.671.1478` não está disponível neste Mac.
- A rotina do emissor que grava os impostos ainda precisa ser confirmada no Windows.
- As triggers `NOTAS_EMITIDAS_ITENS_BINS/BUPD` recalculam `TOTAL` e `PRECO_FINAL`; a fase de aplicação deverá validar esse efeito.
- A senha deverá ser provisionada com DPAPI no Windows; não usar texto puro.
- A primeira versão não gera XML nem transmite.
- Antes de ativar aplicação, validar a fórmula e o arredondamento diretamente contra uma cópia Firebird no Windows.
- O serviço deverá ser instalado inicialmente apontando somente para a cópia de testes.
