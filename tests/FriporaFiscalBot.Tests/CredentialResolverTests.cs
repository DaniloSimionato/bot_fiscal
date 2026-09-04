using System;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using FirebirdSql.Data.FirebirdClient;
using FriporaFiscalBot.Configuration;
using FriporaFiscalBot.Infrastructure;
using Microsoft.Extensions.Options;
using Xunit;

namespace FriporaFiscalBot.Tests;

public sealed class CredentialResolverTests
{
    [Fact]
    public void ItemQueryUsesQtdAndMapsItToQuantidade()
    {
        Assert.Contains("QTD AS QUANTIDADE", FirebirdNoteRepository.ItemsSql);

        var table = new DataTable();
        table.Columns.Add("ITEM", typeof(int));
        table.Columns.Add("VALOR", typeof(decimal));
        table.Columns.Add("TOTAL", typeof(decimal));
        table.Columns.Add("QUANTIDADE", typeof(decimal));
        table.Columns.Add("VALOR_BASE_ST", typeof(decimal));
        table.Columns.Add("PER_ICMS_ST", typeof(decimal));
        table.Columns.Add("VALOR_ICMS", typeof(decimal));
        table.Columns.Add("VALOR_ICMS_ST", typeof(decimal));
        table.Rows.Add(1, 100m, 120m, 2m, 200m, 17m, 20m, 10m);

        using var reader = table.CreateDataReader();
        Assert.True(reader.Read());
        var item = FirebirdNoteRepository.MapItem(reader);

        Assert.Equal(1, item.Item);
        Assert.Equal(2m, item.Quantidade);
        Assert.Equal(200m, item.ValorBaseSt);
        Assert.Equal(17m, item.PerIcmsSt);
        Assert.Equal(20m, item.ValorIcms);
        Assert.Equal(10m, item.ValorIcmsStAtual);
    }

    [Fact]
    public void BuildsLocalFirebirdConnectionWithSeparateHostAndDatabase()
    {
        var options = new FirebirdOptions
        {
            Host = "localhost",
            DatabasePath = @"C:\FriporaFiscalBot\BancoTeste\FRIPORA_TESTE.FDB",
            User = "SYSDBA",
            Port = 3050
        };

        var connectionString = FirebirdNoteRepository.BuildConnectionString(options, "senha-de-teste");
        var parsed = new FbConnectionStringBuilder(connectionString);

        Assert.Equal("localhost", parsed.DataSource);
        Assert.Equal(options.DatabasePath, parsed.Database);
        Assert.Equal(options.Port, parsed.Port);
        Assert.Equal(options.User, parsed.UserID);
        Assert.Equal("senha-de-teste", parsed.Password);
    }

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
