namespace FriporaFiscalBot.Configuration;

public sealed class BotOptions
{
    public string Mode { get; set; } = "HOMOLOGACAO";
    public int SeriePermitida { get; set; } = 3;
    public int AmbientePermitido { get; set; } = 2;
    public bool PermitirProducao { get; set; } = false;
    public bool PermitirTransmissaoAutomatica { get; set; } = false;
    public bool ModoSimulacao { get; set; } = true;
    public int IntervaloVerificacaoSegundos { get; set; } = 60;
    public int LeiturasEstaveisNecessarias { get; set; } = 2;
    public FirebirdOptions Firebird { get; set; } = new();
    public FiscalRuleOptions Regra { get; set; } = new();
}

public sealed class FirebirdOptions
{
    public string DatabasePath { get; set; } = "";
    public string User { get; set; } = "SYSDBA";
    public string PasswordProtectedBase64 { get; set; } = "";
    public int Port { get; set; } = 3050;
}

public sealed class FiscalRuleOptions
{
    public string Nome { get; set; } = "Venda ICMS ST Carne MS";
    public string Ncm { get; set; } = "02012090";
    public string Cfop { get; set; } = "5403";
    public string Cst { get; set; } = "070";
    public decimal AliquotaIcmsSt { get; set; } = 17m;
    public decimal PercentualCredito { get; set; } = 50m;
}
