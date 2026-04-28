using FlowBot;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<FlowBotOptions>(
    builder.Configuration.GetSection(FlowBotOptions.SectionName));

builder.Services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.Guilds,
    LogGatewayIntentWarnings = false,
}));

builder.Services.AddSingleton(sp =>
{
    var client = sp.GetRequiredService<DiscordSocketClient>();

    return new InteractionService(client.Rest, new InteractionServiceConfig
    {
        LogLevel = LogSeverity.Info,
        DefaultRunMode = RunMode.Async,
        UseCompiledLambda = true,
    });
});

builder.Services.AddSingleton<RoleButtonHandler>();
builder.Services.AddHostedService<DiscordBotService>();

var host = builder.Build();
host.Run();
