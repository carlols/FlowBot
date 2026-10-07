using System.Text.RegularExpressions;
using Discord;

namespace FlowBot;

public static partial class GroupFinderMessageParser
{
    public static bool TryReadSession(IUserMessage message, out GroupFinderSession session)
    {
        var buttonState = message.Components
            .OfType<ActionRowComponent>()
            .SelectMany(row => row.Components)
            .OfType<ButtonComponent>()
            .Select(button => button.CustomId ?? string.Empty)
            .Select(customId => GroupFinderButtonIds.TryParse(customId, out var state)
                ? state
                : null)
            .FirstOrDefault(state => state is not null);

        if (buttonState is null)
        {
            session = default!;
            return false;
        }

        return TryReadSession(
            message,
            buttonState.Capacity,
            buttonState.CapacityNoticeSent,
            buttonState.SessionStarted,
            out session);
    }

    public static bool TryReadSession(
        IMessage message,
        int? capacity,
        bool? capacityNoticeSentFromComponents,
        bool? sessionStartedFromComponents,
        out GroupFinderSession session)
    {
        session = new GroupFinderSession(
            "Unknown game",
            null,
            capacity,
            0,
            null,
            false,
            false,
            [],
            new Dictionary<ulong, GroupFinderReadyState>(),
            []);

        var embed = message.Embeds.FirstOrDefault();
        if (embed is null)
        {
            return false;
        }

        var hostValue = FindFieldValue(embed, GroupFinderMessageBuilder.HostFieldName);
        var hostMatch = PlayerMentionRegex().Match(hostValue);

        if (!hostMatch.Success
            || !ulong.TryParse(hostMatch.Groups["id"].Value, out var hostUserId))
        {
            return false;
        }

        var noticeValue = FindFieldValue(embed, GroupFinderMessageBuilder.NoticeFieldName);
        var playerFields = embed.Fields
            .Where(field => IsFieldOrContinuation(field.Name, GroupFinderMessageBuilder.PlayersFieldName))
            .ToArray();

        if (playerFields.Length == 0)
        {
            playerFields = embed.Fields
                .Where(field => ReadTeamNumber(field.Name) is not null)
                .ToArray();
        }

        var playerFieldValue = string.Join(
            Environment.NewLine,
            playerFields.Select(field => field.Value ?? string.Empty));
        var playerMatches = PlayerLineRegex().Matches(playerFieldValue);

        ReadPlayers(playerMatches, out var playerIds, out var readyStates);

        session = new GroupFinderSession(
            embed.Title,
            embed.Description,
            capacity,
            hostUserId,
            TryReadTimestamp(FindFieldValue(embed, GroupFinderMessageBuilder.StartsFieldName)),
            capacityNoticeSentFromComponents
                ?? noticeValue == GroupFinderMessageBuilder.LegacyFullNotificationNotice,
            sessionStartedFromComponents
                ?? noticeValue == GroupFinderMessageBuilder.LegacyFullNotificationNotice,
            playerIds,
            readyStates,
            ReadTeams(embed.Fields, playerIds.ToHashSet()));
        return true;
    }

    private static string FindFieldValue(IEmbed embed, string fieldName) =>
        embed.Fields.FirstOrDefault(field => field.Name == fieldName).Value
        ?? string.Empty;

    private static void ReadPlayers(
        IEnumerable<Match> matches,
        out IReadOnlyList<ulong> playerIds,
        out IReadOnlyDictionary<ulong, GroupFinderReadyState> readyStates)
    {
        var players = new List<ulong>();
        var seenPlayerIds = new HashSet<ulong>();
        var states = new Dictionary<ulong, GroupFinderReadyState>();

        foreach (var match in matches)
        {
            if (!ulong.TryParse(match.Groups["id"].Value, out var playerId)
                || !seenPlayerIds.Add(playerId))
            {
                continue;
            }

            players.Add(playerId);

            if (TryParseReadyState(match.Groups["state"].Value, out var readyState))
            {
                states[playerId] = readyState;
            }
        }

        playerIds = players;
        readyStates = states;
    }

    private static IReadOnlyList<IReadOnlyList<ulong>> ReadTeams(
        IEnumerable<EmbedField> fields,
        ISet<ulong> playerIds)
    {
        var assignedPlayerIds = new HashSet<ulong>();
        var teams = new List<IReadOnlyList<ulong>>();
        var teamFields = fields
            .Select(field => (Field: field, TeamNumber: ReadTeamNumber(field.Name)))
            .Where(item => item.TeamNumber is not null)
            .GroupBy(item => item.TeamNumber!.Value)
            .OrderBy(group => group.Key);

        foreach (var teamFieldGroup in teamFields)
        {
            var teamFieldValue = string.Join(
                Environment.NewLine,
                teamFieldGroup.Select(item => item.Field.Value ?? string.Empty));
            var teamPlayerIds = PlayerMentionRegex()
                .Matches(teamFieldValue)
                .Select(match => ulong.TryParse(match.Groups["id"].Value, out var playerId)
                    ? playerId
                    : 0)
                .Where(playerId =>
                    playerId != 0
                    && playerIds.Contains(playerId)
                    && assignedPlayerIds.Add(playerId))
                .ToArray();

            if (teamPlayerIds.Length > 0)
            {
                teams.Add(teamPlayerIds);
            }
        }

        return teams;
    }

    private static bool IsFieldOrContinuation(string fieldName, string baseFieldName) =>
        fieldName == baseFieldName
        || fieldName.StartsWith($"{baseFieldName} (", StringComparison.Ordinal);

    private static int? ReadTeamNumber(string fieldName)
    {
        var match = TeamFieldNameRegex().Match(fieldName);

        return match.Success && int.TryParse(match.Groups["number"].Value, out var teamNumber)
            ? teamNumber
            : null;
    }

    private static long? TryReadTimestamp(string value)
    {
        var match = TimestampRegex().Match(value);

        return match.Success
            && long.TryParse(match.Groups["timestamp"].Value, out var timestamp)
                ? timestamp
                : null;
    }

    private static bool TryParseReadyState(string value, out GroupFinderReadyState state)
    {
        var normalizedValue = value.ToLowerInvariant();

        state = normalizedValue switch
        {
            "ready" => GroupFinderReadyState.Ready,
            "not ready" => GroupFinderReadyState.NotReady,
            "waiting" => GroupFinderReadyState.Waiting,
            _ => GroupFinderReadyState.Waiting,
        };

        return normalizedValue is "ready" or "not ready" or "waiting";
    }

    [GeneratedRegex("<@!?(?<id>\\d+)>")]
    private static partial Regex PlayerMentionRegex();

    [GeneratedRegex(@"^\d+\.\s+<@!?(?<id>\d+)>(?:\s+-\s+(?:✅|❌|⏳)?\s*(?<state>ready|not ready|waiting))?$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex PlayerLineRegex();

    [GeneratedRegex("<t:(?<timestamp>\\d+):[a-zA-Z]>")]
    private static partial Regex TimestampRegex();

    [GeneratedRegex("^Team (?<number>\\d+)(?: \\(\\d+\\))?$")]
    private static partial Regex TeamFieldNameRegex();
}
