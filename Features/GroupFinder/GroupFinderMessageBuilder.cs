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

        if (session.TeamIds.Count == 0)
        {
            AddPlayerFields(embed, session);
        }

        AddTeamFields(embed, session);

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

    private static void AddTeamFields(EmbedBuilder embed, GroupFinderSession session)
    {
        for (var index = 0; index < session.TeamIds.Count; index++)
        {
            if (session.TeamIds[index].Count == 0)
            {
                continue;
            }

            AddChunkedFields(
                embed,
                $"{TeamFieldPrefix}{index + 1}",
                FormatPlayerLines(session.TeamIds[index], session.ReadyStates),
                inline: true);
        }
    }

    private static void AddPlayerFields(EmbedBuilder embed, GroupFinderSession session)
    {
        if (session.PlayerIds.Count == 0)
        {
            embed.AddField(PlayersFieldName, "No players yet.");
            return;
        }

        AddChunkedFields(embed, PlayersFieldName, FormatPlayerLines(session));
    }

    private static void AddChunkedFields(
        EmbedBuilder embed,
        string fieldName,
        IEnumerable<string> lines,
        bool inline = false)
    {
        var chunks = new List<string>();
        var currentChunk = new List<string>();
        var currentLength = 0;

        foreach (var line in lines)
        {
            var addedLength = line.Length + (currentChunk.Count == 0 ? 0 : Environment.NewLine.Length);

            if (currentChunk.Count > 0 && currentLength + addedLength > EmbedFieldBuilder.MaxFieldValueLength)
            {
                chunks.Add(string.Join(Environment.NewLine, currentChunk));
                currentChunk.Clear();
                currentLength = 0;
                addedLength = line.Length;
            }

            currentChunk.Add(line);
            currentLength += addedLength;
        }

        if (currentChunk.Count > 0)
        {
            chunks.Add(string.Join(Environment.NewLine, currentChunk));
        }

        for (var index = 0; index < chunks.Count; index++)
        {
            var chunkFieldName = index == 0
                ? fieldName
                : $"{fieldName} ({index + 1})";

            embed.AddField(chunkFieldName, chunks[index], inline);
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

    private static IEnumerable<string> FormatPlayerLines(GroupFinderSession session) =>
        FormatPlayerLines(session.PlayerIds, session.ReadyStates);

    private static IEnumerable<string> FormatPlayerLines(
        IReadOnlyList<ulong> playerIds,
        IReadOnlyDictionary<ulong, GroupFinderReadyState> readyStates) =>
        playerIds.Select((playerId, index) =>
        {
            var row = $"{index + 1}. <@{playerId}>";

            return readyStates.TryGetValue(playerId, out var state)
                ? $"{row} - {FormatReadyState(state)}"
                : row;
        });

    private static string FormatReadyState(GroupFinderReadyState state) =>
        state switch
        {
            GroupFinderReadyState.Ready => "✅ Ready",
            GroupFinderReadyState.NotReady => "❌ Not ready",
            _ => "⏳ Waiting",
        };

}
