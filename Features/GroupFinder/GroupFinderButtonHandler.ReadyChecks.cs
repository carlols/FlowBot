using Discord;
using Discord.WebSocket;

namespace FlowBot;

public sealed partial class GroupFinderButtonHandler
{
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

        await modal.DeferAsync(ephemeral: true);

        try
        {
            using (await _messageMutationLock.AcquireAsync(modalState.MessageId))
            {
                var current = await LoadCurrentSessionAsync(modal.Channel, modalState.MessageId);
                if (current is null)
                {
                    await modal.FollowupAsync("That group message no longer exists.", ephemeral: true);
                    return;
                }

                var (userMessage, session) = current.Value;

                if (session.HostUserId != modal.User.Id)
                {
                    await modal.FollowupAsync("Only the group creator can start a ready check.", ephemeral: true);
                    return;
                }

                if (session.HasActiveReadyCheck)
                {
                    await modal.FollowupAsync("This group already has an active ready check.", ephemeral: true);
                    return;
                }

                if (session.PlayerIds.Count < 2)
                {
                    await modal.FollowupAsync("Ready checks need at least two registered players.", ephemeral: true);
                    return;
                }

                var readyStates = session.PlayerIds.ToDictionary(
                    playerId => playerId,
                    _ => GroupFinderReadyState.Waiting);
                var updatedSession = session with { ReadyStates = readyStates };

                await UpdateGroupMessageAsync(userMessage, updatedSession);
                await _notificationService.SendReadyCheckAsync(
                    modal.Channel,
                    updatedSession,
                    userMessage.Id,
                    message);
            }

            await modal.FollowupAsync("Ready check sent.", ephemeral: true);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to start ready check for message {MessageId}.", modalState.MessageId);
            await modal.FollowupAsync("I could not start this ready check.", ephemeral: true);
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

    private async Task HandleReadyResponseAsync(
        SocketMessageComponent component,
        GroupFinderReadyResponseState readyResponse)
    {
        await component.DeferAsync(ephemeral: true);

        try
        {
            GroupFinderReadyState readyState;

            using (await _messageMutationLock.AcquireAsync(readyResponse.MessageId))
            {
                var current = await LoadCurrentSessionAsync(component.Channel, readyResponse.MessageId);
                if (current is null)
                {
                    await component.FollowupAsync("That group message no longer exists.", ephemeral: true);
                    return;
                }

                var (userMessage, session) = current.Value;

                if (!session.HasActiveReadyCheck)
                {
                    await component.FollowupAsync("This group does not have an active ready check.", ephemeral: true);
                    return;
                }

                if (!session.PlayerIds.Contains(component.User.Id))
                {
                    await component.FollowupAsync("Only registered players can answer this ready check.", ephemeral: true);
                    return;
                }

                readyState = readyResponse.Action == GroupFinderButtonAction.Ready
                    ? GroupFinderReadyState.Ready
                    : GroupFinderReadyState.NotReady;
                var readyStates = session.ReadyStates.ToDictionary(pair => pair.Key, pair => pair.Value);
                readyStates[component.User.Id] = readyState;

                await UpdateGroupMessageAsync(userMessage, session with { ReadyStates = readyStates });
            }

            await component.FollowupAsync(
                readyState == GroupFinderReadyState.Ready
                    ? "You are marked ready."
                    : "You are marked not ready.",
                ephemeral: true);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to update ready check for message {MessageId}.", readyResponse.MessageId);
            await component.FollowupAsync("I could not update this ready check.", ephemeral: true);
        }
    }
}