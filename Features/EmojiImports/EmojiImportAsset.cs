namespace FlowBot;

public sealed record EmojiImportAsset(
    string LogId,
    bool IsAnimated,
    string CdnUrl,
    bool ConvertToPngBeforeUpload);