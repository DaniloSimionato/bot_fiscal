using Microsoft.Extensions.Options;

namespace FriporaFiscalBot.Configuration;

public sealed class BotOptionsValidator : IValidateOptions<BotOptions>
{
    public ValidateOptionsResult Validate(string? name, BotOptions options)
    {
        var operation = options.ModoOperacao.Trim().ToUpperInvariant();
        if (operation is not "SIMULACAO" and not "APLICACAO")
            return ValidateOptionsResult.Fail("ModoOperacao deve ser SIMULACAO ou APLICACAO.");

        if (operation == "APLICACAO")
        {
            if (!options.PermitirAplicacao || options.Mode.Trim().ToUpperInvariant() != "HOMOLOGACAO" ||
                options.AmbientePermitido != 2 || options.SeriePermitida != 3 ||
                options.PermitirProducao || options.PermitirTransmissaoAutomatica ||
                options.NotaAlvo != 234 || options.ModoSimulacao)
            {
                return ValidateOptionsResult.Fail(
                    "Aplicacao exige nota 234 em homologacao/série 3, sem produção, transmissão automática ou simulação.");
            }
        }

        if (!string.IsNullOrWhiteSpace(options.Firebird.Password) &&
            (!options.ModoSimulacao || options.AmbientePermitido != 2 || options.SeriePermitida != 3))
        {
            return ValidateOptionsResult.Fail(
                "Firebird:Password somente é permitido em simulação, homologação e série 3.");
        }

        return ValidateOptionsResult.Success;
    }
}
