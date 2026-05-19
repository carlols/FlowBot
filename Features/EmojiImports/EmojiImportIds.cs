namespace FlowBot;

public static class EmojiImportIds
{
    public const string EmojiNameInputId = "emoji-name";
    public const string EmojiSelectId = "flowbot-emoji-select";

    private const string ModalPrefix = "flowbot-emoji-import:";

    public static string CreateModalId(EmojiImportCandidate emoji) =>
        $"{ModalPrefix}{emoji.Id}:{(emoji.IsAnimated ? "a" : "s")}";

    public static string CreateSelectValue(EmojiImportCandidate emoji) =>
        $"{emoji.Id}:{(emoji.IsAnimated ? "a" : "s")}:{emoji.Name}";

    public static bool IsEmojiImportInteraction(string customId) =>
        customId == EmojiSelectId
        || customId.StartsWith(ModalPrefix, StringComparison.Ordinal);

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
        state = new EmojiImportModalState(0, false);

        if (!customId.StartsWith(ModalPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var values = customId[ModalPrefix.Length..].Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (values.Length != 2 || !ulong.TryParse(values[0], out var emojiId))
        {
            return false;
        }

        if (values[1] is not ("a" or "s"))
        {
            return false;
        }

        state = new EmojiImportModalState(emojiId, values[1] == "a");
        return true;
    }
}
