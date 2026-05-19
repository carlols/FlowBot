using System.Text.RegularExpressions;
using Discord;

namespace FlowBot;

public static partial class EmojiImportParser
{
    public static IReadOnlyList<EmojiImportCandidate> FindCustomEmojis(IMessage message) =>
        CustomEmojiRegex()
            .Matches(message.Content)
            .Select(match => new EmojiImportCandidate(
                ulong.Parse(match.Groups["id"].Value),
                match.Groups["name"].Value,
                match.Groups["animated"].Value == "a"))
            .DistinctBy(emoji => emoji.Id)
            .ToArray();

    [GeneratedRegex("<(?<animated>a?):(?<name>[A-Za-z0-9_]{2,32}):(?<id>[0-9]{17,20})>")]
    private static partial Regex CustomEmojiRegex();
}
