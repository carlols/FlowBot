namespace FlowBot;

public static class GroupFinderButtonIds
{
    private const string JoinPrefix = "flowbot-group-join:";
    private const string LeavePrefix = "flowbot-group-leave:";
    private const string ClosePrefix = "flowbot-group-close:";

    public static string CreateJoinId(int capacity) => $"{JoinPrefix}{capacity}";

    public static string CreateLeaveId(int capacity) => $"{LeavePrefix}{capacity}";

    public static string CreateCloseId(int capacity) => $"{ClosePrefix}{capacity}";

    public static bool IsGroupFinderButton(string customId) =>
        customId.StartsWith(JoinPrefix, StringComparison.Ordinal)
        || customId.StartsWith(LeavePrefix, StringComparison.Ordinal)
        || customId.StartsWith(ClosePrefix, StringComparison.Ordinal);

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
