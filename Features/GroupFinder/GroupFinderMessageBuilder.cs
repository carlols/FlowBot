using System.Text.RegularExpressions;
using Discord;

namespace FlowBot;

public static partial class GroupFinderMessageBuilder
{
    public const string StatusFieldName = "Group";
    public const string HostFieldName = "Host";
    public const string PlayersFieldName = "Players";

    public static Embed BuildEmbed(GroupFinderSession session)
    {
        return new EmbedBuilder()
            .WithTitle(session.GameName)
            .WithDescription(session.Description ?? "Looking for players.")
            .AddField(StatusFieldName, $"{session.PlayerIds.Count}/{session.Capacity} people in group", inline: true)
            .AddField(HostFieldName, $"<@{session.HostUserId}>", inline: true)
            .AddField(PlayersFieldName, FormatPlayers(session.PlayerIds))
            .WithColor(new Color(87, 242, 135))
            .WithFooter("Use the buttons below to join, leave, or close this group.")
            .Build();
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
        session = new GroupFinderSession("Unknown game", null, capacity, 0, []);

        var embed = message.Embeds.FirstOrDefault();

        if (embed is null)
        {
            return false;
        }

        var hostField = embed.Fields.FirstOrDefault(field => field.Name == HostFieldName);
        var playersField = embed.Fields.FirstOrDefault(field => field.Name == PlayersFieldName);
        var hostMatch = PlayerMentionRegex().Match(hostField.Value ?? string.Empty);

        if (!hostMatch.Success)
        {
            return false;
        }

        var hostUserId = ulong.Parse(hostMatch.Groups["id"].Value);
        var playerIds = PlayerMentionRegex()
            .Matches(playersField.Value ?? string.Empty)
            .Select(match => ulong.Parse(match.Groups["id"].Value))
            .Distinct()
            .ToArray();

        session = new GroupFinderSession(embed.Title, embed.Description, capacity, hostUserId, playerIds);
        return true;
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
}
