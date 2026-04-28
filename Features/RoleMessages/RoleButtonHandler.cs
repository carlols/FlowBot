using Discord.WebSocket;

namespace FlowBot;

public sealed class RoleButtonHandler(ILogger<RoleButtonHandler> logger)
{
    public async Task HandleAsync(SocketMessageComponent component)
    {
        if (!RoleButtonIds.TryParse(component.Data.CustomId, out var action, out var roleId))
        {
            await component.RespondAsync("I could not identify the role for this button.", ephemeral: true);
            return;
        }

        if (component.User is not SocketGuildUser user)
        {
            await component.RespondAsync("This button can only be used inside a server.", ephemeral: true);
            return;
        }

        var role = user.Guild.GetRole(roleId);

        if (role is null)
        {
            await component.RespondAsync("That role no longer exists.", ephemeral: true);
            return;
        }

        var hasRole = user.Roles.Any(userRole => userRole.Id == role.Id);

        if (action == RoleButtonAction.Add && hasRole)
        {
            await component.RespondAsync($"You already have the {role.Mention} role.", ephemeral: true);
            return;
        }

        if (action == RoleButtonAction.Remove && !hasRole)
        {
            await component.RespondAsync($"You do not have the {role.Mention} role.", ephemeral: true);
            return;
        }

        if (role.Position >= user.Guild.CurrentUser.Hierarchy)
        {
            await component.RespondAsync(
                $"I cannot manage {role.Mention} because it is at or above my highest role.",
                ephemeral: true);
            return;
        }

        await UpdateRoleAsync(component, user, role, action);
    }

    private async Task UpdateRoleAsync(
        SocketMessageComponent component,
        SocketGuildUser user,
        SocketRole role,
        RoleButtonAction action)
    {
        try
        {
            if (action == RoleButtonAction.Add)
            {
                await user.AddRoleAsync(role);
                await component.RespondAsync($"You now have the {role.Mention} role.", ephemeral: true);
                return;
            }

            await user.RemoveRoleAsync(role);
            await component.RespondAsync($"Removed the {role.Mention} role from you.", ephemeral: true);
        }
        catch (Exception exception)
        {
            var actionName = action == RoleButtonAction.Add ? "assign" : "remove";
            logger.LogWarning(exception, "Failed to {Action} role {RoleId} for user {UserId}.", actionName, role.Id, user.Id);
            await component.RespondAsync(
                $"I could not {actionName} that role. Check my role permissions and role order.",
                ephemeral: true);
        }
    }
}
