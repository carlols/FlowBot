using Discord.WebSocket;

namespace FlowBot;

public sealed class RoleSelectionUpdater(ILogger<RoleSelectionUpdater> logger)
{
    public async Task<RoleSelectionUpdateResult> UpdateAsync(
        SocketGuildUser user,
        IReadOnlyList<SocketRole> panelRoles,
        IReadOnlySet<ulong> selectedRoleIds)
    {
        var currentRoleIds = user.Roles.Select(role => role.Id).ToHashSet();
        var effectiveRoleIds = currentRoleIds.ToHashSet();
        var addedRoles = new List<SocketRole>();
        var removedRoles = new List<SocketRole>();
        var failedRoles = new List<SocketRole>();

        foreach (var role in panelRoles)
        {
            var shouldHaveRole = selectedRoleIds.Contains(role.Id);
            var hasRole = currentRoleIds.Contains(role.Id);

            if (shouldHaveRole == hasRole)
            {
                continue;
            }

            var roleIsValid = shouldHaveRole
                ? SelfAssignableRoleValidator.TryValidate(user.Guild, role, out _)
                : SelfAssignableRoleValidator.TryValidateManageable(user.Guild, role, out _);

            if (!roleIsValid)
            {
                failedRoles.Add(role);
                continue;
            }

            try
            {
                if (shouldHaveRole)
                {
                    await user.AddRoleAsync(role);
                    effectiveRoleIds.Add(role.Id);
                    addedRoles.Add(role);
                }
                else
                {
                    await user.RemoveRoleAsync(role);
                    effectiveRoleIds.Remove(role.Id);
                    removedRoles.Add(role);
                }
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Failed to update role {RoleId} for user {UserId} through a role panel.",
                    role.Id,
                    user.Id);
                failedRoles.Add(role);
            }
        }

        return new RoleSelectionUpdateResult(addedRoles, removedRoles, failedRoles, effectiveRoleIds);
    }
}
