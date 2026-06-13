using System.Text.RegularExpressions;

namespace FlowBot;

public static partial class SevenTvEmojiLinkParser
{
    public static bool TryParseEmoteId(string value, out string emoteId)
    {
        emoteId = string.Empty;

        var match = SevenTvEmoteIdRegex().Match(value.Trim());
        if (!match.Success)
        {
            return false;
        }

        emoteId = match.Groups["id"].Value.ToUpperInvariant();
        return true;
    }

    public static bool IsValidEmoteId(string value) =>
        SevenTvEmoteIdOnlyRegex().IsMatch(value);

    [GeneratedRegex(@"(?:7tv\.app/emotes/|cdn\.7tv\.app/emote/)?(?<id>[0-9A-HJKMNP-TV-Z]{26})", RegexOptions.IgnoreCase)]
    private static partial Regex SevenTvEmoteIdRegex();

    [GeneratedRegex("^[0-9A-HJKMNP-TV-Z]{26}$", RegexOptions.IgnoreCase)]
    private static partial Regex SevenTvEmoteIdOnlyRegex();
}
