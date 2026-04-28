using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Options;

namespace FlowBot;

public sealed class DiscordBotService(
    DiscordSocketClient client,
    InteractionService interactions,
    RoleButtonHandler roleButtonHandler,
    GroupFinderButtonHandler groupFinderButtonHandler,
    IServiceProvider services,
    IOptions<FlowBotOptions> options,
    IHostApplicationLifetime lifetime,
    ILogger<DiscordBotService> logger) : BackgroundService
{
    private readonly FlowBotOptions _options = options.Value;
    private bool _commandsRegistered;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Token))
        {
            logger.LogCritical("Missing bot token. Set FlowBot:Token with user secrets or an environment variable.");
            lifetime.StopApplication();
            return;
        }

        client.Log += LogDiscordMessageAsync;
        interactions.Log += LogDiscordMessageAsync;
        client.Ready += RegisterCommandsAsync;
        client.InteractionCreated += HandleInteractionAsync;

        await interactions.AddModulesAsync(Assembly.GetExecutingAssembly(), services);
        await client.LoginAsync(TokenType.Bot, _options.Token);
        await client.StartAsync();

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected during application shutdown.
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        client.InteractionCreated -= HandleInteractionAsync;
        client.Ready -= RegisterCommandsAsync;
        interactions.Log -= LogDiscordMessageAsync;
        client.Log -= LogDiscordMessageAsync;

        await client.StopAsync();
        await client.LogoutAsync();
        await base.StopAsync(cancellationToken);
    }

    private async Task RegisterCommandsAsync()
    {
        if (_commandsRegistered)
        {
            return;
        }

        if (_options.ServerId is { } serverId)
        {
            await interactions.RegisterCommandsToGuildAsync(serverId);
            logger.LogInformation("Registered slash commands to server {ServerId}.", serverId);
        }
        else
        {
            await interactions.RegisterCommandsGloballyAsync();
            logger.LogInformation("Registered global slash commands. Discord may take up to an hour to show them.");
        }

        _commandsRegistered = true;
        logger.LogInformation("FlowBot is connected as {Username}.", client.CurrentUser);
    }

    private async Task HandleInteractionAsync(SocketInteraction interaction)
    {
        if (interaction is SocketMessageComponent component
            && RoleButtonIds.IsRoleButton(component.Data.CustomId))
        {
            await roleButtonHandler.HandleAsync(component);
            return;
        }

        if (interaction is SocketMessageComponent groupFinderComponent
            && GroupFinderButtonIds.IsGroupFinderButton(groupFinderComponent.Data.CustomId))
        {
            await groupFinderButtonHandler.HandleAsync(groupFinderComponent);
            return;
        }

        var context = new SocketInteractionContext(client, interaction);
        var result = await interactions.ExecuteCommandAsync(context, services);

        if (!result.IsSuccess)
        {
            logger.LogWarning("Interaction failed: {Error} {Reason}", result.Error, result.ErrorReason);
        }
    }

    private Task LogDiscordMessageAsync(LogMessage message)
    {
        var logLevel = message.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            LogSeverity.Debug => LogLevel.Trace,
            _ => LogLevel.Information,
        };

        logger.Log(logLevel, message.Exception, "[Discord] {Source}: {Message}", message.Source, message.Message);
        return Task.CompletedTask;
    }
}
