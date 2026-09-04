using FirebirdSql.Data.FirebirdClient;
using FriporaFiscalBot.Configuration;
using FriporaFiscalBot.Domain;
using Microsoft.Extensions.Options;

namespace FriporaFiscalBot.Infrastructure;

public sealed class FirebirdNoteRepository
{
    private static readonly SemaphoreSlim ApplicationLock = new(1, 1);

    private const int ApplicationNoteId = 234;
    private const int ApplicationSeries = 3;
    private const decimal OriginalHeaderIcmsSt = 140.540m;
    private const decimal OriginalHeaderValue = 9900.540m;
    private const decimal CorrectedHeaderIcmsSt = 335.730m;
    private const decimal CorrectedHeaderValue = 10095.730m;

    public const string ItemsSql = """
        SELECT ITEM, VALOR, TOTAL, QTD AS QUANTIDADE, VALOR_BASE_ST,
               PER_ICMS_ST, VALOR_ICMS, VALOR_ICMS_ST
        FROM NOTAS_EMITIDAS_ITENS
        WHERE NOTA_ID = @notaId
        ORDER BY ITEM
        """;

    private readonly BotOptions _options;
    private readonly ILogger<FirebirdNoteRepository> _logger;

    public FirebirdNoteRepository(IOptions<BotOptions> options, ILogger<FirebirdNoteRepository> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    // O caminho de simulação é somente leitura; a aplicação é um caminho separado e explicitamente protegido.
    public async Task<IReadOnlyList<PendingNote>> ReadEligibleNotesAsync(CancellationToken cancellationToken)
    {
        var password = SecretProtector.ResolvePassword(
            _options.Firebird,
            _options.ModoSimulacao,
            _options.AmbientePermitido,
            _options.SeriePermitida);
        _logger.LogInformation("Credencial do Firebird carregada.");
        var cs = BuildConnectionString(_options.Firebird, password);

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

    public async Task<IReadOnlyList<PendingItem>> ReadItemsAsync(
        int notaId, CancellationToken cancellationToken)
    {
        var password = SecretProtector.ResolvePassword(
            _options.Firebird,
            _options.ModoSimulacao,
            _options.AmbientePermitido,
            _options.SeriePermitida);
        var cs = BuildConnectionString(_options.Firebird, password);

        await using var connection = new FbConnection(cs);
        await connection.OpenAsync(cancellationToken);

        var items = new List<PendingItem>();
        await using var command = new FbCommand(ItemsSql, connection);
        command.Parameters.AddWithValue("@notaId", notaId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            items.Add(MapItem(reader));

        return items;
    }

    public async Task<ApplyNoteResult> ApplyNote234Async(CancellationToken cancellationToken)
    {
        if (!new BotOptionsValidator().Validate(Options.DefaultName, _options).Succeeded)
            throw new InvalidOperationException("Configuração insegura: aplicação da nota 234 não autorizada.");

        await ApplicationLock.WaitAsync(cancellationToken);
        try
        {
            var password = SecretProtector.ResolvePassword(
                _options.Firebird,
                _options.ModoSimulacao,
                _options.AmbientePermitido,
                _options.SeriePermitida);
            var cs = BuildConnectionString(_options.Firebird, password);

            await using var connection = new FbConnection(cs);
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                var note = await ReadApplicationNoteAsync(connection, transaction, cancellationToken);
                var items = await ReadApplicationItemsAsync(connection, transaction, cancellationToken);

                ValidateApplicationIdentity(note, items);

                var calculated = items.Select(item => TaxCalculator.Calculate(
                    new TaxItemInput(item.Item, item.Valor, item.TotalAtual, item.Quantidade,
                        item.ValorBaseSt, item.PerIcmsSt, item.ValorIcms, item.ValorIcmsStAtual),
                    _options.Regra.PercentualCredito)).ToArray();

                if (IsAlreadyCorrected(note, items, calculated))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ApplyNoteResult.AlreadyCorrectedResult(items.Count, note.IcmsSt, note.ValorNota);
                }

                ValidateOriginalValues(note, items, calculated);
                _logger.LogInformation(
                    "APLICAÇÃO: validação prévia aprovada para nota {NotaId}; ICMS-ST antigo {OldIcmsSt}, valor antigo {OldValue}, itens {ItemCount}. Nenhum UPDATE executado antes desta validação.",
                    note.NotaId, note.IcmsSt, note.ValorNota, items.Count);

                foreach (var result in calculated)
                {
                    await using var itemCommand = new FbCommand("""
                        UPDATE NOTAS_EMITIDAS_ITENS
                        SET VALOR_ICMS_ST = @icmsSt,
                            TOTAL = @total,
                            PRECO_FINAL = @precoFinal
                        WHERE NOTA_ID = @notaId
                          AND ITEM = @item
                        """, connection, transaction);
                    itemCommand.Parameters.AddWithValue("@icmsSt", result.ValorIcmsStNovo);
                    itemCommand.Parameters.AddWithValue("@total", result.TotalNovo);
                    itemCommand.Parameters.AddWithValue("@precoFinal", result.PrecoFinalNovo);
                    itemCommand.Parameters.AddWithValue("@notaId", ApplicationNoteId);
                    itemCommand.Parameters.AddWithValue("@item", result.Item);
                    if (await itemCommand.ExecuteNonQueryAsync(cancellationToken) != 1)
                        throw new InvalidOperationException($"Item {result.Item} não foi atualizado exatamente uma vez.");
                }

                var totalIcmsSt = calculated.Sum(result => result.ValorIcmsStNovo);
                var totalNota = calculated.Sum(result => result.TotalNovo);
                if (totalIcmsSt != CorrectedHeaderIcmsSt || totalNota != CorrectedHeaderValue)
                    throw new InvalidOperationException("Totais calculados não correspondem aos valores aprovados da nota 234.");

                await using (var headerCommand = new FbCommand("""
                    UPDATE NOTAS_EMITIDAS
                    SET VALOR_ICMS_ST = @icmsSt,
                        VALOR_NOTA = @valorNota
                    WHERE NOTA_ID = @notaId
                      AND SERIE = @serie
                    """, connection, transaction))
                {
                    headerCommand.Parameters.AddWithValue("@icmsSt", totalIcmsSt);
                    headerCommand.Parameters.AddWithValue("@valorNota", totalNota);
                    headerCommand.Parameters.AddWithValue("@notaId", ApplicationNoteId);
                    headerCommand.Parameters.AddWithValue("@serie", ApplicationSeries);
                    if (await headerCommand.ExecuteNonQueryAsync(cancellationToken) != 1)
                        throw new InvalidOperationException("Cabeçalho não foi atualizado exatamente uma vez.");
                }

                var after = await ReadApplicationNoteAsync(connection, transaction, cancellationToken);
                var afterItems = await ReadApplicationItemsAsync(connection, transaction, cancellationToken);
                ValidateFinalValues(after, afterItems, calculated);
                await transaction.CommitAsync(cancellationToken);
                return ApplyNoteResult.Success(items.Count, note.IcmsSt, note.ValorNota,
                    totalIcmsSt, totalNota);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _logger.LogError(ex, "APLICAÇÃO: ROLLBACK da nota {NotaId}. Motivo: {Motivo}",
                    ApplicationNoteId, ex.Message);
                return ApplyNoteResult.RolledBack(ex.Message);
            }
        }
        finally
        {
            ApplicationLock.Release();
        }
    }

    private async Task<ApplicationNote> ReadApplicationNoteAsync(
        FbConnection connection, FbTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = new FbCommand("""
            SELECT NOTA_ID, NUM_NOTA, SERIE, NFE_TPAMB, NFE_CSTAT, NFE_PROT,
                   IDN_CANCELADA, VALOR_ICMS_ST, VALOR_NOTA
            FROM NOTAS_EMITIDAS
            WHERE NOTA_ID = @notaId
              AND SERIE = @serie
            """, connection, transaction);
        command.Parameters.AddWithValue("@notaId", ApplicationNoteId);
        command.Parameters.AddWithValue("@serie", ApplicationSeries);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("Nota 234 série 3 não encontrada.");

        return new ApplicationNote(
            reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3),
            reader.IsDBNull(4) ? null : reader.GetInt32(4),
            reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetString(6),
            reader.GetDecimal(7), reader.GetDecimal(8));
    }

