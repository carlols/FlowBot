using Discord;
using Discord.WebSocket;

namespace FlowBot;

public sealed class GroupFinderButtonHandler(
    GroupFinderNotificationService notificationService,
    ILogger<GroupFinderButtonHandler> logger)
{
    public async Task HandleAsync(SocketMessageComponent component)
    {
        if (GroupFinderButtonIds.TryParseReadyResponse(component.Data.CustomId, out var readyResponse))
        {
            await HandleReadyResponseAsync(component, readyResponse);
            return;
        }

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

        if (buttonState.Action == GroupFinderButtonAction.ReadyCheck)
        {
            await ReadyCheckAsync(component, session);
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
                await component.FollowupAsync("You joined the group.", ephemeral: true);
                return;
            }

            await UpdateGroupMessageAsync(component, updatedSession);
            await component.FollowupAsync("You joined the group.", ephemeral: true);
            return;
        }

        if (!isRegistered)
        {
            await component.RespondAsync("You are not in this group.", ephemeral: true);
            return;
        }

        playerIds.Remove(userId);
        var readyStates = session.ReadyStates
            .Where(pair => pair.Key != userId)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        await UpdateGroupMessageAsync(component, session with { PlayerIds = playerIds, ReadyStates = readyStates });
        await component.FollowupAsync("You left the group.", ephemeral: true);
    }

    public async Task HandleReadyCheckModalAsync(SocketModal modal)
    {
        if (!GroupFinderButtonIds.TryParseReadyCheckModal(modal.Data.CustomId, out var modalState))
        {
            await modal.RespondAsync("I could not identify this ready check form.", ephemeral: true);
            return;
        }

        var message = modal.Data.Components
            .FirstOrDefault(component => component.CustomId == GroupFinderMessageBuilder.ReadyCheckMessageInputId)
            ?.Value;

        if (message?.Length > GroupFinderMessageBuilder.ReadyCheckMessageMaxLength)
        {
            await modal.RespondAsync(
                $"Ready check messages can be at most {GroupFinderMessageBuilder.ReadyCheckMessageMaxLength} characters.",
                ephemeral: true);
            return;
        }

        try
        {
            var originalMessage = await modal.Channel.GetMessageAsync(modalState.MessageId);

            if (originalMessage is not IUserMessage userMessage)
            {
                await modal.RespondAsync("That group message no longer exists.", ephemeral: true);
                return;
            }

            if (!GroupFinderMessageBuilder.TryReadSession(
                    userMessage,
                    modalState.Capacity,
                    modalState.CapacityNoticeSent,
                    modalState.SessionStarted,
                    out var session)
                || session.HostUserId != modal.User.Id)
            {
                await modal.RespondAsync("Only the group creator can start a ready check.", ephemeral: true);
                return;
            }

            if (session.HasActiveReadyCheck)
            {
                await modal.RespondAsync("This group already has an active ready check.", ephemeral: true);
                return;
            }

            if (session.PlayerIds.Count < 2)
            {
                await modal.RespondAsync("Ready checks need at least two registered players.", ephemeral: true);
                return;
            }

            var readyStates = session.PlayerIds.ToDictionary(
                playerId => playerId,
                _ => GroupFinderReadyState.Waiting);
            var updatedSession = session with { ReadyStates = readyStates };

            await userMessage.ModifyAsync(properties =>
            {
                properties.Embed = GroupFinderMessageBuilder.BuildEmbed(updatedSession);
                properties.Components = GroupFinderMessageBuilder.BuildComponents(
                    updatedSession.Capacity,
                    updatedSession.PlayerIds.Count,
                    updatedSession.CapacityNoticeSent,
                    updatedSession.SessionStarted);
            });

            await notificationService.SendReadyCheckAsync(
                modal.Channel,
                updatedSession,
                userMessage.Id,
                message);

            await modal.RespondAsync("Ready check sent.", ephemeral: true);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to start ready check for message {MessageId}.", modalState.MessageId);
            await modal.RespondAsync("I could not start this ready check.", ephemeral: true);
        }
    }

    private async Task ReadyCheckAsync(SocketMessageComponent component, GroupFinderSession session)
    {
        if (component.User.Id != session.HostUserId)
        {
            await component.RespondAsync("Only the group creator can start a ready check.", ephemeral: true);
            return;
        }

        if (session.SessionStarted)
        {
            await component.RespondAsync("This session has already been started.", ephemeral: true);
            return;
        }

        if (session.HasActiveReadyCheck)
        {
            await component.RespondAsync("This group already has an active ready check.", ephemeral: true);
            return;
        }

        if (session.PlayerIds.Count < 2)
        {
            await component.RespondAsync("Ready checks need at least two registered players.", ephemeral: true);
            return;
        }

        var modal = new ModalBuilder()
            .WithTitle("Ready check")
            .WithCustomId(GroupFinderButtonIds.CreateReadyCheckModalId(
                component.Message.Id,
                session.Capacity,
                session.CapacityNoticeSent,
                session.SessionStarted))
            .AddTextInput(
                label: "Message",
                customId: GroupFinderMessageBuilder.ReadyCheckMessageInputId,
                style: TextInputStyle.Paragraph,
                placeholder: "Optional message for the group",
                minLength: 0,
                maxLength: GroupFinderMessageBuilder.ReadyCheckMessageMaxLength,
                required: false)
            .Build();

        await component.RespondWithModalAsync(modal);
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
            "This will mention all registered players in this channel.",
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

            var updatedSession = session with
            {
                SessionStarted = true,
                ReadyStates = new Dictionary<ulong, GroupFinderReadyState>(),
            };

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

    private async Task HandleReadyResponseAsync(
        SocketMessageComponent component,
        GroupFinderReadyResponseState readyResponse)
    {
        try
        {
            var message = await component.Channel.GetMessageAsync(readyResponse.MessageId);

            if (message is not IUserMessage userMessage)
            {
                await component.RespondAsync("That group message no longer exists.", ephemeral: true);
                return;
            }

            if (!GroupFinderMessageBuilder.TryReadSession(
                    userMessage,
                    readyResponse.Capacity,
                    readyResponse.CapacityNoticeSent,
                    readyResponse.SessionStarted,
                    out var session))
            {
                await component.RespondAsync("I could not read that group finder message.", ephemeral: true);
                return;
            }

            if (!session.HasActiveReadyCheck)
            {
                await component.RespondAsync("This group does not have an active ready check.", ephemeral: true);
                return;
            }

            if (!session.PlayerIds.Contains(component.User.Id))
            {
                await component.RespondAsync("Only registered players can answer this ready check.", ephemeral: true);
                return;
            }

            var readyState = readyResponse.Action == GroupFinderButtonAction.Ready
                ? GroupFinderReadyState.Ready
                : GroupFinderReadyState.NotReady;
            var readyStates = session.ReadyStates.ToDictionary(pair => pair.Key, pair => pair.Value);
            readyStates[component.User.Id] = readyState;
            var updatedSession = session with { ReadyStates = readyStates };

            await userMessage.ModifyAsync(properties =>
            {
                properties.Embed = GroupFinderMessageBuilder.BuildEmbed(updatedSession);
                properties.Components = GroupFinderMessageBuilder.BuildComponents(
                    updatedSession.Capacity,
                    updatedSession.PlayerIds.Count,
                    updatedSession.CapacityNoticeSent,
                    updatedSession.SessionStarted);
            });

            await component.RespondAsync(
                readyState == GroupFinderReadyState.Ready
                    ? "You are marked ready."
                    : "You are marked not ready.",
                ephemeral: true);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to update ready check for message {MessageId}.", readyResponse.MessageId);
            await component.RespondAsync("I could not update this ready check.", ephemeral: true);
        }
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
