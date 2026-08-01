using Discord;
using Discord.WebSocket;

namespace FlowBot;

public sealed partial class RolePanelHandler
{
    private static async Task OpenMemberEditorAsync(SocketMessageComponent component)
    {
        if (component.User is not SocketGuildUser user)
        {
            await component.RespondAsync("Role panels can only be used inside a server.", ephemeral: true);
            return;
        }

        if (!RolePanelMessageParser.TryParse(component.Message, out var panel))
        {
            await component.RespondAsync("I could not read this role panel.", ephemeral: true);
            return;
        }

        var availableRoleIds = panel.RoleIds
            .Where(roleId => user.Guild.GetRole(roleId) is not null)
            .ToArray();

        if (availableRoleIds.Length == 0)
        {
            await component.RespondAsync("None of the roles in this panel still exist.", ephemeral: true);
            return;
        }

        var availablePanel = panel with { RoleIds = availableRoleIds };
        var selectedRoleIds = user.Roles.Select(role => role.Id).ToHashSet();

        await component.RespondAsync(
            "Select every role you want to keep from this panel. Your current roles are already selected.",
            components: RolePanelMenuBuilder.BuildMemberEditor(
                availablePanel,
                user.Guild,
                selectedRoleIds,
                component.Channel.Id,
                component.Message.Id),
            ephemeral: true);
    }

    private async Task SaveMemberRolesAsync(
        SocketMessageComponent component,
        RolePanelInteractionState state)
    {
        if (component.User is not SocketGuildUser user)
        {
            await UpdateEphemeralResponseAsync(component, "Role panels can only be used inside a server.");
            return;
        }

        await UpdateEphemeralResponseAsync(component, "Saving your roles...");

        try
        {
            var loadedPanel = await LoadPanelAsync(user.Guild, state.ChannelId, state.MessageId);

            if (loadedPanel is null)
            {
                await ModifyEphemeralResponseAsync(component, "That role panel no longer exists.");
                return;
            }

            var roles = loadedPanel.Value.Panel.RoleIds
                .Select(user.Guild.GetRole)
                .Where(role => role is not null)
                .Cast<SocketRole>()
                .ToArray();
            if (roles.Length == 0)
            {
                await ModifyEphemeralResponseAsync(component, "None of the roles in this panel still exist.");
                return;
            }

            var selectedRoleIds = component.Data.Values
                .Select(value => ulong.TryParse(value, out var roleId) ? roleId : 0)
                .Where(roleId => roleId != 0)
                .ToHashSet();
            var result = await _roleSelectionUpdater.UpdateAsync(user, roles, selectedRoleIds);

            await ModifyEphemeralResponseAsync(
                component,
                BuildMemberUpdateSummary(result),
                RolePanelMenuBuilder.BuildMemberEditor(
                    loadedPanel.Value.Panel,
                    user.Guild,
                    result.EffectiveRoleIds,
                    state.ChannelId,
                    state.MessageId));
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to save roles for user {UserId} from panel {MessageId}.",
                user.Id,
                state.MessageId);
            await ModifyEphemeralResponseAsync(component, "I could not save your role selection.");
        }
    }

    private static string BuildMemberUpdateSummary(RoleSelectionUpdateResult result)
    {
        var changes = new List<string>();

        if (result.AddedRoles.Count > 0)
        {
            changes.Add($"Added {string.Join(", ", result.AddedRoles.Select(role => role.Mention))}.");
        }

        if (result.RemovedRoles.Count > 0)
        {
            changes.Add($"Removed {string.Join(", ", result.RemovedRoles.Select(role => role.Mention))}.");
        }

        if (changes.Count == 0 && result.FailedRoles.Count == 0)
        {
            changes.Add("Your roles were already up to date.");
        }

        if (result.FailedRoles.Count > 0)
        {
            changes.Add($"I could not update {string.Join(", ", result.FailedRoles.Select(role => role.Mention))}.");
        }

        return string.Join(' ', changes);
    }
}