    private static async Task<IReadOnlyList<PendingItem>> ReadApplicationItemsAsync(
        FbConnection connection, FbTransaction transaction, CancellationToken cancellationToken)
    {
        var items = new List<PendingItem>();
        await using var command = new FbCommand(ItemsSql, connection, transaction);
        command.Parameters.AddWithValue("@notaId", ApplicationNoteId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            items.Add(MapItem(reader));
        return items;
    }

    private static void ValidateApplicationIdentity(ApplicationNote note, IReadOnlyList<PendingItem> items)
    {
        if (note.NotaId != ApplicationNoteId || note.Serie != ApplicationSeries || note.Ambiente != 2 ||
            note.Cstat is not null || note.Protocolo is not null || note.Cancelada != "N" || items.Count != 2)
        {
            throw new InvalidOperationException("Nota 234 não está elegível: identidade, ambiente, status, protocolo, cancelamento ou quantidade inválida.");
        }
    }

    private static bool IsAlreadyCorrected(
        ApplicationNote note, IReadOnlyList<PendingItem> items, IReadOnlyList<TaxItemResult> calculated)
    {
        return note.IcmsSt == CorrectedHeaderIcmsSt && note.ValorNota == CorrectedHeaderValue &&
            items.Count == 2 && items.Zip(calculated).All(pair =>
                pair.First.ValorIcmsStAtual == pair.Second.ValorIcmsStNovo &&
                pair.First.TotalAtual == pair.Second.TotalNovo);
    }

    private static void ValidateOriginalValues(
        ApplicationNote note, IReadOnlyList<PendingItem> items, IReadOnlyList<TaxItemResult> calculated)
    {
        if (note.IcmsSt != OriginalHeaderIcmsSt || note.ValorNota != OriginalHeaderValue)
            throw new InvalidOperationException("Valores originais do cabeçalho não correspondem aos esperados.");

        var expected = new Dictionary<int, (decimal Icms, decimal BaseSt, decimal St, decimal Total)>
        {
            [1] = (135.760m, 1086.070m, 48.870m, 3442.91m),
            [2] = (254.630m, 2037.070m, 91.670m, 6457.63m)
        };
        foreach (var item in items)
        {
            if (!expected.TryGetValue(item.Item, out var value) ||
                item.ValorIcms != value.Icms || item.ValorBaseSt != value.BaseSt ||
                item.ValorIcmsStAtual != value.St || item.TotalAtual != value.Total)
            {
                throw new InvalidOperationException($"Valores originais do item {item.Item} não correspondem aos esperados.");
            }
        }

        if (calculated.Count != 2 || calculated.Sum(x => x.ValorIcmsStNovo) != CorrectedHeaderIcmsSt ||
            calculated.Sum(x => x.TotalNovo) != CorrectedHeaderValue)
            throw new InvalidOperationException("Cálculo da nota 234 não corresponde aos valores aprovados.");
    }

    private static void ValidateFinalValues(
        ApplicationNote note, IReadOnlyList<PendingItem> items, IReadOnlyList<TaxItemResult> calculated)
    {
        if (note.IcmsSt != CorrectedHeaderIcmsSt || note.ValorNota != CorrectedHeaderValue)
            throw new InvalidOperationException("Conferência final do cabeçalho falhou.");
        if (items.Count != calculated.Count || items.Zip(calculated).Any(pair =>
                pair.First.ValorIcmsStAtual != pair.Second.ValorIcmsStNovo ||
                pair.First.TotalAtual != pair.Second.TotalNovo))
            throw new InvalidOperationException("Conferência final dos itens falhou.");
    }

    public static PendingItem MapItem(System.Data.IDataRecord reader)
    {
        return new PendingItem(
            reader.GetInt32(0),
            reader.GetDecimal(1),
            reader.GetDecimal(2),
            reader.GetDecimal(3),
            reader.GetDecimal(4),
            reader.GetDecimal(5),
            reader.GetDecimal(6),
            reader.GetDecimal(7));
    }

    public static string BuildConnectionString(FirebirdOptions options, string password)
    {
        return new FbConnectionStringBuilder
        {
            DataSource = string.IsNullOrWhiteSpace(options.Host) ? "localhost" : options.Host,
            Database = options.DatabasePath,
            UserID = options.User,
            Password = password,
            Port = options.Port,
            Charset = "UTF8",
            Pooling = false,
            Dialect = 3
        }.ToString();
    }
}

public sealed record PendingNote(
    int NotaId, int Numero, int Serie, int Ambiente, int? Cstat, string? Protocolo,
    string Cancelada, decimal IcmsSt, decimal ValorNota);

public sealed record PendingItem(
    int Item, decimal Valor, decimal TotalAtual, decimal Quantidade,
    decimal ValorBaseSt, decimal PerIcmsSt, decimal ValorIcms, decimal ValorIcmsStAtual);

public sealed record ApplicationNote(
    int NotaId, int Numero, int Serie, int Ambiente, int? Cstat, string? Protocolo,
    string Cancelada, decimal IcmsSt, decimal ValorNota);

public sealed record ApplyNoteResult(
    bool Committed, bool AlreadyCorrected, string Message, int ItemsChanged,
    decimal PreviousIcmsSt, decimal PreviousValue, decimal NewIcmsSt, decimal NewValue)
{
    public static ApplyNoteResult Success(int items, decimal oldSt, decimal oldValue, decimal newSt, decimal newValue) =>
        new(true, false, "COMMIT", items, oldSt, oldValue, newSt, newValue);
    public static ApplyNoteResult AlreadyCorrectedResult(int items, decimal st, decimal value) =>
        new(false, true, "NOTA JÁ CORRIGIDA", items, st, value, st, value);
    public static ApplyNoteResult RolledBack(string reason) =>
        new(false, false, $"ROLLBACK: {reason}", 0, 0m, 0m, 0m, 0m);
}

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
