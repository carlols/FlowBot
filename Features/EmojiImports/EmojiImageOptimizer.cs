using ImageMagick;

namespace FlowBot;

public sealed class EmojiImageOptimizer(ILogger<EmojiImageOptimizer> logger)
{
    public const int DiscordEmojiSizeLimitBytes = 256 * 1024;

    private static readonly int?[] DimensionSteps = [null, 128, 112, 96, 80, 64];
    private static readonly int?[] ColorSteps = [null, 224, 192, 160, 128, 96, 64];

    public EmojiImageOptimizationResult? Optimize(byte[] imageBytes, bool isAnimated)
    {
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
    }

    private static EmojiImageOptimizationResult? OptimizeAnimatedGif(byte[] imageBytes)
    {
        EmojiImageOptimizationResult? smallestResult = null;

        foreach (var maxDimension in DimensionSteps)
        {
            foreach (var colors in ColorSteps)
            {
                if (maxDimension is null && colors is not null)
                {
                    continue;
                }

                var result = CreateAnimatedGifCandidate(imageBytes, maxDimension, colors);
                smallestResult = GetSmallerResult(smallestResult, result);

                if (result.ImageBytes.Length <= DiscordEmojiSizeLimitBytes)
                {
                    return result;
                }
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

        images.Coalesce();

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
}
