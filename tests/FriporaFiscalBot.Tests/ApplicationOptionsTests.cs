using FriporaFiscalBot.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace FriporaFiscalBot.Tests;

public sealed class ApplicationOptionsTests
{
    [Fact]
    public void ApplicationIsAllowedOnlyForNote234InHomologationSeries3()
    {
        var options = ValidApplicationOptions();

        var result = new BotOptionsValidator().Validate(Options.DefaultName, options);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("PRODUCAO", 2, 3, false, false, 234)]
    [InlineData("HOMOLOGACAO", 1, 3, false, false, 234)]
    [InlineData("HOMOLOGACAO", 2, 2, false, false, 234)]
    [InlineData("HOMOLOGACAO", 2, 3, true, false, 234)]
    [InlineData("HOMOLOGACAO", 2, 3, false, true, 234)]
    [InlineData("HOMOLOGACAO", 2, 3, false, false, 237)]
    public void ApplicationIsRejectedWhenAnySafetyConditionFails(
        string mode, int ambiente, int serie, bool production, bool automaticTransmission, int note)
    {
        var options = ValidApplicationOptions();
        options.Mode = mode;
        options.AmbientePermitido = ambiente;
        options.SeriePermitida = serie;
        options.PermitirProducao = production;
        options.PermitirTransmissaoAutomatica = automaticTransmission;
        options.NotaAlvo = note;

        var result = new BotOptionsValidator().Validate(Options.DefaultName, options);

        Assert.False(result.Succeeded);
    }

    private static BotOptions ValidApplicationOptions() => new()
    {
        Mode = "HOMOLOGACAO",
        SeriePermitida = 3,
        AmbientePermitido = 2,
        PermitirProducao = false,
        PermitirTransmissaoAutomatica = false,
        ModoSimulacao = false,
        ModoOperacao = "APLICACAO",
        PermitirAplicacao = true,
        NotaAlvo = 234
    };
}
