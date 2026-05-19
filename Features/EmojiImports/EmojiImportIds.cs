namespace FlowBot;

public static class EmojiImportIds
{
    public const string EmojiNameInputId = "emoji-name";

    private const string ModalPrefix = "flowbot-emoji-import:";

    public static string CreateModalId(EmojiImportCandidate emoji) =>
        $"{ModalPrefix}{emoji.Id}:{(emoji.IsAnimated ? "a" : "s")}";

    public static bool IsEmojiImportInteraction(string customId) =>
        customId.StartsWith(ModalPrefix, StringComparison.Ordinal);

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
