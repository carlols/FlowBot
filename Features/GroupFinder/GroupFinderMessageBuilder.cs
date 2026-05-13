using System.Text.RegularExpressions;
using Discord;

namespace FlowBot;

public static partial class GroupFinderMessageBuilder
{
    public const int ReadyCheckMessageMaxLength = 200;
    public const string ReadyCheckMessageInputId = "flowbot-group-ready-message";
    public const string StatusFieldName = "Group";
    public const string HostFieldName = "Host";
    public const string StartsFieldName = "Starts";
    public const string NoticeFieldName = "Notice";
    public const string LegacyFullNotificationNotice = "Group filled and players notified.";
    public const string PlayersFieldName = "Players";

    public static Embed BuildEmbed(GroupFinderSession session)
    {
        var embed = new EmbedBuilder()
            .WithTitle(session.GameName)
            .WithDescription(session.Description ?? "Looking for players.")
            .AddField(StatusFieldName, FormatStatus(session), inline: true)
            .AddField(HostFieldName, $"<@{session.HostUserId}>", inline: true)
            .WithColor(new Color(87, 242, 135))
            .WithFooter("The group creator can start the session when everyone is ready.");

        if (session.StartsAtUnixTimeSeconds is { } startsAt)
        {
            embed.AddField(StartsFieldName, $"<t:{startsAt}:f> (<t:{startsAt}:R>)", inline: true);
        }

        if (session.HasActiveReadyCheck)
        {
            embed.AddField("Ready Check", "Active", inline: true);
        }

        embed.AddField(PlayersFieldName, FormatPlayers(session));

        return embed.Build();
    }

    public static MessageComponent BuildComponents(
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
            .Build();
    }

    public static bool TryReadSession(
        IMessage message,
        int? capacity,
        bool? capacityNoticeSentFromComponents,
        bool? sessionStartedFromComponents,
        out GroupFinderSession session)
    {
        session = new GroupFinderSession("Unknown game", null, capacity, 0, null, false, false, [], new Dictionary<ulong, GroupFinderReadyState>());

        var embed = message.Embeds.FirstOrDefault();

        if (embed is null)
        {
            return false;
        }

        var hostField = embed.Fields.FirstOrDefault(field => field.Name == HostFieldName);
        var startsField = embed.Fields.FirstOrDefault(field => field.Name == StartsFieldName);
        var noticeField = embed.Fields.FirstOrDefault(field => field.Name == NoticeFieldName);
        var playersField = embed.Fields.FirstOrDefault(field => field.Name == PlayersFieldName);
        var hostMatch = PlayerMentionRegex().Match(hostField.Value ?? string.Empty);

        if (!hostMatch.Success)
        {
            return false;
        }

        var hostUserId = ulong.Parse(hostMatch.Groups["id"].Value);
        var startsAtUnixTimeSeconds = TryReadTimestamp(startsField.Value);
        var capacityNoticeSent = capacityNoticeSentFromComponents
            ?? noticeField.Value == LegacyFullNotificationNotice;
        var sessionStarted = sessionStartedFromComponents
            ?? noticeField.Value == LegacyFullNotificationNotice;
        var playerMatches = PlayerLineRegex()
            .Matches(playersField.Value ?? string.Empty)
            .ToArray();
        var playerIds = playerMatches
            .Select(match => ulong.Parse(match.Groups["id"].Value))
            .Distinct()
            .ToArray();
        var readyStates = playerMatches
            .Where(match => TryParseReadyState(match.Groups["state"].Value, out _))
            .Select(match =>
            {
                var userId = ulong.Parse(match.Groups["id"].Value);
                _ = TryParseReadyState(match.Groups["state"].Value, out var readyState);
                return new KeyValuePair<ulong, GroupFinderReadyState>(userId, readyState);
            })
            .DistinctBy(pair => pair.Key)
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        session = new GroupFinderSession(
            embed.Title,
            embed.Description,
            capacity,
            hostUserId,
            startsAtUnixTimeSeconds,
            capacityNoticeSent,
            sessionStarted,
            playerIds,
            readyStates);
        return true;
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

    private static long? TryReadTimestamp(string? value)
    {
        var match = TimestampRegex().Match(value ?? string.Empty);

        return match.Success
            ? long.Parse(match.Groups["timestamp"].Value)
            : null;
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

    private static string FormatReadyState(GroupFinderReadyState state) =>
        state switch
        {
            GroupFinderReadyState.Ready => "ready",
            GroupFinderReadyState.NotReady => "not ready",
            _ => "waiting",
        };

    private static bool TryParseReadyState(string value, out GroupFinderReadyState state)
    {
        state = value switch
        {
            "ready" => GroupFinderReadyState.Ready,
            "not ready" => GroupFinderReadyState.NotReady,
            "waiting" => GroupFinderReadyState.Waiting,
            _ => GroupFinderReadyState.Waiting,
        };

        return value is "ready" or "not ready" or "waiting";
    }

    [GeneratedRegex("<@!?(?<id>\\d+)>")]
    private static partial Regex PlayerMentionRegex();

    [GeneratedRegex(@"^\d+\.\s+<@!?(?<id>\d+)>(?:\s+-\s+(?<state>ready|not ready|waiting))?$", RegexOptions.Multiline)]
    private static partial Regex PlayerLineRegex();

    [GeneratedRegex("<t:(?<timestamp>\\d+):[a-zA-Z]>")]
    private static partial Regex TimestampRegex();
}
