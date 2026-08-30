namespace FlowBot;

public sealed record SevenTvEmojiLookupResult(SevenTvEmojiAsset? Asset, string? ErrorMessage)
{
    public static SevenTvEmojiLookupResult Found(SevenTvEmojiAsset asset) =>
        new(asset, ErrorMessage: null);

    public static SevenTvEmojiLookupResult Failed(string errorMessage) =>
        new(Asset: null, errorMessage);
}
