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

        try
        {
            var originalMessage = await modal.Channel.GetMessageAsync(modalState.MessageId);

            if (originalMessage is not IUserMessage userMessage)
            {
                await modal.RespondAsync("That group message no longer exists.", ephemeral: true);
                return;
            }

            if (!GroupFinderMessageParser.TryReadSession(
                    userMessage,
                    modalState.Capacity,
                    modalState.CapacityNoticeSent,
                    modalState.SessionStarted,
                    out var session)
                || session.HostUserId != modal.User.Id)
            {
                await modal.RespondAsync("Only the group creator can edit the start time.", ephemeral: true);
                return;
            }

            if (session.SessionStarted)
            {
                await modal.RespondAsync("This session has already been started.", ephemeral: true);
                return;
            }

            var updatedSession = session with { StartsAtUnixTimeSeconds = startsAtUnixTimeSeconds };

            await userMessage.ModifyAsync(properties =>
            {
                properties.Embed = GroupFinderMessageBuilder.BuildEmbed(updatedSession);
                properties.Components = GroupFinderMessageBuilder.BuildComponents(updatedSession);
            });

            await modal.RespondAsync("Start time updated.", ephemeral: true);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to edit start time for group finder message {MessageId}.", modalState.MessageId);
            await modal.RespondAsync("I could not update the start time.", ephemeral: true);
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

    private async Task ScrambleTeamsAsync(SocketMessageComponent component, GroupFinderSession session)
    {
        if (component.User.Id != session.HostUserId)
        {
            await component.RespondAsync("Only the group creator can scramble teams.", ephemeral: true);
            return;
        }

        if (session.PlayerIds.Count < 2)
        {
            await component.RespondAsync("Team scrambling needs at least two registered players.", ephemeral: true);
            return;
        }

        var teams = _teamScrambler.CreateTeams(session.PlayerIds);

        await UpdateGroupMessageAsync(component, session with { TeamIds = teams });
        await component.FollowupAsync("Teams scrambled.", ephemeral: true);
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

            if (!GroupFinderMessageParser.TryReadSession(
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
                properties.Components = GroupFinderMessageBuilder.BuildComponents(updatedSession);
            });

            await _notificationService.SendSessionStartedAsync(component.Channel, updatedSession, userMessage.Id);

            await UpdateEphemeralResponseAsync(component, "Session started. Registered players have been notified.");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to start group finder session for message {MessageId}.",
                confirmation.MessageId);
            await UpdateEphemeralResponseAsync(component, "I could not start this session.");
        }
    }

}