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
                // Fase 1: simulação. Não há UPDATE, geração de XML ou transmissão.
                _logger.LogInformation(
                    "SIMULAÇÃO: nota {NotaId}, número {Numero}, série {Serie}, ambiente {Ambiente}, ICMS-ST atual {IcmsSt}, valor {ValorNota}.",
                    note.NotaId, note.Numero, note.Serie, note.Ambiente, note.IcmsSt, note.ValorNota);
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
