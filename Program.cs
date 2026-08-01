using FlowBot;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<FlowBotOptions>(
    builder.Configuration.GetSection(FlowBotOptions.SectionName));

builder.Services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildVoiceStates,
    MessageCacheSize = 0,
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
builder.Services.AddSingleton<RolePanelHandler>();
builder.Services.AddSingleton<RoleSelectionUpdater>();
builder.Services.AddSingleton<DiscordMessageMutationLock>();
builder.Services.AddSingleton<GroupFinderButtonHandler>();
builder.Services.AddSingleton<GroupFinderNotificationService>();
builder.Services.AddSingleton<GroupFinderRelatedMessageCleaner>();
builder.Services.AddSingleton<GroupFinderTeamScrambler>();
builder.Services.AddSingleton<GroupFinderTimeParser>();
builder.Services.AddSingleton<PullCountGuessHandler>();
builder.Services.AddSingleton<HttpClient>();
builder.Services.AddSingleton<EmojiImportHandler>();
builder.Services.AddSingleton<EmojiImageOptimizer>();
builder.Services.AddSingleton<SevenTvEmojiService>();
builder.Services.AddSingleton<VoiceMemberMover>();
builder.Services.AddSingleton<RoleVoiceMoveHandler>();
builder.Services.AddSingleton<DiscordInteractionRouter>();
builder.Services.AddHostedService<DiscordBotService>();

var host = builder.Build();
host.Run();
