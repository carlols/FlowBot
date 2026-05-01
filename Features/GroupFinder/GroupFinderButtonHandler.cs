using Discord;
using Discord.WebSocket;

namespace FlowBot;

public sealed class GroupFinderButtonHandler(
    DiscordSocketClient client,
    ILogger<GroupFinderButtonHandler> logger)
{
    public async Task HandleAsync(SocketMessageComponent component)
    {
        if (GroupFinderButtonIds.TryParseStartConfirmation(
            component.Data.CustomId,
            out var startConfirmationAction,
            out var startMessageId,
            out var startHostUserId,
            out var startCapacity,
            out var startCapacityNoticeSentFromComponents,
            out var startSessionStartedFromComponents))
        {
            await HandleStartConfirmationAsync(
                component,
                startConfirmationAction,
                startMessageId,
                startHostUserId,
                startCapacity,
                startCapacityNoticeSentFromComponents,
                startSessionStartedFromComponents);
            return;
        }

        if (GroupFinderButtonIds.TryParseCloseConfirmation(
            component.Data.CustomId,
            out var confirmationAction,
            out var messageId,
            out var hostUserId))
        {
            await HandleCloseConfirmationAsync(component, confirmationAction, messageId, hostUserId);
            return;
        }

        if (!GroupFinderButtonIds.TryParse(
            component.Data.CustomId,
            out var action,
            out var capacity,
            out var capacityNoticeSentFromComponents,
            out var sessionStartedFromComponents))
        {
            await component.RespondAsync("I could not identify this group finder button.", ephemeral: true);
            return;
        }

        if (!GroupFinderMessageBuilder.TryReadSession(
            component.Message,
            capacity,
            capacityNoticeSentFromComponents,
            sessionStartedFromComponents,
            out var session))
        {
            await component.RespondAsync("I could not read this group finder message.", ephemeral: true);
            return;
        }

        var playerIds = session.PlayerIds.ToList();
        var userId = component.User.Id;
        var isRegistered = playerIds.Contains(userId);

        if (action == GroupFinderButtonAction.Close)
        {
            await CloseGroupAsync(component, session);
            return;
        }

        if (action == GroupFinderButtonAction.Start)
        {
            await StartSessionAsync(component, session);
            return;
        }

        if (action == GroupFinderButtonAction.Join)
        {
            if (isRegistered)
            {
                await component.RespondAsync("You are already in this group.", ephemeral: true);
                return;
            }

            if (session.IsFull)
            {
                await component.RespondAsync("This group is already full.", ephemeral: true);
                return;
            }

            playerIds.Add(userId);
            var updatedSession = session with { PlayerIds = playerIds };

            if (updatedSession.IsFull && !session.CapacityNoticeSent)
            {
                updatedSession = updatedSession with { CapacityNoticeSent = true };
                await UpdateGroupMessageAsync(component, updatedSession);
                await SendCapacityNoticeAsync(component, updatedSession);
                return;
            }

            await UpdateGroupMessageAsync(component, updatedSession);
            return;
        }

        if (!isRegistered)
        {
            await component.RespondAsync("You are not in this group.", ephemeral: true);
            return;
        }

        playerIds.Remove(userId);
        await UpdateGroupMessageAsync(component, session with { PlayerIds = playerIds });
    }

    private static async Task SendGroupNotificationAsync(
        IMessageChannel channel,
        GroupFinderSession session,
        string message)
    {
        var playerMentions = string.Join(" ", session.PlayerIds.Select(playerId => $"<@{playerId}>"));

        await channel.SendMessageAsync(
            text: $"{message}\n{playerMentions}",
            allowedMentions: new AllowedMentions { UserIds = session.PlayerIds.ToList() });
    }

    private async Task SendCapacityNoticeAsync(SocketMessageComponent component, GroupFinderSession session)
    {
        var messageLink = CreateMessageLink(component);
        var message = $"Your group for **{session.GameName}** is full.";

        if (messageLink is not null)
        {
            message += $"{Environment.NewLine}{Environment.NewLine}Open group message:{Environment.NewLine}{messageLink}";
        }

        try
        {
            IUser? host = client.GetUser(session.HostUserId);
            host ??= await client.Rest.GetUserAsync(session.HostUserId);

            if (host is null)
            {
                logger.LogWarning(
                    "Could not DM group finder host {HostUserId} because the user was not cached.",
                    session.HostUserId);
                return;
            }

            await host.SendMessageAsync(message);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to DM group finder host {HostUserId} that message {MessageId} is full.",
                session.HostUserId,
                component.Message.Id);
        }
    }

    private static string? CreateMessageLink(SocketMessageComponent component)
    {
        if (component.Channel is not SocketGuildChannel guildChannel)
        {
            return null;
        }

        return $"https://discord.com/channels/{guildChannel.Guild.Id}/{component.Channel.Id}/{component.Message.Id}";
    }

    private async Task StartSessionAsync(SocketMessageComponent component, GroupFinderSession session)
    {
        if (component.User.Id != session.HostUserId)
        {
            await component.RespondAsync("Only the group creator can start this session.", ephemeral: true);
            return;
        }

        if (session.SessionStarted)
        {
            await component.RespondAsync("This session has already been started.", ephemeral: true);
            return;
        }

        var components = new ComponentBuilder()
            .WithButton(
                label: "Confirm start",
                customId: GroupFinderButtonIds.CreateConfirmStartId(
                    component.Message.Id,
                    session.HostUserId,
                    session.Capacity,
                    session.CapacityNoticeSent,
                    session.SessionStarted),
                style: ButtonStyle.Primary)
            .WithButton(
                label: "Cancel",
                customId: GroupFinderButtonIds.CreateCancelStartId(),
                style: ButtonStyle.Secondary)
            .Build();

        await component.RespondAsync(
            "Starting this session will ping every registered player.",
            components: components,
            ephemeral: true);
    }

    private async Task HandleStartConfirmationAsync(
        SocketMessageComponent component,
        GroupFinderButtonAction action,
        ulong messageId,
        ulong hostUserId,
        int? capacity,
        bool? capacityNoticeSentFromComponents,
        bool? sessionStartedFromComponents)
    {
        if (action == GroupFinderButtonAction.CancelStart)
        {
            await component.UpdateAsync(properties =>
            {
                properties.Content = "Start cancelled.";
                properties.Components = new ComponentBuilder().Build();
            });
            return;
        }

        if (component.User.Id != hostUserId)
        {
            await component.UpdateAsync(properties =>
            {
                properties.Content = "Only the group creator can start this session.";
                properties.Components = new ComponentBuilder().Build();
            });
            return;
        }

        try
        {
            var message = await component.Channel.GetMessageAsync(messageId);

            if (message is not IUserMessage userMessage)
            {
                await component.UpdateAsync(properties =>
                {
                    properties.Content = "That group message no longer exists.";
                    properties.Components = new ComponentBuilder().Build();
                });
                return;
            }

            if (!GroupFinderMessageBuilder.TryReadSession(
                userMessage,
                capacity,
                capacityNoticeSentFromComponents,
                sessionStartedFromComponents,
                out var session)
                || session.HostUserId != hostUserId)
            {
                await component.UpdateAsync(properties =>
                {
                    properties.Content = "I could not read that group finder message.";
                    properties.Components = new ComponentBuilder().Build();
                });
                return;
            }

            if (session.SessionStarted)
            {
                await component.UpdateAsync(properties =>
                {
                    properties.Content = "This session has already been started.";
                    properties.Components = new ComponentBuilder().Build();
                });
                return;
            }

            var updatedSession = session with { SessionStarted = true };

            await userMessage.ModifyAsync(properties =>
            {
                properties.Embed = GroupFinderMessageBuilder.BuildEmbed(updatedSession);
                properties.Components = GroupFinderMessageBuilder.BuildComponents(
                    updatedSession.Capacity,
                    updatedSession.PlayerIds.Count,
                    updatedSession.CapacityNoticeSent,
                    updatedSession.SessionStarted);
            });

            await SendGroupNotificationAsync(
                component.Channel,
                updatedSession,
                $"**{updatedSession.GameName}** is starting!");

            await component.UpdateAsync(properties =>
            {
                properties.Content = "Session started. Registered players have been notified.";
                properties.Components = new ComponentBuilder().Build();
            });
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to start group finder session for message {MessageId}.", messageId);
            await component.UpdateAsync(properties =>
            {
                properties.Content = "I could not start this session.";
                properties.Components = new ComponentBuilder().Build();
            });
        }
    }

    private async Task CloseGroupAsync(SocketMessageComponent component, GroupFinderSession session)
    {
        if (!CanCloseGroup(component.User, session.HostUserId))
        {
            await component.RespondAsync("Only the host or moderators can close this group.", ephemeral: true);
            return;
        }

        var components = new ComponentBuilder()
            .WithButton(
                label: "Confirm close",
                customId: GroupFinderButtonIds.CreateConfirmCloseId(component.Message.Id, session.HostUserId),
                style: ButtonStyle.Danger)
            .WithButton(
                label: "Cancel",
                customId: GroupFinderButtonIds.CreateCancelCloseId(),
                style: ButtonStyle.Secondary)
            .Build();

        await component.RespondAsync(
            "You can close this group. Confirming will delete the group finder message.",
            components: components,
            ephemeral: true);
    }

    private async Task HandleCloseConfirmationAsync(
        SocketMessageComponent component,
        GroupFinderButtonAction action,
        ulong messageId,
        ulong hostUserId)
    {
        if (action == GroupFinderButtonAction.CancelClose)
        {
            await component.UpdateAsync(properties =>
            {
                properties.Content = "Close cancelled.";
                properties.Components = new ComponentBuilder().Build();
            });
            return;
        }

        if (!CanCloseGroup(component.User, hostUserId))
        {
            await component.UpdateAsync(properties =>
            {
                properties.Content = "Only the host or moderators can close this group.";
                properties.Components = new ComponentBuilder().Build();
            });
            return;
        }

        try
        {
            var message = await component.Channel.GetMessageAsync(messageId);

            if (message is null)
            {
                await component.UpdateAsync(properties =>
                {
                    properties.Content = "That group message no longer exists.";
                    properties.Components = new ComponentBuilder().Build();
                });
                return;
            }

            await message.DeleteAsync();
            await component.UpdateAsync(properties =>
            {
                properties.Content = "Group closed.";
                properties.Components = new ComponentBuilder().Build();
            });
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to delete group finder message {MessageId}.", messageId);
            await component.UpdateAsync(properties =>
            {
                properties.Content = "I could not delete this group message.";
                properties.Components = new ComponentBuilder().Build();
            });
        }
    }

    private static bool CanCloseGroup(SocketUser user, ulong hostUserId)
    {
        if (user.Id == hostUserId)
        {
            return true;
        }

        return user is SocketGuildUser guildUser
            && (guildUser.GuildPermissions.Administrator
                || guildUser.GuildPermissions.ManageMessages);
    }

    private async Task UpdateGroupMessageAsync(SocketMessageComponent component, GroupFinderSession session)
    {
        try
        {
            await component.UpdateAsync(properties =>
            {
                properties.Embed = GroupFinderMessageBuilder.BuildEmbed(session);
                properties.Components = GroupFinderMessageBuilder.BuildComponents(
                    session.Capacity,
                    session.PlayerIds.Count,
                    session.CapacityNoticeSent,
                    session.SessionStarted);
            });
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to update group finder message {MessageId}.", component.Message.Id);
            await component.RespondAsync("I could not update this group message.", ephemeral: true);
        }
    }
}
