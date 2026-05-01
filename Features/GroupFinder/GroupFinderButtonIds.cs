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

    public static string CreateJoinId(int? capacity, bool capacityNoticeSent, bool sessionStarted) =>
        $"{JoinPrefix}{FormatCapacity(capacity)}:{FormatState(capacityNoticeSent)}:{FormatState(sessionStarted)}";

    public static string CreateLeaveId(int? capacity, bool capacityNoticeSent, bool sessionStarted) =>
        $"{LeavePrefix}{FormatCapacity(capacity)}:{FormatState(capacityNoticeSent)}:{FormatState(sessionStarted)}";

    public static string CreateStartId(int? capacity, bool capacityNoticeSent, bool sessionStarted) =>
        $"{StartPrefix}{FormatCapacity(capacity)}:{FormatState(capacityNoticeSent)}:{FormatState(sessionStarted)}";

    public static string CreateConfirmStartId(
        ulong messageId,
        ulong hostUserId,
        int? capacity,
        bool capacityNoticeSent,
        bool sessionStarted) =>
        $"{ConfirmStartPrefix}{messageId}:{hostUserId}:{FormatCapacity(capacity)}:{FormatState(capacityNoticeSent)}:{FormatState(sessionStarted)}";

    public static string CreateCancelStartId() => CancelStartPrefix;

    public static string CreateCloseId(int? capacity, bool capacityNoticeSent, bool sessionStarted) =>
        $"{ClosePrefix}{FormatCapacity(capacity)}:{FormatState(capacityNoticeSent)}:{FormatState(sessionStarted)}";

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

    public static bool TryParse(string customId, out GroupFinderButtonState state)
    {
        if (TryParse(customId, JoinPrefix, GroupFinderButtonAction.Join, out state))
        {
            return true;
        }

        if (TryParse(customId, LeavePrefix, GroupFinderButtonAction.Leave, out state))
        {
            return true;
        }

        if (TryParse(customId, StartPrefix, GroupFinderButtonAction.Start, out state))
        {
            return true;
        }

        return TryParse(customId, ClosePrefix, GroupFinderButtonAction.Close, out state);
    }

    public static bool TryParseStartConfirmation(string customId, out GroupFinderStartConfirmation confirmation)
    {
        confirmation = new GroupFinderStartConfirmation(GroupFinderButtonAction.ConfirmStart, 0, 0, null, null, null);

        if (customId == CancelStartPrefix)
        {
            confirmation = confirmation with { Action = GroupFinderButtonAction.CancelStart };
            return true;
        }

        if (!customId.StartsWith(ConfirmStartPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var values = customId[ConfirmStartPrefix.Length..].Split(':', StringSplitOptions.RemoveEmptyEntries);

        if (values.Length is < 3 or > 5
            || !ulong.TryParse(values[0], out var messageId)
            || !ulong.TryParse(values[1], out var hostUserId)
            || !TryParseCapacity(values[2], out var capacity))
        {
            return false;
        }

        bool? capacityNoticeSent = null;
        bool? sessionStarted = null;

        if (values.Length == 4)
        {
            capacityNoticeSent = values[3] == "1";
            sessionStarted = capacityNoticeSent;
        }

        if (values.Length == 5)
        {
            capacityNoticeSent = values[3] == "1";
            sessionStarted = values[4] == "1";
        }

        confirmation = new GroupFinderStartConfirmation(
            GroupFinderButtonAction.ConfirmStart,
            messageId,
            hostUserId,
            capacity,
            capacityNoticeSent,
            sessionStarted);
        return true;
    }

    public static bool TryParseCloseConfirmation(string customId, out GroupFinderCloseConfirmation confirmation)
    {
        confirmation = new GroupFinderCloseConfirmation(GroupFinderButtonAction.ConfirmClose, 0, 0);

        if (customId == CancelClosePrefix)
        {
            confirmation = confirmation with { Action = GroupFinderButtonAction.CancelClose };
            return true;
        }

        if (!customId.StartsWith(ConfirmClosePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var values = customId[ConfirmClosePrefix.Length..].Split(':', StringSplitOptions.RemoveEmptyEntries);

        if (values.Length != 2
            || !ulong.TryParse(values[0], out var messageId)
            || !ulong.TryParse(values[1], out var hostUserId))
        {
            return false;
        }

        confirmation = new GroupFinderCloseConfirmation(
            GroupFinderButtonAction.ConfirmClose,
            messageId,
            hostUserId);
        return true;
    }

    private static bool TryParse(
        string customId,
        string prefix,
        GroupFinderButtonAction expectedAction,
        out GroupFinderButtonState state)
    {
        state = new GroupFinderButtonState(expectedAction, null, null, null);

        if (!customId.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var values = customId[prefix.Length..].Split(':', StringSplitOptions.RemoveEmptyEntries);

        if (values.Length is < 1 or > 3
            || !TryParseCapacity(values[0], out var capacity))
        {
            return false;
        }

        bool? capacityNoticeSent = null;
        bool? sessionStarted = null;

        if (values.Length == 2)
        {
            capacityNoticeSent = values[1] == "1";
            sessionStarted = capacityNoticeSent;
        }

        if (values.Length == 3)
        {
            capacityNoticeSent = values[1] == "1";
            sessionStarted = values[2] == "1";
        }

        state = new GroupFinderButtonState(expectedAction, capacity, capacityNoticeSent, sessionStarted);
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

    private static string FormatState(bool value) =>
        value ? "1" : "0";
}
