namespace FlowBot;

public static class RolePanelIds
{
    public const string OpenMemberEditor = "flowbot-role-panel:open";

    private const string Prefix = "flowbot-role-panel:";
    private const string SaveMemberRolesPrefix = $"{Prefix}save:";
    private const string AddRolePrefix = $"{Prefix}add:";
    private const string RemoveRolePrefix = $"{Prefix}remove:";

    public static string CreateSaveMemberRolesId(ulong channelId, ulong messageId) =>
        CreateTargetedId(SaveMemberRolesPrefix, channelId, messageId);

    public static string CreateAddRoleId(ulong channelId, ulong messageId) =>
        CreateTargetedId(AddRolePrefix, channelId, messageId);

    public static string CreateRemoveRoleId(ulong channelId, ulong messageId) =>
        CreateTargetedId(RemoveRolePrefix, channelId, messageId);

    public static bool IsRolePanelInteraction(string customId) =>
        customId.StartsWith(Prefix, StringComparison.Ordinal);

    public static bool TryParse(string customId, out RolePanelInteractionState state)
    {
        if (customId == OpenMemberEditor)
        {
            state = new RolePanelInteractionState(RolePanelAction.OpenMemberEditor);
            return true;
        }

        if (TryParseTargetedId(customId, SaveMemberRolesPrefix, RolePanelAction.SaveMemberRoles, out state)
            || TryParseTargetedId(customId, AddRolePrefix, RolePanelAction.AddRole, out state)
            || TryParseTargetedId(customId, RemoveRolePrefix, RolePanelAction.RemoveRole, out state))
        {
            return true;
        }

        state = default!;
        return false;
    }

    private static string CreateTargetedId(string prefix, ulong channelId, ulong messageId) =>
        $"{prefix}{channelId}:{messageId}";

    private static bool TryParseTargetedId(
        string customId,
        string prefix,
        RolePanelAction action,
        out RolePanelInteractionState state)
    {
        state = default!;

        if (!customId.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var values = customId[prefix.Length..].Split(':');

        if (values.Length != 2
            || !ulong.TryParse(values[0], out var channelId)
            || !ulong.TryParse(values[1], out var messageId))
        {
            return false;
        }

        state = new RolePanelInteractionState(action, channelId, messageId);
        return true;
    }
}
