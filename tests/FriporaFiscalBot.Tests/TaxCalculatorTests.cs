using FriporaFiscalBot.Domain;

namespace FriporaFiscalBot.Tests;

public sealed class TaxCalculatorTests
{
    [Fact]
    public void Nota5_Item1_Produces3994()
    {
        var result = TaxCalculator.Calculate(
            new TaxItemInput(1, 1161m, 1200.94m, 43m, 371.51m, 17m, 46.44m, 39.94m), 50m);
        Assert.Equal(39.94m, result.ValorIcmsStNovo);
    }

    [Theory]
    [InlineData(213.44, 17, 26.68, 22.94)]
    [InlineData(86.40, 17, 10.80, 9.29)]
    [InlineData(207.36, 17, 25.92, 22.29)]
    public void ReferenceValuesUseTwoDecimalRounding(
        decimal baseSt, decimal aliquota, decimal icms, decimal expected)
    {
        var result = TaxCalculator.Calculate(
            new TaxItemInput(1, 1m, 100m, 1m, baseSt, aliquota, icms, 0m), 50m);
        Assert.Equal(expected, result.ValorIcmsStNovo);
    }

    [Fact]
    public void RejectsZeroQuantity()
    {
        Assert.Throws<InvalidOperationException>(() => TaxCalculator.Calculate(
            new TaxItemInput(1, 1m, 1m, 0m, 1m, 17m, 1m, 0m), 50m));
    }
}
