using Discord;

namespace FlowBot;

public static class GroupFinderMessageBuilder
{
    public const int ReadyCheckMessageMaxLength = 200;
    public const string ReadyCheckMessageInputId = "flowbot-group-ready-message";
    public const string StartTimeInputId = "flowbot-group-start-time";
    public const string StatusFieldName = "Group";
    public const string HostFieldName = "Host";
    public const string StartsFieldName = "Starts";
    public const string NoticeFieldName = "Notice";
    public const string LegacyFullNotificationNotice = "Group filled and players notified.";
    public const string PlayersFieldName = "Players";
    internal const string TeamFieldPrefix = "Team ";

    public static Embed BuildEmbed(GroupFinderSession session)
    {
        var embed = new EmbedBuilder()
            .WithTitle(session.GameName)
            .WithDescription(session.Description ?? "Looking for players.")
            .AddField(StatusFieldName, FormatStatus(session), inline: true)
            .AddField(HostFieldName, $"<@{session.HostUserId}>", inline: true)
            .WithColor(new Color(87, 242, 135))
            .WithFooter(session.SessionStarted
                ? "Session started by the group creator."
                : "The group creator can start the session when everyone is ready.");

        if (session.StartsAtUnixTimeSeconds is { } startsAt)
        {
            embed.AddField(StartsFieldName, $"<t:{startsAt}:f> (<t:{startsAt}:R>)", inline: true);
        }

        if (session.HasActiveReadyCheck)
        {
            embed.AddField("Ready Check", "Active", inline: true);
        }

        embed.AddField(PlayersFieldName, FormatPlayers(session));
        AddTeamFields(embed, session.TeamIds);

        return embed.Build();
    }

    public static MessageComponent BuildComponents(GroupFinderSession session) =>
        BuildComponents(
            session.Capacity,
            session.PlayerIds.Count,
            session.CapacityNoticeSent,
            session.SessionStarted);

    private static MessageComponent BuildComponents(
        int? capacity,
        int playerCount,
        bool capacityNoticeSent,
        bool sessionStarted)
    {
        return new ComponentBuilder()
            .WithButton(
                label: "Join",
                customId: GroupFinderButtonIds.CreateJoinId(capacity, capacityNoticeSent, sessionStarted),
                style: ButtonStyle.Success,
                disabled: capacity is { } maxPlayers && playerCount >= maxPlayers)
            .WithButton(
                label: "Leave",
                customId: GroupFinderButtonIds.CreateLeaveId(capacity, capacityNoticeSent, sessionStarted),
                style: ButtonStyle.Danger)
            .WithButton(
                label: "Ready Check",
                customId: GroupFinderButtonIds.CreateReadyCheckId(capacity, capacityNoticeSent, sessionStarted),
                style: ButtonStyle.Secondary,
                disabled: sessionStarted)
            .WithButton(
                label: "Start",
                customId: GroupFinderButtonIds.CreateStartId(capacity, capacityNoticeSent, sessionStarted),
                style: ButtonStyle.Primary,
                disabled: sessionStarted)
            .WithButton(
                label: "Close",
                customId: GroupFinderButtonIds.CreateCloseId(capacity, capacityNoticeSent, sessionStarted),
                style: ButtonStyle.Danger)
            .WithButton(
                label: "Scramble Teams",
                customId: GroupFinderButtonIds.CreateScrambleTeamsId(capacity, capacityNoticeSent, sessionStarted),
                style: ButtonStyle.Secondary,
                row: 1)
            .WithButton(
                label: "Edit Time",
                customId: GroupFinderButtonIds.CreateEditTimeId(capacity, capacityNoticeSent, sessionStarted),
                style: ButtonStyle.Secondary,
                disabled: sessionStarted,
                row: 1)
            .WithButton(
                label: "Move Players",
                customId: GroupFinderButtonIds.CreateMovePlayersId(capacity, capacityNoticeSent, sessionStarted),
                style: ButtonStyle.Secondary,
                row: 1)
            .Build();
    }

    private static void AddTeamFields(EmbedBuilder embed, IReadOnlyList<IReadOnlyList<ulong>> teamIds)
    {
        for (var index = 0; index < teamIds.Count; index++)
        {
            if (teamIds[index].Count == 0)
            {
                continue;
            }

            embed.AddField($"{TeamFieldPrefix}{index + 1}", FormatTeam(teamIds[index]), inline: true);
        }
    }

    private static string FormatStatus(GroupFinderSession session)
    {
        if (session.Capacity is { } capacity)
        {
            var status = $"{session.PlayerIds.Count}/{capacity} players joined";

            return session.IsFull
                ? $"{status} - full"
                : status;
        }

        return $"{session.PlayerIds.Count} people interested";
    }

    private static string FormatPlayers(GroupFinderSession session)
    {
        if (session.PlayerIds.Count == 0)
        {
            return "No players yet.";
        }

        return string.Join(
            Environment.NewLine,
            session.PlayerIds.Select((playerId, index) =>
            {
                var row = $"{index + 1}. <@{playerId}>";

                return session.ReadyStates.TryGetValue(playerId, out var state)
                    ? $"{row} - {FormatReadyState(state)}"
                    : row;
            }));
    }

    private static string FormatTeam(IReadOnlyList<ulong> playerIds) =>
        string.Join(
            Environment.NewLine,
            playerIds.Select((playerId, index) => $"{index + 1}. <@{playerId}>"));

    private static string FormatReadyState(GroupFinderReadyState state) =>
        state switch
        {
            GroupFinderReadyState.Ready => "ready",
            GroupFinderReadyState.NotReady => "not ready",
            _ => "waiting",
        };

}
