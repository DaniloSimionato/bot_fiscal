namespace FriporaFiscalBot.Domain;

public sealed record TaxItemInput(
    int Item,
    decimal Valor,
    decimal TotalAtual,
    decimal Quantidade,
    decimal ValorBaseSt,
    decimal PerIcmsSt,
    decimal ValorIcms,
    decimal ValorIcmsStAtual);

public sealed record TaxItemResult(
    int Item,
    decimal ValorIcmsStNovo,
    decimal TotalNovo,
    decimal PrecoFinalNovo,
    decimal CreditoPresumido);

public static class TaxCalculator
{
    public static TaxItemResult Calculate(TaxItemInput input, decimal percentualCredito)
    {
        if (input.Quantidade <= 0)
            throw new InvalidOperationException($"Item {input.Item}: quantidade deve ser maior que zero.");
        if (input.ValorBaseSt < 0 || input.ValorIcms < 0 || input.ValorIcmsStAtual < 0)
            throw new InvalidOperationException($"Item {input.Item}: base ou imposto inválido.");

        var icmsSobreBase = Round2(input.ValorBaseSt * input.PerIcmsSt / 100m);
        var credito = Round2(input.ValorIcms * percentualCredito / 100m);
        var novoSt = Round2(icmsSobreBase - credito);

        if (novoSt < 0)
            throw new InvalidOperationException($"Item {input.Item}: ICMS-ST calculado negativo.");

        // Preserva todos os demais componentes já calculados pelo emissor.
        var novoTotal = Round2(input.TotalAtual - input.ValorIcmsStAtual + novoSt);
        var novoPreco = Round4(novoTotal / input.Quantidade);

        return new TaxItemResult(input.Item, novoSt, novoTotal, novoPreco, credito);
    }

    // Firebird NUMERIC/DECIMAL casts round monetary half-values away from zero.
    private static decimal Round2(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private static decimal Round4(decimal value) => decimal.Round(value, 4, MidpointRounding.AwayFromZero);
}
