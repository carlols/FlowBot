using System.Globalization;
using Microsoft.Extensions.Options;

namespace FlowBot;

public sealed class GroupFinderTimeParser(IOptions<FlowBotOptions> options, ILogger<GroupFinderTimeParser> logger)
{
    private static readonly string[] TimeFormats = ["H:mm", "HH:mm", "H.mm", "HH.mm"];
    private static readonly string[] DateTimeFormats = ["yyyy-MM-dd H:mm", "yyyy-MM-dd HH:mm", "yyyy-MM-dd H.mm", "yyyy-MM-dd HH.mm"];

    public bool TryParse(string? input, out long? unixTimeSeconds, out string errorMessage)
    {
        unixTimeSeconds = null;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            return true;
        }

        var timeZone = GetConfiguredTimeZone();
        var trimmedInput = input.Trim();

        if (TryParseDateTime(trimmedInput, timeZone, out var dateTime))
        {
            unixTimeSeconds = dateTime.ToUnixTimeSeconds();
            return true;
        }

        errorMessage = "I could not understand that time. Try `20:00`, `17.00`, `tomorrow 20:00`, or `2026-04-28 20:00`.";
        return false;
    }

    private bool TryParseDateTime(string input, TimeZoneInfo timeZone, out DateTimeOffset dateTime)
    {
        if (TryParseTimeOnly(input, timeZone, out dateTime))
        {
            return true;
        }

        if (input.StartsWith("tomorrow ", StringComparison.OrdinalIgnoreCase))
        {
            var timeInput = input["tomorrow ".Length..].Trim();
            return TryParseTimeForDate(timeInput, GetNowInTimeZone(timeZone).Date.AddDays(1), timeZone, out dateTime);
        }

        if (input.StartsWith("today ", StringComparison.OrdinalIgnoreCase))
        {
            var timeInput = input["today ".Length..].Trim();
            return TryParseTimeForDate(timeInput, GetNowInTimeZone(timeZone).Date, timeZone, out dateTime);
        }

        if (DateTime.TryParseExact(
            input,
            DateTimeFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsedLocalDateTime))
        {
            return TryCreateDateTimeOffset(parsedLocalDateTime, timeZone, out dateTime);
        }

        dateTime = default;
        return false;
    }

    private bool TryParseTimeOnly(string input, TimeZoneInfo timeZone, out DateTimeOffset dateTime)
    {
        if (!TimeOnly.TryParseExact(input, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedTime))
        {
            dateTime = default;
            return false;
        }

        var now = GetNowInTimeZone(timeZone);
        var localDateTime = now.Date.Add(parsedTime.ToTimeSpan());

        if (localDateTime <= now.DateTime)
        {
            localDateTime = localDateTime.AddDays(1);
        }

        return TryCreateDateTimeOffset(localDateTime, timeZone, out dateTime);
    }

    private bool TryParseTimeForDate(string input, DateTime date, TimeZoneInfo timeZone, out DateTimeOffset dateTime)
    {
        if (!TimeOnly.TryParseExact(input, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedTime))
        {
            dateTime = default;
            return false;
        }

        return TryCreateDateTimeOffset(date.Add(parsedTime.ToTimeSpan()), timeZone, out dateTime);
    }

    private static DateTimeOffset GetNowInTimeZone(TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);

    private static bool TryCreateDateTimeOffset(DateTime localDateTime, TimeZoneInfo timeZone, out DateTimeOffset dateTime)
    {
        if (timeZone.IsInvalidTime(localDateTime))
        {
            dateTime = default;
            return false;
        }

        var offset = timeZone.GetUtcOffset(localDateTime);
        dateTime = new DateTimeOffset(localDateTime, offset);
        return true;
    }

    private TimeZoneInfo GetConfiguredTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);
        }
        catch (TimeZoneNotFoundException exception)
        {
            logger.LogWarning(exception, "Configured time zone {TimeZone} was not found. Falling back to local time.", options.Value.TimeZone);
            return TimeZoneInfo.Local;
        }
        catch (InvalidTimeZoneException exception)
        {
            logger.LogWarning(exception, "Configured time zone {TimeZone} is invalid. Falling back to local time.", options.Value.TimeZone);
            return TimeZoneInfo.Local;
        }
    }
}
