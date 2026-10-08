using Discord;
using Discord.Net;

namespace FlowBot;

public sealed partial class EmojiImportHandler
{
    private static bool ShouldTryOptimization(HttpException exception) =>
        exception.DiscordCode == DiscordErrorCode.FailedToResizeAssetBelowTheMaximumSize;

    private static string BuildDiscordUploadFailureMessage(HttpException exception)
    {
        if (exception.DiscordCode == DiscordErrorCode.FailedToResizeAssetBelowTheMaximumSize)
        {
            return "Discord rejected that emoji because it could not resize the asset below 256 KB.";
        }

        return string.IsNullOrWhiteSpace(exception.Reason)
            ? "Discord rejected that emoji upload."
            : $"Discord rejected that emoji upload: {exception.Reason}";
    }
}
