namespace FlowBot;

public sealed record EmojiImportCandidate(ulong Id, string Name, bool IsAnimated)
{
    public string CdnUrl => $"https://cdn.discordapp.com/emojis/{Id}.{(IsAnimated ? "gif" : "png")}?quality=lossless";
}
