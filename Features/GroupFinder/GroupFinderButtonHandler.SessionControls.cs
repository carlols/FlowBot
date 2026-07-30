using Discord;
using Discord.WebSocket;

namespace FlowBot;

public sealed partial class GroupFinderButtonHandler
{
    private async Task HandleEditTimeModalAsync(SocketModal modal, GroupFinderEditTimeModalState modalState)
    {
        var timeInput = modal.Data.Components
            .FirstOrDefault(component => component.CustomId == GroupFinderMessageBuilder.StartTimeInputId)
            ?.Value;

        if (!_timeParser.TryParse(timeInput, out var startsAtUnixTimeSeconds, out var errorMessage)
            || startsAtUnixTimeSeconds is null)
        {
            await modal.RespondAsync(
                string.IsNullOrWhiteSpace(errorMessage)
                    ? "Please enter a start time."
                    : errorMessage,
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
                    await modal.FollowupAsync("Only the group creator can edit the start time.", ephemeral: true);
                    return;
                }

                if (session.SessionStarted)
                {
                    await modal.FollowupAsync("This session has already been started.", ephemeral: true);
                    return;
                }

                await UpdateGroupMessageAsync(
                    userMessage,
                    session with { StartsAtUnixTimeSeconds = startsAtUnixTimeSeconds });
            }

            await modal.FollowupAsync("Start time updated.", ephemeral: true);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to edit start time for group finder message {MessageId}.", modalState.MessageId);
            await modal.FollowupAsync("I could not update the start time.", ephemeral: true);
        }
    }

    private async Task EditTimeAsync(SocketMessageComponent component, GroupFinderSession session)
    {
        if (component.User.Id != session.HostUserId)
        {
            await component.RespondAsync("Only the group creator can edit the start time.", ephemeral: true);
            return;
        }

        if (session.SessionStarted)
        {
            await component.RespondAsync("This session has already been started.", ephemeral: true);
            return;
        }

        var modal = new ModalBuilder()
            .WithTitle("Edit start time")
            .WithCustomId(GroupFinderButtonIds.CreateEditTimeModalId(
                component.Message.Id,
                session.Capacity,
                session.CapacityNoticeSent,
                session.SessionStarted))
            .AddTextInput(
                label: "Start time",
                customId: GroupFinderMessageBuilder.StartTimeInputId,
                style: TextInputStyle.Short,
                placeholder: "20:00, 17.00, tomorrow 20:00, or 2026-04-28 20:00",
                minLength: 1,
                maxLength: 64,
                required: true)
            .Build();

        await component.RespondWithModalAsync(modal);
    }

    private async Task ScrambleTeamsAsync(SocketMessageComponent component)
    {
        await component.DeferAsync(ephemeral: true);

        try
        {
            using (await _messageMutationLock.AcquireAsync(component.Message.Id))
            {
                var current = await LoadCurrentSessionAsync(component.Channel, component.Message.Id);
                if (current is null)
                {
                    await component.FollowupAsync("That group message no longer exists.", ephemeral: true);
                    return;
                }

                var (userMessage, session) = current.Value;

                if (component.User.Id != session.HostUserId)
                {
                    await component.FollowupAsync("Only the group creator can scramble teams.", ephemeral: true);
                    return;
                }

                if (session.PlayerIds.Count < 2)
                {
                    await component.FollowupAsync("Team scrambling needs at least two registered players.", ephemeral: true);
                    return;
                }

                var teams = _teamScrambler.CreateTeams(session.PlayerIds);
                await UpdateGroupMessageAsync(userMessage, session with { TeamIds = teams });
            }

            await component.FollowupAsync("Teams scrambled.", ephemeral: true);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to scramble teams for group finder message {MessageId}.",
                component.Message.Id);
            await component.FollowupAsync("I could not scramble teams for this group.", ephemeral: true);
        }
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

        await UpdateEphemeralResponseAsync(component, "Starting session...");

        GroupFinderSession startedSession;
        ulong groupMessageId;

        try
        {
            using (await _messageMutationLock.AcquireAsync(confirmation.MessageId))
            {
                var current = await LoadCurrentSessionAsync(component.Channel, confirmation.MessageId);
                if (current is null)
                {
                    await component.FollowupAsync("That group message no longer exists.", ephemeral: true);
                    return;
                }

                var (userMessage, session) = current.Value;

                if (session.HostUserId != component.User.Id)
                {
                    await component.FollowupAsync("Only the group creator can start this session.", ephemeral: true);
                    return;
                }

                if (session.SessionStarted)
                {
                    await component.FollowupAsync("This session has already been started.", ephemeral: true);
                    return;
                }

                startedSession = session with
                {
                    SessionStarted = true,
                    ReadyStates = new Dictionary<ulong, GroupFinderReadyState>(),
                };
                groupMessageId = userMessage.Id;

                await UpdateGroupMessageAsync(userMessage, startedSession);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to start group finder session for message {MessageId}.",
                confirmation.MessageId);
            await component.FollowupAsync("I could not start this session.", ephemeral: true);
            return;
        }

        try
        {
            await _notificationService.SendSessionStartedAsync(
                component.Channel,
                startedSession,
                groupMessageId);
            await component.FollowupAsync(
                "Session started. Registered players have been notified.",
                ephemeral: true);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Started group finder session {MessageId}, but failed to notify its players.",
                confirmation.MessageId);
            await component.FollowupAsync(
                "Session started, but I could not send the player notification.",
                ephemeral: true);
        }
    }
}