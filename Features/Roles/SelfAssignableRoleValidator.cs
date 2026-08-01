using Discord.WebSocket;

namespace FlowBot;

public static class SelfAssignableRoleValidator
{
    public static bool TryValidate(SocketGuild guild, SocketRole role, out string errorMessage)
    {
        if (!TryValidateManageable(guild, role, out errorMessage))
        {
            return false;
        }

        if (role.Permissions.Administrator
            || role.Permissions.ManageGuild
            || role.Permissions.ManageRoles)
        {
            errorMessage = "I cannot make administrative roles self-assignable.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    public static bool TryValidateManageable(
        SocketGuild guild,
        SocketRole role,
        out string errorMessage)
    {
        if (role.IsEveryone)
        {
            errorMessage = "I cannot manage @everyone.";
            return false;
        }

        if (role.IsManaged)
        {
            errorMessage = "I cannot manage integration or bot roles.";
            return false;
        }

        if (role.Position >= guild.CurrentUser.Hierarchy)
        {
            errorMessage = $"I cannot manage {role.Mention} because it is at or above my highest role. Move Flowbot's role above it and try again.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}
