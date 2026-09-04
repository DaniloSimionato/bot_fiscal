using FirebirdSql.Data.FirebirdClient;
using FriporaFiscalBot.Configuration;
using Microsoft.Extensions.Options;

namespace FriporaFiscalBot.Infrastructure;

public sealed class FirebirdNoteRepository
{
    private readonly BotOptions _options;
    private readonly ILogger<FirebirdNoteRepository> _logger;

    public FirebirdNoteRepository(IOptions<BotOptions> options, ILogger<FirebirdNoteRepository> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    // Fase 1: somente leitura. Nenhum método de escrita existe nesta classe.
    public async Task<IReadOnlyList<PendingNote>> ReadEligibleNotesAsync(CancellationToken cancellationToken)
    {
        var password = SecretProtector.ResolvePassword(
            _options.Firebird,
            _options.ModoSimulacao,
            _options.AmbientePermitido,
            _options.SeriePermitida);
        _logger.LogInformation("Credencial do Firebird carregada.");
        var cs = new FbConnectionStringBuilder
        {
            Database = _options.Firebird.DatabasePath,
            UserID = _options.Firebird.User,
            Password = password,
            Port = _options.Firebird.Port,
            Charset = "UTF8",
            Pooling = false,
            Dialect = 3
        }.ToString();

        await using var connection = new FbConnection(cs);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT NOTA_ID, NUM_NOTA, SERIE, NFE_TPAMB, NFE_CSTAT, NFE_PROT,
                   IDN_CANCELADA, VALOR_ICMS_ST, VALOR_NOTA
            FROM NOTAS_EMITIDAS
            WHERE SERIE = @serie
              AND NFE_TPAMB = @ambiente
              AND NFE_CSTAT IS NULL
              AND NFE_PROT IS NULL
              AND IDN_CANCELADA = 'N'
              AND EXISTS (
                  SELECT 1 FROM NOTAS_EMITIDAS_ITENS I
                  WHERE I.NOTA_ID = NOTAS_EMITIDAS.NOTA_ID
              )
              AND NOT EXISTS (
                  SELECT 1
                  FROM NOTAS_EMITIDAS_ITENS I
                  WHERE I.NOTA_ID = NOTAS_EMITIDAS.NOTA_ID
                    AND (COALESCE(I.COD_NCM, '') <> @ncm OR COALESCE(I.CFOP, '') <> @cfop
                         OR COALESCE(I.COD_ST, '') <> @cst
                         OR COALESCE(I.PER_ICMS_ST, -1) <> @aliquota)
              )
            ORDER BY NOTA_ID
            """;

        var notes = new List<PendingNote>();
        await using var command = new FbCommand(sql, connection);
        command.Parameters.AddWithValue("@serie", _options.SeriePermitida);
        command.Parameters.AddWithValue("@ambiente", _options.AmbientePermitido);
        command.Parameters.AddWithValue("@ncm", _options.Regra.Ncm);
        command.Parameters.AddWithValue("@cfop", _options.Regra.Cfop);
        command.Parameters.AddWithValue("@cst", _options.Regra.Cst);
        command.Parameters.AddWithValue("@aliquota", _options.Regra.AliquotaIcmsSt);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            notes.Add(new PendingNote(
                reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2),
                reader.GetInt32(3), reader.IsDBNull(4) ? null : reader.GetInt32(4),
                reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetString(6),
                reader.GetDecimal(7), reader.GetDecimal(8)));
        }

        return notes;
    }
}

public sealed record PendingNote(
    int NotaId, int Numero, int Serie, int Ambiente, int? Cstat, string? Protocolo,
    string Cancelada, decimal IcmsSt, decimal ValorNota);

public static class SecretProtector
{
    public static string ResolvePassword(
        FirebirdOptions options,
        bool simulation,
        int ambientePermitido,
        int seriePermitida)
    {
        if (!string.IsNullOrWhiteSpace(options.Password))
        {
            if (!simulation || ambientePermitido != 2 || seriePermitida != 3)
            {
                throw new InvalidOperationException(
                    "Senha em texto somente é permitida em simulação, homologação e série 3.");
            }

            return options.Password;
        }

        return Unprotect(options.PasswordProtectedBase64);
    }

    public static string Unprotect(string protectedBase64)
    {
        if (string.IsNullOrWhiteSpace(protectedBase64))
            throw new InvalidOperationException("Senha Firebird protegida não configurada.");

        var encrypted = Convert.FromBase64String(protectedBase64);
        var clear = System.Security.Cryptography.ProtectedData.Unprotect(
            encrypted, null, System.Security.Cryptography.DataProtectionScope.LocalMachine);
        return System.Text.Encoding.UTF8.GetString(clear);
    }
}
