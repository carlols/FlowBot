namespace FlowBot;

public static class EmojiImportIds
{
    public const string EmojiNameInputId = "emoji-name";
    public const string EmojiSelectId = "flowbot-emoji-select";

    private const string DiscordModalPrefix = "flowbot-emoji-import:";
    private const string SevenTvModalPrefix = "flowbot-7tv-import:";

    public static string CreateModalId(EmojiImportCandidate emoji) =>
        $"{DiscordModalPrefix}{emoji.Id}:{(emoji.IsAnimated ? "a" : "s")}";

    public static string CreateSevenTvModalId(string emoteId, bool isAnimated) =>
        $"{SevenTvModalPrefix}{emoteId}:{(isAnimated ? "a" : "s")}";

    public static string CreateSelectValue(EmojiImportCandidate emoji) =>
        $"{emoji.Id}:{(emoji.IsAnimated ? "a" : "s")}:{emoji.Name}";

    public static bool IsEmojiImportInteraction(string customId) =>
        customId == EmojiSelectId
        || customId.StartsWith(DiscordModalPrefix, StringComparison.Ordinal)
        || customId.StartsWith(SevenTvModalPrefix, StringComparison.Ordinal);

    public static bool TryParseSelectValue(string value, out EmojiImportCandidate emoji)
    {
        emoji = new EmojiImportCandidate(0, string.Empty, false);

        var values = value.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (values.Length != 3 || !ulong.TryParse(values[0], out var emojiId))
        {
            return false;
        }

        if (values[1] is not ("a" or "s") || !EmojiImportName.IsValid(values[2]))
        {
            return false;
        }

        emoji = new EmojiImportCandidate(emojiId, values[2], values[1] == "a");
        return true;
    }

    public static bool TryParseModal(string customId, out EmojiImportModalState state)
    {
        state = new EmojiImportModalState(string.Empty, false, EmojiImportSource.Discord);

        if (customId.StartsWith(DiscordModalPrefix, StringComparison.Ordinal))
        {
            var values = customId[DiscordModalPrefix.Length..].Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (values.Length != 2 || !ulong.TryParse(values[0], out var emojiId))
            {
                return false;
            }

            if (values[1] is not ("a" or "s"))
            {
                return false;
            }

            state = new EmojiImportModalState(emojiId.ToString(), values[1] == "a", EmojiImportSource.Discord);
            return true;
        }

        if (customId.StartsWith(SevenTvModalPrefix, StringComparison.Ordinal))
        {
            var values = customId[SevenTvModalPrefix.Length..].Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (values.Length != 2 || !SevenTvEmojiLinkParser.IsValidEmoteId(values[0]))
            {
                return false;
            }

            if (values[1] is not ("a" or "s"))
            {
                return false;
            }

            state = new EmojiImportModalState(values[0], values[1] == "a", EmojiImportSource.SevenTv);
            return true;
        }

        return false;
    }
}
