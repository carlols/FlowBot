using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlowBot;

public sealed class SevenTvEmojiService(HttpClient httpClient, ILogger<SevenTvEmojiService> logger)
{
    private const string ApiBaseUrl = "https://7tv.io/v3/emotes/";

    public async Task<SevenTvEmojiLookupResult> GetEmojiAsync(string emoteId)
    {
        try
        {
            var response = await httpClient.GetFromJsonAsync<SevenTvEmoteResponse>($"{ApiBaseUrl}{emoteId}");
            if (response is null)
            {
                return SevenTvEmojiLookupResult.Failed("I could not find that 7TV emote.");
            }

            var file = SelectBestFile(response);
            if (file is null)
            {
                return SevenTvEmojiLookupResult.Failed(BuildMissingAssetMessage(response));
            }

            if (string.IsNullOrWhiteSpace(response.Host.Url))
            {
                return SevenTvEmojiLookupResult.Failed("7TV did not provide a download location for that emote.");
            }

            if (response.Animated && string.Equals(file.Format, "WEBP", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation(
                    "Using animated WebP fallback {FileName} ({FileSize} bytes) for 7TV emote {EmoteId}.",
                    file.Name,
                    file.Size,
                    emoteId);
            }

            return SevenTvEmojiLookupResult.Found(new SevenTvEmojiAsset(
                response.Id,
                BuildDefaultEmojiName(response.Name),
                response.Animated,
                BuildCdnUrl(response.Host.Url, file.Name),
                file.ConvertToPngBeforeUpload));
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogInformation("7TV emote {EmoteId} was not found.", emoteId);
            return SevenTvEmojiLookupResult.Failed("I could not find that 7TV emote.");
        }
        catch (Exception exception) when (
            exception is HttpRequestException or JsonException or NotSupportedException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Failed to fetch 7TV emote {EmoteId}.", emoteId);
            return SevenTvEmojiLookupResult.Failed("I could not retrieve that 7TV emote right now.");
        }
    }

    private static SevenTvSelectedFile? SelectBestFile(SevenTvEmoteResponse response)
    {
        var files = response.Host.Files;

        if (response.Animated)
        {
            return SelectBestSizedFile(files, "GIF", convertToPngBeforeUpload: false)
                ?? SelectBestSizedFile(files, "WEBP", convertToPngBeforeUpload: false);
        }

        var pngFile = files
            .Where(file => string.Equals(file.Format, "PNG", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(file => file.Width * file.Height)
            .ThenByDescending(file => file.Size)
            .Select(file => new SevenTvSelectedFile(
                file.Name,
                file.Format,
                file.Size,
                ConvertToPngBeforeUpload: false))
            .FirstOrDefault();

        if (pngFile is not null)
        {
            return pngFile;
        }

        return files
            .Where(file => string.Equals(file.Format, "WEBP", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(file => file.Width * file.Height)
            .ThenByDescending(file => file.Size)
            .Select(file => new SevenTvSelectedFile(
                file.Name,
                file.Format,
                file.Size,
                ConvertToPngBeforeUpload: true))
            .FirstOrDefault();
    }

    private static SevenTvSelectedFile? SelectBestSizedFile(
        IReadOnlyList<SevenTvFile> files,
        string format,
        bool convertToPngBeforeUpload) =>
        files
            .Where(file => string.Equals(file.Format, format, StringComparison.OrdinalIgnoreCase))
            .Where(file => file.Size <= EmojiImageOptimizer.DiscordEmojiSizeLimitBytes)
            .OrderByDescending(file => file.Width * file.Height)
            .ThenByDescending(file => file.Size)
            .Select(file => new SevenTvSelectedFile(file.Name, file.Format, file.Size, convertToPngBeforeUpload))
            .FirstOrDefault();

    private static string BuildMissingAssetMessage(SevenTvEmoteResponse response)
    {
        var supportedFormats = response.Animated ? new[] { "GIF", "WEBP" } : new[] { "PNG", "WEBP" };
        var smallestFile = response.Host.Files
            .Where(file => supportedFormats.Contains(file.Format, StringComparer.OrdinalIgnoreCase))
            .MinBy(file => file.Size);

        if (smallestFile is null)
        {
            var formatList = string.Join(" or ", supportedFormats);
            return $"7TV does not provide a {formatList} file for that emote.";
        }

        var sizeInKilobytes = (int)Math.Ceiling(smallestFile.Size / 1024d);
        return $"The smallest compatible file 7TV provides is {sizeInKilobytes} KB, above Discord's 256 KB emoji limit.";
    }

    private static string BuildDefaultEmojiName(string name)
    {
        var normalizedName = EmojiImportName.Normalize(name);
        return EmojiImportName.IsValid(normalizedName) ? normalizedName : "seventv_emote";
    }

    private static string BuildCdnUrl(string hostUrl, string fileName)
    {
        var absoluteHostUrl = hostUrl.StartsWith("//", StringComparison.Ordinal)
            ? $"https:{hostUrl}"
            : hostUrl;

        return $"{absoluteHostUrl.TrimEnd('/')}/{fileName}";
    }

    private sealed record SevenTvSelectedFile(
        string Name,
        string Format,
        int Size,
        bool ConvertToPngBeforeUpload);

    private sealed record SevenTvEmoteResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("animated")] bool Animated,
        [property: JsonPropertyName("host")] SevenTvHost Host);

    private sealed record SevenTvHost(
        [property: JsonPropertyName("url")] string Url,
        [property: JsonPropertyName("files")] IReadOnlyList<SevenTvFile> Files);

    private sealed record SevenTvFile(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("width")] int Width,
        [property: JsonPropertyName("height")] int Height,
        [property: JsonPropertyName("size")] int Size,
        [property: JsonPropertyName("format")] string Format);
}
