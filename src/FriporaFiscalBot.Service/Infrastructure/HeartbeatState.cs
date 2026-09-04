namespace FriporaFiscalBot.Infrastructure;

public sealed class HeartbeatState
{
    private readonly object _sync = new();
    public DateTimeOffset LastCheckUtc { get; private set; }
    public bool DatabaseConnected { get; private set; }
    public string LastNote { get; private set; } = "";
    public string LastError { get; private set; } = "";
    public long ProcessedCount { get; private set; }
    public string Mode { get; private set; } = "SIMULACAO";
    public int Serie { get; private set; } = 3;
    public int? TargetNote { get; private set; }
    public string LastAction { get; private set; } = "";

    public void Configure(string mode, int serie, int? targetNote)
    {
        lock (_sync)
        {
            Mode = mode;
            Serie = serie;
            TargetNote = targetNote;
        }
    }

    public void MarkApplication(ApplyNoteResult result)
    {
        lock (_sync)
        {
            LastAction = result.Message;
            if (result.Committed)
                ProcessedCount++;
        }
    }

    public void MarkCheck(bool connected, string lastNote = "", string lastError = "")
    {
        lock (_sync)
        {
            LastCheckUtc = DateTimeOffset.UtcNow;
            DatabaseConnected = connected;
            LastNote = lastNote;
            LastError = lastError;
        }
    }

    public object Snapshot() => new
    {
        service = "running",
        database = DatabaseConnected ? "connected" : "disconnected",
        mode = Mode,
        serie = Serie,
        targetNote = TargetNote,
        lastCheckUtc = LastCheckUtc,
        lastNote = LastNote,
        processedCount = ProcessedCount,
        lastAction = LastAction,
        lastError = LastError
    };
}

public interface IBotClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemBotClock : IBotClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
