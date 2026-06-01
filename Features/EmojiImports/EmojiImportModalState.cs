namespace FlowBot;

public sealed record EmojiImportModalState(ulong EmojiId, bool IsAnimated)
{
    public string CdnUrl => $"https://cdn.discordapp.com/emojis/{EmojiId}.{(IsAnimated ? "gif" : "png")}";
}
