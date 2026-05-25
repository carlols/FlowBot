namespace FlowBot;

public static class RaidVoiceSplitButtonIds
{
    private const string Prefix = "flowbot-raid-voice:";

    public static string CreateMoveGroupId(ulong roleId, ulong targetChannelId) =>
        $"{Prefix}move:{roleId}:{targetChannelId}";

    public static string CreateCloseId() =>
        $"{Prefix}close";

    public static bool IsRaidVoiceSplitButton(string customId) =>
        customId.StartsWith(Prefix, StringComparison.Ordinal);

    public static bool TryParse(string customId, out RaidVoiceSplitButtonState state)
    {
        state = new RaidVoiceSplitButtonState(RaidVoiceSplitAction.Close, 0, 0);

        if (!customId.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var values = customId[Prefix.Length..].Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (values.Length == 1 && values[0] == "close")
        {
            return true;
        }

        if (values.Length != 3
            || values[0] != "move"
            || !ulong.TryParse(values[1], out var roleId)
            || !ulong.TryParse(values[2], out var targetChannelId))
        {
            return false;
        }

        state = new RaidVoiceSplitButtonState(RaidVoiceSplitAction.MoveGroup, roleId, targetChannelId);
        return true;
    }
}
