using System.Text.RegularExpressions;
using Discord;

namespace FlowBot;

public static partial class GroupFinderMessageBuilder
{
    public const string StatusFieldName = "Group";
    public const string HostFieldName = "Host";
    public const string StartsFieldName = "Starts";
    public const string PlayersFieldName = "Players";

    public static Embed BuildEmbed(GroupFinderSession session)
    {
        var embed = new EmbedBuilder()
            .WithTitle(session.GameName)
            .WithDescription(session.Description ?? "Looking for players.")
            .AddField(StatusFieldName, $"{session.PlayerIds.Count}/{session.Capacity} people in group", inline: true)
            .AddField(HostFieldName, $"<@{session.HostUserId}>", inline: true)
            .WithColor(new Color(87, 242, 135))
            .WithFooter("Use the buttons below to join, leave, or close this group.");

        if (session.StartsAtUnixTimeSeconds is { } startsAt)
        {
            embed.AddField(StartsFieldName, $"<t:{startsAt}:f> (<t:{startsAt}:R>)", inline: true);
        }

        embed.AddField(PlayersFieldName, FormatPlayers(session.PlayerIds));

        return embed.Build();
    }

    public static MessageComponent BuildComponents(int capacity, int playerCount)
    {
        return new ComponentBuilder()
            .WithButton(
                label: "Join group",
                customId: GroupFinderButtonIds.CreateJoinId(capacity),
                style: ButtonStyle.Success,
                disabled: playerCount >= capacity)
            .WithButton(
                label: "Leave group",
                customId: GroupFinderButtonIds.CreateLeaveId(capacity),
                style: ButtonStyle.Danger)
            .WithButton(
                label: "Close group",
                customId: GroupFinderButtonIds.CreateCloseId(capacity),
                style: ButtonStyle.Danger)
            .Build();
    }

    public static bool TryReadSession(IMessage message, int capacity, out GroupFinderSession session)
    {
        session = new GroupFinderSession("Unknown game", null, capacity, 0, null, []);

        var embed = message.Embeds.FirstOrDefault();

        if (embed is null)
        {
            return false;
        }

        var hostField = embed.Fields.FirstOrDefault(field => field.Name == HostFieldName);
        var startsField = embed.Fields.FirstOrDefault(field => field.Name == StartsFieldName);
        var playersField = embed.Fields.FirstOrDefault(field => field.Name == PlayersFieldName);
        var hostMatch = PlayerMentionRegex().Match(hostField.Value ?? string.Empty);

        if (!hostMatch.Success)
        {
            return false;
        }

        var hostUserId = ulong.Parse(hostMatch.Groups["id"].Value);
        var startsAtUnixTimeSeconds = TryReadTimestamp(startsField.Value);
        var playerIds = PlayerMentionRegex()
            .Matches(playersField.Value ?? string.Empty)
            .Select(match => ulong.Parse(match.Groups["id"].Value))
            .Distinct()
            .ToArray();

        session = new GroupFinderSession(embed.Title, embed.Description, capacity, hostUserId, startsAtUnixTimeSeconds, playerIds);
        return true;
    }

    private static long? TryReadTimestamp(string? value)
    {
        var match = TimestampRegex().Match(value ?? string.Empty);

        return match.Success
            ? long.Parse(match.Groups["timestamp"].Value)
            : null;
    }

    private static string FormatPlayers(IReadOnlyCollection<ulong> playerIds)
    {
        if (playerIds.Count == 0)
        {
            return "No players yet.";
        }

        return string.Join(Environment.NewLine, playerIds.Select((playerId, index) => $"{index + 1}. <@{playerId}>"));
    }

    [GeneratedRegex("<@!?(?<id>\\d+)>")]
    private static partial Regex PlayerMentionRegex();

    [GeneratedRegex("<t:(?<timestamp>\\d+):[a-zA-Z]>")]
    private static partial Regex TimestampRegex();
}
