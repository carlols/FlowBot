using Discord;
using Discord.WebSocket;

namespace FlowBot;

public sealed partial class GroupFinderButtonHandler
{
    private readonly GroupFinderNotificationService _notificationService;
    private readonly GroupFinderRelatedMessageCleaner _relatedMessageCleaner;
    private readonly GroupFinderTimeParser _timeParser;
    private readonly GroupFinderTeamScrambler _teamScrambler;
    private readonly VoiceMemberMover _voiceMemberMover;
    private readonly ILogger<GroupFinderButtonHandler> _logger;

    public GroupFinderButtonHandler(
        GroupFinderNotificationService notificationService,
        GroupFinderRelatedMessageCleaner relatedMessageCleaner,
        GroupFinderTimeParser timeParser,
        GroupFinderTeamScrambler teamScrambler,
        VoiceMemberMover voiceMemberMover,
        ILogger<GroupFinderButtonHandler> logger)
    {
        _notificationService = notificationService;
        _relatedMessageCleaner = relatedMessageCleaner;
        _timeParser = timeParser;
        _teamScrambler = teamScrambler;
        _voiceMemberMover = voiceMemberMover;
        _logger = logger;
    }

    public async Task HandleAsync(SocketMessageComponent component)
    {
        if (GroupFinderButtonIds.TryParseVoiceChannelSelect(component.Data.CustomId, out var groupMessageId))
        {
            await HandleVoiceChannelSelectionAsync(component, groupMessageId);
            return;
        }

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

        if (!GroupFinderMessageParser.TryReadSession(
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

        if (buttonState.Action == GroupFinderButtonAction.EditTime)
        {
            await EditTimeAsync(component, session);
            return;
        }

        if (buttonState.Action == GroupFinderButtonAction.ScrambleTeams)
        {
            await ScrambleTeamsAsync(component, session);
            return;
        }

        if (buttonState.Action == GroupFinderButtonAction.MovePlayers)
        {
            await MovePlayersAsync(component, session);
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
            var updatedSession = session with { PlayerIds = playerIds, TeamIds = [] };

            if (updatedSession.IsFull && !session.CapacityNoticeSent)
            {
                updatedSession = updatedSession with { CapacityNoticeSent = true };
                await UpdateGroupMessageAsync(component, updatedSession);
                await _notificationService.SendCapacityNoticeAsync(component, updatedSession);
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
        await UpdateGroupMessageAsync(component, session with { PlayerIds = playerIds, ReadyStates = readyStates, TeamIds = [] });
        await component.FollowupAsync("You left the group.", ephemeral: true);
    }

    public async Task HandleModalAsync(SocketModal modal)
    {
        if (GroupFinderButtonIds.TryParseEditTimeModal(modal.Data.CustomId, out var editTimeState))
        {
            await HandleEditTimeModalAsync(modal, editTimeState);
            return;
        }

        await HandleReadyCheckModalAsync(modal);
    }

    private async Task UpdateGroupMessageAsync(SocketMessageComponent component, GroupFinderSession session)
    {
        try
        {
            await component.UpdateAsync(properties =>
            {
                properties.Embed = GroupFinderMessageBuilder.BuildEmbed(session);
                properties.Components = GroupFinderMessageBuilder.BuildComponents(session);
            });
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to update group finder message {MessageId}.", component.Message.Id);
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