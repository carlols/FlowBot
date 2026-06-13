using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlowBot;

public sealed class SevenTvEmojiService(HttpClient httpClient, ILogger<SevenTvEmojiService> logger)
{
    private const string ApiBaseUrl = "https://7tv.io/v3/emotes/";

    public async Task<SevenTvEmojiAsset?> GetEmojiAsync(string emoteId)
    {
        try
        {
            var response = await httpClient.GetFromJsonAsync<SevenTvEmoteResponse>($"{ApiBaseUrl}{emoteId}");
            if (response is null)
            {
                return null;
            }

            var file = SelectBestFile(response);
            if (file is null || string.IsNullOrWhiteSpace(response.Host.Url))
            {
                return null;
            }

            return new SevenTvEmojiAsset(
                response.Id,
                BuildDefaultEmojiName(response.Name),
                response.Animated,
                BuildCdnUrl(response.Host.Url, file.Name),
                file.ConvertToPngBeforeUpload);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or NotSupportedException)
        {
            logger.LogWarning(exception, "Failed to fetch 7TV emote {EmoteId}.", emoteId);
            return null;
        }
    }

    private static SevenTvSelectedFile? SelectBestFile(SevenTvEmoteResponse response)
    {
        var files = response.Host.Files;

        if (response.Animated)
        {
            return files
                .Where(file => string.Equals(file.Format, "GIF", StringComparison.OrdinalIgnoreCase))
                .Where(file => file.Size <= EmojiImageOptimizer.DiscordEmojiSizeLimitBytes)
                .OrderByDescending(file => file.Width * file.Height)
                .ThenByDescending(file => file.Size)
                .Select(file => new SevenTvSelectedFile(file.Name, ConvertToPngBeforeUpload: false))
                .FirstOrDefault();
        }

        var pngFile = files
            .Where(file => string.Equals(file.Format, "PNG", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(file => file.Width * file.Height)
            .ThenByDescending(file => file.Size)
            .Select(file => new SevenTvSelectedFile(file.Name, ConvertToPngBeforeUpload: false))
            .FirstOrDefault();

        if (pngFile is not null)
        {
            return pngFile;
        }

        return files
            .Where(file => string.Equals(file.Format, "WEBP", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(file => file.Width * file.Height)
            .ThenByDescending(file => file.Size)
            .Select(file => new SevenTvSelectedFile(file.Name, ConvertToPngBeforeUpload: true))
            .FirstOrDefault();
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

    private sealed record SevenTvSelectedFile(string Name, bool ConvertToPngBeforeUpload);

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
