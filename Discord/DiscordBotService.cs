using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Options;

namespace FlowBot;

public sealed class DiscordBotService(
    DiscordSocketClient client,
    InteractionService interactions,
    DiscordInteractionRouter interactionRouter,
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
        client.Ready += HandleReadyAsync;
        client.JoinedGuild += HandleJoinedGuildAsync;
        client.InteractionCreated += interactionRouter.RouteAsync;

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
        client.InteractionCreated -= interactionRouter.RouteAsync;
        client.JoinedGuild -= HandleJoinedGuildAsync;
        client.Ready -= HandleReadyAsync;
        interactions.Log -= LogDiscordMessageAsync;
        client.Log -= LogDiscordMessageAsync;

        await client.StopAsync();
        await client.LogoutAsync();
        await base.StopAsync(cancellationToken);
    }

    private async Task HandleReadyAsync()
    {
        if (!ValidateServerConfiguration())
        {
            lifetime.StopApplication();
            return;
        }

        await LeaveDisallowedGuildsAsync();
        await RegisterCommandsAsync();
    }

    private async Task RegisterCommandsAsync()
    {
        if (_commandsRegistered)
        {
            return;
        }

        if (_options.AllowedServerIds.Length > 0)
        {
            var currentAllowedServerIds = client.Guilds
                .Where(guild => IsGuildAllowed(guild.Id))
                .Select(guild => guild.Id)
                .Distinct()
                .ToArray();

            foreach (var allowedServerId in currentAllowedServerIds)
            {
                await RegisterCommandsToAllowedGuildAsync(allowedServerId);
            }
        }
        else if (_options.ServerId is { } serverId)
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

    private async Task HandleJoinedGuildAsync(SocketGuild guild)
    {
        if (IsGuildAllowed(guild.Id))
        {
            logger.LogInformation("FlowBot joined allowed server {GuildName} ({GuildId}).", guild.Name, guild.Id);
            await RegisterCommandsToAllowedGuildAsync(guild.Id);
            return;
        }

        logger.LogWarning(
            "FlowBot was added to unallowed server {GuildName} ({GuildId}) and will leave.",
            guild.Name,
            guild.Id);
        await guild.LeaveAsync();
    }

    private async Task LeaveDisallowedGuildsAsync()
    {
        foreach (var guild in client.Guilds.Where(guild => !IsGuildAllowed(guild.Id)).ToArray())
        {
            logger.LogWarning(
                "FlowBot is in unallowed server {GuildName} ({GuildId}) and will leave.",
                guild.Name,
                guild.Id);
            await guild.LeaveAsync();
        }
    }

    private bool ValidateServerConfiguration()
    {
        if (_options.ServerId is not { } serverId
            || _options.AllowedServerIds.Length == 0
            || _options.AllowedServerIds.Contains(serverId))
        {
            return true;
        }

        logger.LogCritical(
            "FlowBot:ServerId {ServerId} must also be included in FlowBot:AllowedServerIds when the allowlist is configured.",
            serverId);
        return false;
    }

    private bool IsGuildAllowed(ulong guildId) =>
        _options.AllowedServerIds.Length == 0
        || _options.AllowedServerIds.Contains(guildId);

    private async Task RegisterCommandsToAllowedGuildAsync(ulong guildId)
    {
        await interactions.RegisterCommandsToGuildAsync(guildId);
        logger.LogInformation("Registered slash commands to allowed server {ServerId}.", guildId);
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
