using Microsoft.Extensions.Options;

namespace FriporaFiscalBot.Configuration;

public sealed class BotOptionsValidator : IValidateOptions<BotOptions>
{
    public ValidateOptionsResult Validate(string? name, BotOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Firebird.Password) &&
            (!options.ModoSimulacao || options.AmbientePermitido != 2 || options.SeriePermitida != 3))
        {
            return ValidateOptionsResult.Fail(
                "Firebird:Password somente é permitido em simulação, homologação e série 3.");
        }

        return ValidateOptionsResult.Success;
    }
}
