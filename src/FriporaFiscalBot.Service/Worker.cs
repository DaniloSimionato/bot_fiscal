using FriporaFiscalBot.Configuration;
using FriporaFiscalBot.Infrastructure;
using Microsoft.Extensions.Options;

namespace FriporaFiscalBot;

public sealed class Worker : BackgroundService
{
    private readonly BotOptions _options;
    private readonly FirebirdNoteRepository _repository;
    private readonly HeartbeatState _heartbeat;
    private readonly LocalStatusPipe _statusPipe;
    private readonly ILogger<Worker> _logger;

    public Worker(
        IOptions<BotOptions> options,
        FirebirdNoteRepository repository,
        HeartbeatState heartbeat,
        LocalStatusPipe statusPipe,
        ILogger<Worker> logger)
    {
        _options = options.Value;
        _repository = repository;
        _heartbeat = heartbeat;
        _statusPipe = statusPipe;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Fripora Fiscal Bot iniciado em modo {Mode}, série {Serie}.",
            _options.Mode, _options.SeriePermitida);

        _ = Task.Run(() => _statusPipe.ServeAsync(stoppingToken), stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.IntervaloVerificacaoSegundos));
        do
        {
            await CheckOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task CheckOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var firstRead = await _repository.ReadEligibleNotesAsync(cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            var secondRead = await _repository.ReadEligibleNotesAsync(cancellationToken);
            var notes = firstRead.SequenceEqual(secondRead) ? secondRead : Array.Empty<PendingNote>();

            if (!firstRead.SequenceEqual(secondRead))
                _logger.LogWarning("SIMULAÇÃO: dados instáveis; nenhum registro será processado neste ciclo.");

            foreach (var note in notes)
            {
                var items = await _repository.ReadItemsAsync(note.NotaId, cancellationToken);
                var results = new List<FriporaFiscalBot.Domain.TaxItemResult>(items.Count);

                _logger.LogInformation(
                    "SIMULAÇÃO — NENHUMA ALTERAÇÃO FOI GRAVADA. Nota {NotaId}, número {Numero}, série {Serie}, quantidade de itens {QuantidadeItens}.",
                    note.NotaId, note.Numero, note.Serie, items.Count);

                foreach (var item in items)
                {
                    var result = FriporaFiscalBot.Domain.TaxCalculator.Calculate(
                        new FriporaFiscalBot.Domain.TaxItemInput(
                            item.Item,
                            item.Valor,
                            item.TotalAtual,
                            item.Quantidade,
                            item.ValorBaseSt,
                            item.PerIcmsSt,
                            item.ValorIcms,
                            item.ValorIcmsStAtual),
                        _options.Regra.PercentualCredito);
                    results.Add(result);

                    _logger.LogInformation(
                        "SIMULAÇÃO — Nota {NotaId}, item {Item}: ICMS-ST atual {IcmsStAtual}; ICMS próprio {IcmsProprio}; base ST {BaseSt}; alíquota {Aliquota}%; crédito presumido {Credito}; novo ICMS-ST {NovoIcmsSt}; total atual {TotalAtual}; novo total {NovoTotal}.",
                        note.NotaId,
                        item.Item,
                        item.ValorIcmsStAtual,
                        item.ValorIcms,
                        item.ValorBaseSt,
                        item.PerIcmsSt,
                        result.CreditoPresumido,
                        result.ValorIcmsStNovo,
                        item.TotalAtual,
                        result.TotalNovo);
                }

                _logger.LogInformation(
                    "SIMULAÇÃO — Nota {NotaId}: soma final ICMS-ST {IcmsStNovo}; novo valor total da nota {ValorNotaNovo}. NENHUMA ALTERAÇÃO FOI GRAVADA.",
                    note.NotaId,
                    results.Sum(result => result.ValorIcmsStNovo),
                    results.Sum(result => result.TotalNovo));
            }

            _heartbeat.MarkCheck(true, notes.Count > 0 ? string.Join(",", notes.Select(n => n.NotaId)) : "");
        }
        catch (Exception ex)
        {
            _heartbeat.MarkCheck(false, lastError: ex.Message);
            _logger.LogError(ex, "Falha na verificação do Firebird.");
        }
    }
}
