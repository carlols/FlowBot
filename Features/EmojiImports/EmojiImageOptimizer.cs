using ImageMagick;

namespace FlowBot;

public sealed class EmojiImageOptimizer(ILogger<EmojiImageOptimizer> logger)
{
    public const int DiscordEmojiSizeLimitBytes = 256 * 1024;

    private const int MaxOptimizableImageBytes = 2 * 1024 * 1024;
    private static readonly SemaphoreSlim OptimizationLock = new(1, 1);
    private static readonly int?[] DimensionSteps = [null, 128, 112, 96, 80, 64];

    static EmojiImageOptimizer()
    {
        // Keep native ImageMagick work inside the small memory envelope of the Fly machine.
        MagickNET.SetEnvironmentVariable("MAGICK_THREAD_LIMIT", "1");
        MagickNET.SetEnvironmentVariable("MAGICK_MEMORY_LIMIT", "64MiB");
        MagickNET.SetEnvironmentVariable("MAGICK_MAP_LIMIT", "64MiB");
        MagickNET.SetEnvironmentVariable("MAGICK_AREA_LIMIT", "16MP");
        MagickNET.SetEnvironmentVariable("MAGICK_DISK_LIMIT", "128MiB");
        MagickNET.SetTempDirectory(Path.GetTempPath());
    }

    public EmojiImageOptimizationResult? OptimizeStaticImage(byte[] imageBytes)
    {
        if (imageBytes.Length > MaxOptimizableImageBytes)
        {
            logger.LogInformation(
                "Skipping emoji optimization because the source image is {ImageSize} bytes, above the {Limit} byte safety limit.",
                imageBytes.Length,
                MaxOptimizableImageBytes);
            return null;
        }

        if (!OptimizationLock.Wait(TimeSpan.FromSeconds(2)))
        {
            logger.LogInformation("Skipping emoji optimization because another optimization is already running.");
            return null;
        }

        try
        {
            return Optimize(imageBytes);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to optimize emoji image.");
            return null;
        }
        finally
        {
            OptimizationLock.Release();
        }
    }

    private static EmojiImageOptimizationResult? Optimize(byte[] imageBytes)
    {
        EmojiImageOptimizationResult? smallestResult = null;

        foreach (var maxDimension in DimensionSteps)
        {
            var result = CreateStaticImageCandidate(imageBytes, maxDimension);
            smallestResult = GetSmallerResult(smallestResult, result);

            if (result.ImageBytes.Length <= DiscordEmojiSizeLimitBytes)
            {
                return result;
            }
        }

        return smallestResult?.ImageBytes.Length <= DiscordEmojiSizeLimitBytes
            ? smallestResult
            : null;
    }

    private static EmojiImageOptimizationResult CreateStaticImageCandidate(
        byte[] imageBytes,
        int? maxDimension)
    {
        using var image = new MagickImage(imageBytes);
        image.Strip();

        if (maxDimension is { } dimension)
        {
            image.Resize(new MagickGeometry((uint)dimension, (uint)dimension)
            {
                IgnoreAspectRatio = false,
                Greater = true,
            });
        }

        using var stream = new MemoryStream();
        image.Write(stream, image.Format);

        return new EmojiImageOptimizationResult(
            stream.ToArray(),
            maxDimension is null
                ? "stripped image metadata"
                : $"stripped image metadata and resized to max {maxDimension}px");
    }

    private static EmojiImageOptimizationResult GetSmallerResult(
        EmojiImageOptimizationResult? current,
        EmojiImageOptimizationResult candidate) =>
        current is null || candidate.ImageBytes.Length < current.ImageBytes.Length
            ? candidate
            : current;
}
