using System.IO.Pipes;
using System.Text.Json;

namespace FriporaFiscalBot.Monitor;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new StatusForm());
    }
}

internal sealed class StatusForm : Form
{
    private readonly Label _status = MakeLabel();
    private readonly Label _details = MakeLabel();
    private readonly System.Windows.Forms.Timer _timer;

    public StatusForm()
    {
        Text = "Fripora Fiscal Bot";
        Width = 430;
        Height = 240;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(16),
            AutoScroll = true
        };
        layout.Controls.Add(_status);
        layout.Controls.Add(_details);
        Controls.Add(layout);

        _timer = new System.Windows.Forms.Timer { Interval = 5000 };
        _timer.Tick += async (_, _) => await ReadStatusAsync();
        _timer.Start();
        _ = ReadStatusAsync();
    }

    private async Task ReadStatusAsync()
    {
        try
        {
            await using var pipe = new NamedPipeClientStream(
                ".", "FriporaFiscalBot.Status", PipeDirection.In, PipeOptions.Asynchronous);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await pipe.ConnectAsync(timeout.Token);
            using var reader = new StreamReader(pipe);
            var json = await reader.ReadLineAsync(timeout.Token);
            var status = JsonSerializer.Deserialize<JsonElement>(json ?? "{}");

            var database = status.GetProperty("database").GetString();
            _status.Text = database == "connected"
                ? "● Serviço: Em execução   Banco: Conectado"
                : "● Serviço: Em execução   Banco: Desconectado";
            _status.ForeColor = database == "connected" ? Color.DarkGreen : Color.DarkOrange;
            var target = status.TryGetProperty("targetNote", out var targetProperty) &&
                         targetProperty.ValueKind != JsonValueKind.Null
                ? targetProperty.ToString()
                : "nenhuma";
            _details.Text = $"Modo: {status.GetProperty("mode").GetString()}\n" +
                            $"Série: {status.GetProperty("serie").GetInt32()}\n" +
                            $"Nota alvo: {target}\n" +
                            $"Última verificação UTC: {status.GetProperty("lastCheckUtc")}\n" +
                            $"Última nota: {status.GetProperty("lastNote")}\n" +
                            $"Processadas: {status.GetProperty("processedCount")}\n" +
                            $"Última ação: {status.GetProperty("lastAction")}\n" +
                            $"Último erro: {status.GetProperty("lastError")}";
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException)
        {
            _status.Text = "● Serviço: indisponível / banco sem heartbeat";
            _status.ForeColor = Color.DarkRed;
            _details.Text = "Não foi possível obter o status do serviço pelo Named Pipe local.";
        }
    }

    private static Label MakeLabel() => new()
    {
        AutoSize = true,
        Font = new Font("Segoe UI", 10f),
        Margin = new Padding(0, 0, 0, 12)
    };
}
