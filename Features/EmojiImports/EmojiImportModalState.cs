namespace FlowBot;

public sealed record EmojiImportModalState(string SourceId, bool IsAnimated, EmojiImportSource Source)
{
    public string LogId => Source == EmojiImportSource.Discord
        ? $"Discord emoji {SourceId}"
        : $"7TV emote {SourceId}";
}
