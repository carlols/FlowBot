using Discord;
using Discord.WebSocket;

namespace FlowBot;

public sealed partial class GroupFinderButtonHandler
{
    private readonly DiscordMessageMutationLock _messageMutationLock;
    private readonly GroupFinderNotificationService _notificationService;
    private readonly GroupFinderRelatedMessageCleaner _relatedMessageCleaner;
    private readonly GroupFinderTimeParser _timeParser;
    private readonly GroupFinderTeamScrambler _teamScrambler;
    private readonly VoiceMemberMover _voiceMemberMover;
    private readonly ILogger<GroupFinderButtonHandler> _logger;

    public GroupFinderButtonHandler(
        DiscordMessageMutationLock messageMutationLock,
        GroupFinderNotificationService notificationService,
        GroupFinderRelatedMessageCleaner relatedMessageCleaner,
        GroupFinderTimeParser timeParser,
        GroupFinderTeamScrambler teamScrambler,
        VoiceMemberMover voiceMemberMover,
        ILogger<GroupFinderButtonHandler> logger)
    {
        _messageMutationLock = messageMutationLock;
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

        if (buttonState.Action is GroupFinderButtonAction.Join or GroupFinderButtonAction.Leave)
        {
            await UpdateMembershipAsync(component, buttonState.Action);
            return;
        }

        if (buttonState.Action == GroupFinderButtonAction.ScrambleTeams)
        {
            await ScrambleTeamsAsync(component);
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

        switch (buttonState.Action)
        {
            case GroupFinderButtonAction.Close:
                await CloseGroupAsync(component, session);
                break;
            case GroupFinderButtonAction.Start:
                await StartSessionAsync(component, session);
                break;
            case GroupFinderButtonAction.ReadyCheck:
                await ReadyCheckAsync(component, session);
                break;
            case GroupFinderButtonAction.EditTime:
                await EditTimeAsync(component, session);
                break;
            case GroupFinderButtonAction.MovePlayers:
                await MovePlayersAsync(component, session);
                break;
            default:
                await component.RespondAsync("I could not identify this group finder action.", ephemeral: true);
                break;
        }
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

    private async Task UpdateMembershipAsync(
        SocketMessageComponent component,
        GroupFinderButtonAction action)
    {
        await component.DeferAsync(ephemeral: true);

        try
        {
            GroupFinderSession? capacityNoticeSession = null;
            string response;

            using (await _messageMutationLock.AcquireAsync(component.Message.Id))
            {
                var current = await LoadCurrentSessionAsync(component.Channel, component.Message.Id);
                if (current is null)
                {
                    await component.FollowupAsync("That group message no longer exists.", ephemeral: true);
                    return;
                }

                var (message, session) = current.Value;
                var playerIds = session.PlayerIds.ToList();
                var userId = component.User.Id;
                var isRegistered = playerIds.Contains(userId);

                if (action == GroupFinderButtonAction.Join)
                {
                    if (isRegistered)
                    {
                        await component.FollowupAsync("You are already in this group.", ephemeral: true);
                        return;
                    }

                    if (session.IsFull)
                    {
                        await component.FollowupAsync("This group is already full.", ephemeral: true);
                        return;
                    }

                    playerIds.Add(userId);
                    var updatedSession = session with { PlayerIds = playerIds, TeamIds = [] };

                    if (updatedSession.IsFull && !session.CapacityNoticeSent)
                    {
                        updatedSession = updatedSession with { CapacityNoticeSent = true };
                        capacityNoticeSession = updatedSession;
                    }

                    await UpdateGroupMessageAsync(message, updatedSession);
                    response = "You joined the group.";
                }
                else
                {
                    if (!isRegistered)
                    {
                        await component.FollowupAsync("You are not in this group.", ephemeral: true);
                        return;
                    }

                    playerIds.Remove(userId);
                    var readyStates = session.ReadyStates
                        .Where(pair => pair.Key != userId)
                        .ToDictionary(pair => pair.Key, pair => pair.Value);
                    var updatedSession = session with
                    {
                        PlayerIds = playerIds,
                        ReadyStates = readyStates,
                        TeamIds = [],
                    };

                    await UpdateGroupMessageAsync(message, updatedSession);
                    response = "You left the group.";
                }
            }

            if (capacityNoticeSession is not null)
            {
                await _notificationService.SendCapacityNoticeAsync(component, capacityNoticeSession);
            }

            await component.FollowupAsync(response, ephemeral: true);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to update membership for group finder message {MessageId}.",
                component.Message.Id);
            await component.FollowupAsync("I could not update this group message.", ephemeral: true);
        }
    }

    private static async Task<(IUserMessage Message, GroupFinderSession Session)?> LoadCurrentSessionAsync(
        IMessageChannel channel,
        ulong messageId)
    {
        var message = await channel.GetMessageAsync(messageId, CacheMode.AllowDownload);

        return message is IUserMessage userMessage
            && GroupFinderMessageParser.TryReadSession(userMessage, out var session)
                ? (userMessage, session)
                : null;
    }

    private static Task UpdateGroupMessageAsync(IUserMessage message, GroupFinderSession session) =>
        message.ModifyAsync(properties =>
        {
            properties.Embed = GroupFinderMessageBuilder.BuildEmbed(session);
            properties.Components = GroupFinderMessageBuilder.BuildComponents(session);
        });

    private static Task UpdateEphemeralResponseAsync(SocketMessageComponent component, string content) =>
        component.UpdateAsync(properties =>
        {
            properties.Content = content;
            properties.Components = new ComponentBuilder().Build();
        });
}