namespace FlowBot;

public static class RoleButtonIds
{
    private const string AddRolePrefix = "flowbot-role-add:";
    private const string RemoveRolePrefix = "flowbot-role-remove:";

    public static string CreateAddRoleId(ulong roleId) => $"{AddRolePrefix}{roleId}";

    public static string CreateRemoveRoleId(ulong roleId) => $"{RemoveRolePrefix}{roleId}";

    public static bool IsRoleButton(string customId) =>
        customId.StartsWith(AddRolePrefix, StringComparison.Ordinal)
        || customId.StartsWith(RemoveRolePrefix, StringComparison.Ordinal);

    public static bool TryParse(string customId, out RoleButtonAction action, out ulong roleId)
    {
        if (TryParse(customId, AddRolePrefix, RoleButtonAction.Add, out action, out roleId))
        {
            return true;
        }

        return TryParse(customId, RemoveRolePrefix, RoleButtonAction.Remove, out action, out roleId);
    }

    private static bool TryParse(
        string customId,
        string prefix,
        RoleButtonAction expectedAction,
        out RoleButtonAction action,
        out ulong roleId)
    {
        action = expectedAction;
        roleId = 0;

        return customId.StartsWith(prefix, StringComparison.Ordinal)
            && ulong.TryParse(customId[prefix.Length..], out roleId);
    }
}
