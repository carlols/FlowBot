namespace FlowBot;

public sealed record SevenTvEmojiAsset(
    string Id,
    string Name,
    bool IsAnimated,
    string CdnUrl,
    bool ConvertToPngBeforeUpload);
