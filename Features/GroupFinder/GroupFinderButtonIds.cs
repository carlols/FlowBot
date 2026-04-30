namespace FlowBot;

public static class GroupFinderButtonIds
{
    private const string JoinPrefix = "flowbot-group-join:";
    private const string LeavePrefix = "flowbot-group-leave:";
    private const string StartPrefix = "flowbot-group-start:";
    private const string ConfirmStartPrefix = "flowbot-group-confirm-start:";
    private const string CancelStartPrefix = "flowbot-group-cancel-start:";
    private const string ClosePrefix = "flowbot-group-close:";
    private const string ConfirmClosePrefix = "flowbot-group-confirm-close:";
    private const string CancelClosePrefix = "flowbot-group-cancel-close:";
    private const string OpenCapacityToken = "open";

    public static string CreateJoinId(int? capacity, bool fullNotificationSent) =>
        $"{JoinPrefix}{FormatCapacity(capacity)}:{FormatNotificationState(fullNotificationSent)}";

    public static string CreateLeaveId(int? capacity, bool fullNotificationSent) =>
        $"{LeavePrefix}{FormatCapacity(capacity)}:{FormatNotificationState(fullNotificationSent)}";

    public static string CreateStartId(int? capacity, bool fullNotificationSent) =>
        $"{StartPrefix}{FormatCapacity(capacity)}:{FormatNotificationState(fullNotificationSent)}";

    public static string CreateConfirmStartId(ulong messageId, ulong hostUserId, int? capacity, bool fullNotificationSent) =>
        $"{ConfirmStartPrefix}{messageId}:{hostUserId}:{FormatCapacity(capacity)}:{FormatNotificationState(fullNotificationSent)}";

    public static string CreateCancelStartId() => CancelStartPrefix;

    public static string CreateCloseId(int? capacity, bool fullNotificationSent) =>
        $"{ClosePrefix}{FormatCapacity(capacity)}:{FormatNotificationState(fullNotificationSent)}";

    public static string CreateConfirmCloseId(ulong messageId, ulong hostUserId) =>
        $"{ConfirmClosePrefix}{messageId}:{hostUserId}";

    public static string CreateCancelCloseId() => CancelClosePrefix;

    public static bool IsGroupFinderButton(string customId) =>
        customId.StartsWith(JoinPrefix, StringComparison.Ordinal)
        || customId.StartsWith(LeavePrefix, StringComparison.Ordinal)
        || customId.StartsWith(StartPrefix, StringComparison.Ordinal)
        || customId.StartsWith(ConfirmStartPrefix, StringComparison.Ordinal)
        || customId.StartsWith(CancelStartPrefix, StringComparison.Ordinal)
        || customId.StartsWith(ClosePrefix, StringComparison.Ordinal)
        || customId.StartsWith(ConfirmClosePrefix, StringComparison.Ordinal)
        || customId.StartsWith(CancelClosePrefix, StringComparison.Ordinal);

    public static bool TryParse(
        string customId,
        out GroupFinderButtonAction action,
        out int? capacity,
        out bool? fullNotificationSent)
    {
        if (TryParse(customId, JoinPrefix, GroupFinderButtonAction.Join, out action, out capacity, out fullNotificationSent))
        {
            return true;
        }

        if (TryParse(customId, LeavePrefix, GroupFinderButtonAction.Leave, out action, out capacity, out fullNotificationSent))
        {
            return true;
        }

        if (TryParse(customId, StartPrefix, GroupFinderButtonAction.Start, out action, out capacity, out fullNotificationSent))
        {
            return true;
        }

        return TryParse(customId, ClosePrefix, GroupFinderButtonAction.Close, out action, out capacity, out fullNotificationSent);
    }

    public static bool TryParseStartConfirmation(
        string customId,
        out GroupFinderButtonAction action,
        out ulong messageId,
        out ulong hostUserId,
        out int? capacity,
        out bool? fullNotificationSent)
    {
        action = GroupFinderButtonAction.ConfirmStart;
        messageId = 0;
        hostUserId = 0;
        capacity = null;
        fullNotificationSent = null;

        if (customId == CancelStartPrefix)
        {
            action = GroupFinderButtonAction.CancelStart;
            return true;
        }

        if (!customId.StartsWith(ConfirmStartPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var values = customId[ConfirmStartPrefix.Length..].Split(':', StringSplitOptions.RemoveEmptyEntries);

        if (values.Length is < 3 or > 4
            || !ulong.TryParse(values[0], out messageId)
            || !ulong.TryParse(values[1], out hostUserId)
            || !TryParseCapacity(values[2], out capacity))
        {
            return false;
        }

        if (values.Length == 4)
        {
            fullNotificationSent = values[3] == "1";
        }

        return true;
    }

    public static bool TryParseCloseConfirmation(
        string customId,
        out GroupFinderButtonAction action,
        out ulong messageId,
        out ulong hostUserId)
    {
        action = GroupFinderButtonAction.ConfirmClose;
        messageId = 0;
        hostUserId = 0;

        if (customId == CancelClosePrefix)
        {
            action = GroupFinderButtonAction.CancelClose;
            return true;
        }

        if (!customId.StartsWith(ConfirmClosePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var values = customId[ConfirmClosePrefix.Length..].Split(':', StringSplitOptions.RemoveEmptyEntries);

        return values.Length == 2
            && ulong.TryParse(values[0], out messageId)
            && ulong.TryParse(values[1], out hostUserId);
    }

    private static bool TryParse(
        string customId,
        string prefix,
        GroupFinderButtonAction expectedAction,
        out GroupFinderButtonAction action,
        out int? capacity,
        out bool? fullNotificationSent)
    {
        action = expectedAction;
        capacity = null;
        fullNotificationSent = null;

        if (!customId.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var values = customId[prefix.Length..].Split(':', StringSplitOptions.RemoveEmptyEntries);

        if (values.Length is < 1 or > 2
            || !TryParseCapacity(values[0], out capacity))
        {
            return false;
        }

        if (values.Length == 2)
        {
            fullNotificationSent = values[1] == "1";
        }

        return true;
    }

    private static string FormatCapacity(int? capacity) =>
        capacity?.ToString() ?? OpenCapacityToken;

    private static bool TryParseCapacity(string value, out int? capacity)
    {
        capacity = null;

        if (string.Equals(value, OpenCapacityToken, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!int.TryParse(value, out var parsedCapacity)
            || parsedCapacity is < GroupFinderSession.MinCapacity or > GroupFinderSession.MaxCapacity)
        {
            return false;
        }

        capacity = parsedCapacity;
        return true;
    }

    private static string FormatNotificationState(bool fullNotificationSent) =>
        fullNotificationSent ? "1" : "0";
}
