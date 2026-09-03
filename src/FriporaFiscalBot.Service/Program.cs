using FriporaFiscalBot.Configuration;
using FriporaFiscalBot.Infrastructure;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<BotOptions>(builder.Configuration.GetSection("FriporaFiscalBot"));
builder.Services.AddSingleton<IBotClock, SystemBotClock>();
builder.Services.AddSingleton<HeartbeatState>();
builder.Services.AddSingleton<FirebirdNoteRepository>();
builder.Services.AddSingleton<LocalStatusPipe>();
builder.Services.AddHostedService<Worker>();
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Fripora Fiscal Bot Service";
});

await builder.Build().RunAsync();
