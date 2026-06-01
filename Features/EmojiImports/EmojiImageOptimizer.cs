using ImageMagick;

namespace FlowBot;

public sealed class EmojiImageOptimizer(ILogger<EmojiImageOptimizer> logger)
{
    public const int DiscordEmojiSizeLimitBytes = 256 * 1024;

    private const int MaxOptimizableImageBytes = 2 * 1024 * 1024;
    private const int MaxAnimatedFrameCount = 80;
    private const ulong MaxAnimatedPixelsPerFrame = 512UL * 512UL;
    private static readonly SemaphoreSlim OptimizationLock = new(1, 1);
    private static readonly int?[] DimensionSteps = [null, 128, 112, 96, 80, 64];
    private static readonly AnimatedOptimizationStep[] AnimatedSteps =
    [
        new(null, null),
        new(128, null),
        new(112, null),
        new(96, null),
        new(128, 224),
        new(112, 224),
        new(96, 224),
        new(96, 192),
        new(80, 192),
        new(80, 160),
        new(64, 160),
        new(64, 128),
        new(64, 96),
        new(64, 64),
    ];

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

    public EmojiImageOptimizationResult? Optimize(byte[] imageBytes, bool isAnimated)
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
            return isAnimated
                ? OptimizeAnimatedGif(imageBytes)
                : OptimizeStaticImage(imageBytes);
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

    private static EmojiImageOptimizationResult? OptimizeAnimatedGif(byte[] imageBytes)
    {
        EmojiImageOptimizationResult? smallestResult = null;

        foreach (var step in AnimatedSteps)
        {
            var result = CreateAnimatedGifCandidate(imageBytes, step.MaxDimension, step.Colors);
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

    private static EmojiImageOptimizationResult? OptimizeStaticImage(byte[] imageBytes)
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

    private static EmojiImageOptimizationResult CreateAnimatedGifCandidate(
        byte[] imageBytes,
        int? maxDimension,
        int? colors)
    {
        using var images = new MagickImageCollection(imageBytes);

        EnsureAnimatedGifIsSafeToOptimize(images);

        foreach (var image in images)
        {
            image.Strip();

            if (maxDimension is { } dimension)
            {
                image.Resize(new MagickGeometry((uint)dimension, (uint)dimension)
                {
                    IgnoreAspectRatio = false,
                    Greater = true,
                });
            }
        }

        if (colors is { } colorCount)
        {
            images.Quantize(new QuantizeSettings
            {
                Colors = (uint)colorCount,
                DitherMethod = DitherMethod.FloydSteinberg,
            });
        }

        images.Optimize();
        images.OptimizeTransparency();

        using var stream = new MemoryStream();
        images.Write(stream, MagickFormat.Gif);

        return new EmojiImageOptimizationResult(
            stream.ToArray(),
            BuildDescription(maxDimension, colors));
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

    private static string BuildDescription(int? maxDimension, int? colors)
    {
        var steps = new List<string> { "optimized GIF frames" };

        if (maxDimension is { } dimension)
        {
            steps.Add($"resized to max {dimension}px");
        }

        if (colors is { } colorCount)
        {
            steps.Add($"reduced palette to {colorCount} colors");
        }

        return string.Join(", ", steps);
    }

    private static void EnsureAnimatedGifIsSafeToOptimize(MagickImageCollection images)
    {
        if (images.Count > MaxAnimatedFrameCount)
        {
            throw new InvalidOperationException(
                $"Animated emoji has {images.Count} frames, above the {MaxAnimatedFrameCount} frame optimization limit.");
        }

        foreach (var image in images)
        {
            var pixels = (ulong)image.Width * image.Height;

            if (pixels > MaxAnimatedPixelsPerFrame)
            {
                throw new InvalidOperationException(
                    $"Animated emoji has a {image.Width}x{image.Height} frame, above the optimization pixel limit.");
            }
        }
    }

    private sealed record AnimatedOptimizationStep(int? MaxDimension, int? Colors);
}
