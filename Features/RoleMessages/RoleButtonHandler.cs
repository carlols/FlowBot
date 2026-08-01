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
        var resolvedAction = action == RoleButtonAction.Toggle
            ? hasRole ? RoleButtonAction.Remove : RoleButtonAction.Add
            : action;
        var roleIsValid = resolvedAction == RoleButtonAction.Add
            ? SelfAssignableRoleValidator.TryValidate(user.Guild, role, out var errorMessage)
            : SelfAssignableRoleValidator.TryValidateManageable(user.Guild, role, out errorMessage);

        if (!roleIsValid)
        {
            await component.RespondAsync(errorMessage, ephemeral: true);
            return;
        }

        if (resolvedAction == RoleButtonAction.Add && hasRole)
        {
            await component.RespondAsync($"You already have the {role.Mention} role.", ephemeral: true);
            return;
        }

        if (resolvedAction == RoleButtonAction.Remove && !hasRole)
        {
            await component.RespondAsync($"You do not have the {role.Mention} role.", ephemeral: true);
            return;
        }

        await UpdateRoleAsync(component, user, role, resolvedAction);
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
                await component.RespondAsync($"Added {role.Mention}.", ephemeral: true);
                return;
            }

            await user.RemoveRoleAsync(role);
            await component.RespondAsync($"Removed {role.Mention}.", ephemeral: true);
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
