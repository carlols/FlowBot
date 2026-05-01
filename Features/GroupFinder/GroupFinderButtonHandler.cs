using Discord;
using Discord.WebSocket;

namespace FlowBot;

public sealed class GroupFinderButtonHandler(
    GroupFinderNotificationService notificationService,
    ILogger<GroupFinderButtonHandler> logger)
{
    public async Task HandleAsync(SocketMessageComponent component)
    {
        if (GroupFinderButtonIds.TryParseStartConfirmation(component.Data.CustomId, out var startConfirmation))
        {
            await HandleStartConfirmationAsync(component, startConfirmation);
            return;
        }

        if (GroupFinderButtonIds.TryParseCloseConfirmation(component.Data.CustomId, out var closeConfirmation))
        {
            await HandleCloseConfirmationAsync(component, closeConfirmation);
            return;
        }

        if (!GroupFinderButtonIds.TryParse(component.Data.CustomId, out var buttonState))
        {
            await component.RespondAsync("I could not identify this group finder button.", ephemeral: true);
            return;
        }

        if (!GroupFinderMessageBuilder.TryReadSession(
            component.Message,
            buttonState.Capacity,
            buttonState.CapacityNoticeSent,
            buttonState.SessionStarted,
            out var session))
        {
            await component.RespondAsync("I could not read this group finder message.", ephemeral: true);
            return;
        }

        var playerIds = session.PlayerIds.ToList();
        var userId = component.User.Id;
        var isRegistered = playerIds.Contains(userId);

        if (buttonState.Action == GroupFinderButtonAction.Close)
        {
            await CloseGroupAsync(component, session);
            return;
        }

        if (buttonState.Action == GroupFinderButtonAction.Start)
        {
            await StartSessionAsync(component, session);
            return;
        }

        if (buttonState.Action == GroupFinderButtonAction.Join)
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
                await notificationService.SendCapacityNoticeAsync(component, updatedSession);
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
        GroupFinderStartConfirmation confirmation)
    {
        if (confirmation.Action == GroupFinderButtonAction.CancelStart)
        {
            await UpdateEphemeralResponseAsync(component, "Start cancelled.");
            return;
        }

        if (component.User.Id != confirmation.HostUserId)
        {
            await UpdateEphemeralResponseAsync(component, "Only the group creator can start this session.");
            return;
        }

        try
        {
            var message = await component.Channel.GetMessageAsync(confirmation.MessageId);

            if (message is not IUserMessage userMessage)
            {
                await UpdateEphemeralResponseAsync(component, "That group message no longer exists.");
                return;
            }

            if (!GroupFinderMessageBuilder.TryReadSession(
                userMessage,
                confirmation.Capacity,
                confirmation.CapacityNoticeSent,
                confirmation.SessionStarted,
                out var session)
                || session.HostUserId != confirmation.HostUserId)
            {
                await UpdateEphemeralResponseAsync(component, "I could not read that group finder message.");
                return;
            }

            if (session.SessionStarted)
            {
                await UpdateEphemeralResponseAsync(component, "This session has already been started.");
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

            await notificationService.SendSessionStartedAsync(component.Channel, updatedSession);

            await UpdateEphemeralResponseAsync(component, "Session started. Registered players have been notified.");
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to start group finder session for message {MessageId}.",
                confirmation.MessageId);
            await UpdateEphemeralResponseAsync(component, "I could not start this session.");
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
        GroupFinderCloseConfirmation confirmation)
    {
        if (confirmation.Action == GroupFinderButtonAction.CancelClose)
        {
            await UpdateEphemeralResponseAsync(component, "Close cancelled.");
            return;
        }

        if (!CanCloseGroup(component.User, confirmation.HostUserId))
        {
            await UpdateEphemeralResponseAsync(component, "Only the host or moderators can close this group.");
            return;
        }

        try
        {
            var message = await component.Channel.GetMessageAsync(confirmation.MessageId);

            if (message is null)
            {
                await UpdateEphemeralResponseAsync(component, "That group message no longer exists.");
                return;
            }

            await message.DeleteAsync();
            await UpdateEphemeralResponseAsync(component, "Group closed.");
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to delete group finder message {MessageId}.",
                confirmation.MessageId);
            await UpdateEphemeralResponseAsync(component, "I could not delete this group message.");
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

    private static Task UpdateEphemeralResponseAsync(SocketMessageComponent component, string content) =>
        component.UpdateAsync(properties =>
        {
            properties.Content = content;
            properties.Components = new ComponentBuilder().Build();
        });
}
