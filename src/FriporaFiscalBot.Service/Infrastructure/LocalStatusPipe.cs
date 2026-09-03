using System.IO.Pipes;
using System.Text.Json;

namespace FriporaFiscalBot.Infrastructure;

public sealed class LocalStatusPipe
{
    private readonly HeartbeatState _state;
    public LocalStatusPipe(HeartbeatState state) => _state = state;

    public async Task ServeAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var pipe = new NamedPipeServerStream(
                "FriporaFiscalBot.Status", PipeDirection.Out, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            await pipe.WaitForConnectionAsync(cancellationToken);
            var payload = JsonSerializer.Serialize(_state.Snapshot()) + "\n";
            await using var writer = new StreamWriter(pipe) { AutoFlush = true };
            await writer.WriteAsync(payload);
        }
    }
}
