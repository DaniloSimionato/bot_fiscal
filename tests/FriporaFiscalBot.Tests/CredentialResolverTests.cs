using System;
using System.Security.Cryptography;
using System.Text;
using FriporaFiscalBot.Configuration;
using FriporaFiscalBot.Infrastructure;
using Microsoft.Extensions.Options;
using Xunit;

namespace FriporaFiscalBot.Tests;

public sealed class CredentialResolverTests
{
    [Fact]
    public void UsesPlainTextPasswordOnlyInSimulationHomologationSeries3()
    {
        var options = new FirebirdOptions { Password = "senha-de-teste" };

        var password = SecretProtector.ResolvePassword(options, true, 2, 3);

        Assert.Equal("senha-de-teste", password);
    }

    [Fact]
    public void UsesDpapiPasswordWhenPlainTextPasswordIsEmpty()
    {
        const string expected = "senha-protegida-de-teste";
        var protectedBytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(expected),
            null,
            DataProtectionScope.LocalMachine);
        var options = new FirebirdOptions
        {
            PasswordProtectedBase64 = Convert.ToBase64String(protectedBytes)
        };

        var password = SecretProtector.ResolvePassword(options, false, 1, 2);

        Assert.Equal(expected, password);
    }

    [Theory]
    [InlineData(false, 2, 3)]
    [InlineData(true, 1, 3)]
    [InlineData(true, 2, 2)]
    public void RejectsPlainTextPasswordOutsideRestrictedTestMode(
        bool simulation, int ambiente, int serie)
    {
        var options = new FirebirdOptions { Password = "senha-de-teste" };

        Assert.Throws<InvalidOperationException>(() =>
            SecretProtector.ResolvePassword(options, simulation, ambiente, serie));
    }

    [Fact]
    public void ValidatorRejectsPlainTextPasswordOutsideRestrictedTestMode()
    {
        var options = new BotOptions
        {
            ModoSimulacao = false,
            AmbientePermitido = 2,
            SeriePermitida = 3,
            Firebird = new FirebirdOptions { Password = "senha-de-teste" }
        };

        var result = new BotOptionsValidator().Validate(Options.DefaultName, options);

        Assert.False(result.Succeeded);
    }
}
