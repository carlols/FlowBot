using Discord;
using Discord.WebSocket;

namespace FlowBot;

public sealed partial class GroupFinderButtonHandler
{
    private async Task MovePlayersAsync(SocketMessageComponent component, GroupFinderSession session)
    {
        if (!CanMovePlayers(component.User, session.HostUserId))
        {
            await component.RespondAsync(
                "Only the group creator or server admins can move this group's players.",
                ephemeral: true);
            return;
        }

        if (session.PlayerIds.Count == 0)
        {
            await component.RespondAsync("This group has no registered players to move.", ephemeral: true);
            return;
        }

        var channelMenu = new SelectMenuBuilder()
            .WithCustomId(GroupFinderButtonIds.CreateVoiceChannelSelectId(component.Message.Id))
            .WithPlaceholder("Choose a destination voice channel")
            .WithMinValues(1)
            .WithMaxValues(1)
            .WithType(ComponentType.ChannelSelect)
            .WithChannelTypes(ChannelType.Voice);
        var components = new ComponentBuilder()
            .WithSelectMenu(channelMenu)
            .Build();

        await component.RespondAsync(
            "Choose where to move the registered players who are currently connected to voice.",
            components: components,
            ephemeral: true);
    }

    private async Task HandleVoiceChannelSelectionAsync(
        SocketMessageComponent component,
        ulong groupMessageId)
    {
        if (component.User is not SocketGuildUser guildUser)
        {
            await UpdateEphemeralResponseAsync(component, "Voice moves can only be used inside a server.");
            return;
        }

        var selectedChannelId = component.Data.Channels?.SingleOrDefault()?.Id;
        var destinationChannel = selectedChannelId is { } channelId
            ? guildUser.Guild.GetVoiceChannel(channelId)
            : null;

        if (destinationChannel is null)
        {
            await UpdateEphemeralResponseAsync(component, "Please choose a valid voice channel.");
            return;
        }

        await UpdateEphemeralResponseAsync(component, $"Moving registered players to {destinationChannel.Mention}...");

        try
        {
            var groupMessage = await component.Channel.GetMessageAsync(groupMessageId, CacheMode.AllowDownload);
            if (groupMessage is not IUserMessage userMessage
                || !GroupFinderMessageParser.TryReadSession(userMessage, out var session))
            {
                await ModifyPickerResponseAsync(component, "That group message no longer exists.");
                return;
            }

            if (!CanMovePlayers(component.User, session.HostUserId))
            {
                await ModifyPickerResponseAsync(
                    component,
                    "Only the group creator or server admins can move this group's players.");
                return;
            }

            if (!VoiceMemberMover.CanMoveTo(guildUser.Guild, destinationChannel, out var permissionMessage))
            {
                await ModifyPickerResponseAsync(component, permissionMessage);
                return;
            }

            var registeredPlayerIds = session.PlayerIds.ToHashSet();
            var connectedPlayers = guildUser.Guild.VoiceChannels
                .SelectMany(channel => channel.ConnectedUsers)
                .Where(user => registeredPlayerIds.Contains(user.Id))
                .DistinctBy(user => user.Id)
                .ToArray();

            if (connectedPlayers.Length == 0)
            {
                await ModifyPickerResponseAsync(
                    component,
                    "None of the registered players are currently connected to a voice channel.");
                return;
            }

            var result = await _voiceMemberMover.MoveAsync(
                guildUser.Guild,
                connectedPlayers,
                destinationChannel);
            var disconnectedPlayerCount = registeredPlayerIds.Count - connectedPlayers.Length;

            await ModifyPickerResponseAsync(
                component,
                BuildVoiceMoveSummary(destinationChannel, result, disconnectedPlayerCount));
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to move players for group finder message {MessageId}.",
                groupMessageId);
            await ModifyPickerResponseAsync(component, "I could not move this group's players.");
        }
    }

    private static bool CanMovePlayers(SocketUser user, ulong hostUserId) =>
        user.Id == hostUserId
        || user is SocketGuildUser guildUser && guildUser.GuildPermissions.Administrator;

    private static string BuildVoiceMoveSummary(
        SocketVoiceChannel destinationChannel,
        VoiceMoveResult result,
        int disconnectedPlayerCount)
    {
        var additionalDetails = disconnectedPlayerCount > 0
            ? new[] { $"{disconnectedPlayerCount} registered player(s) were not connected to voice." }
            : [];

        return VoiceMoveSummaryBuilder.Build(
            result,
            $"Moved {result.MovedUsers.Count} registered player(s) to {destinationChannel.Mention}.",
            $"No registered players needed to be moved to {destinationChannel.Mention}.",
            count => $"{count} were already there.",
            additionalDetails);
    }

    private static Task ModifyPickerResponseAsync(SocketMessageComponent component, string content) =>
        component.ModifyOriginalResponseAsync(properties =>
        {
            properties.Content = content;
            properties.Components = new ComponentBuilder().Build();
        });
}
