namespace FlowBot;

public static class RoleVoiceMoveButtonIds
{
    private const string Prefix = "flowbot-role-voice:";
    private const string LegacyPrefix = "flowbot-raid-voice:";

    public static string CreateMoveId(ulong roleId, ulong destinationChannelId) =>
        $"{Prefix}move:{roleId}:{destinationChannelId}";

    public static string CreateCloseId() =>
        $"{Prefix}close";

    public static bool IsRoleVoiceMoveButton(string customId) =>
        customId.StartsWith(Prefix, StringComparison.Ordinal)
        || customId.StartsWith(LegacyPrefix, StringComparison.Ordinal);

    public static bool TryParse(string customId, out RoleVoiceMoveButtonState state)
    {
        state = new RoleVoiceMoveButtonState(RoleVoiceMoveAction.Close, 0, 0);

        var prefix = customId.StartsWith(Prefix, StringComparison.Ordinal)
            ? Prefix
            : customId.StartsWith(LegacyPrefix, StringComparison.Ordinal)
                ? LegacyPrefix
                : null;

        if (prefix is null)
        {
            return false;
        }

        var values = customId[prefix.Length..].Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (values.Length == 1 && values[0] == "close")
        {
            return true;
        }

        if (values.Length != 3
            || values[0] is not ("move" or "move-split" or "move-main")
            || !ulong.TryParse(values[1], out var roleId)
            || !ulong.TryParse(values[2], out var destinationChannelId))
        {
            return false;
        }

        state = new RoleVoiceMoveButtonState(RoleVoiceMoveAction.MoveGroup, roleId, destinationChannelId);
        return true;
    }
}
