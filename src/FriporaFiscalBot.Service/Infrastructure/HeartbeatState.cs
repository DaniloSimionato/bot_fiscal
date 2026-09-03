namespace FriporaFiscalBot.Infrastructure;

public sealed class HeartbeatState
{
    private readonly object _sync = new();
    public DateTimeOffset LastCheckUtc { get; private set; }
    public bool DatabaseConnected { get; private set; }
    public string LastNote { get; private set; } = "";
    public string LastError { get; private set; } = "";
    public long ProcessedCount { get; private set; }

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
        mode = "HOMOLOGACAO",
        serie = 3,
        lastCheckUtc = LastCheckUtc,
        lastNote = LastNote,
        processedCount = ProcessedCount,
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
