namespace FlowBot;

public static class GroupFinderButtonIds
{
    private const string JoinPrefix = "flowbot-group-join:";
    private const string LeavePrefix = "flowbot-group-leave:";
    private const string ClosePrefix = "flowbot-group-close:";
    private const string ConfirmClosePrefix = "flowbot-group-confirm-close:";
    private const string CancelClosePrefix = "flowbot-group-cancel-close:";

    public static string CreateJoinId(int capacity) => $"{JoinPrefix}{capacity}";

    public static string CreateLeaveId(int capacity) => $"{LeavePrefix}{capacity}";

    public static string CreateCloseId(int capacity) => $"{ClosePrefix}{capacity}";

    public static string CreateConfirmCloseId(ulong messageId, ulong hostUserId) =>
        $"{ConfirmClosePrefix}{messageId}:{hostUserId}";

    public static string CreateCancelCloseId() => CancelClosePrefix;

    public static bool IsGroupFinderButton(string customId) =>
        customId.StartsWith(JoinPrefix, StringComparison.Ordinal)
        || customId.StartsWith(LeavePrefix, StringComparison.Ordinal)
        || customId.StartsWith(ClosePrefix, StringComparison.Ordinal)
        || customId.StartsWith(ConfirmClosePrefix, StringComparison.Ordinal)
        || customId.StartsWith(CancelClosePrefix, StringComparison.Ordinal);

    public static bool TryParse(string customId, out GroupFinderButtonAction action, out int capacity)
    {
        if (TryParse(customId, JoinPrefix, GroupFinderButtonAction.Join, out action, out capacity))
        {
            return true;
        }

        if (TryParse(customId, LeavePrefix, GroupFinderButtonAction.Leave, out action, out capacity))
        {
            return true;
        }

        return TryParse(customId, ClosePrefix, GroupFinderButtonAction.Close, out action, out capacity);
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
        out int capacity)
    {
        action = expectedAction;
        capacity = 0;

        return customId.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(customId[prefix.Length..], out capacity)
            && capacity is >= GroupFinderSession.MinCapacity and <= GroupFinderSession.MaxCapacity;
    }
}
