using Discord;
using Discord.WebSocket;

namespace FlowBot;

public sealed partial class RolePanelHandler
{
    private async Task AddRoleAsync(
        SocketMessageComponent component,
        RolePanelInteractionState state)
    {
        if (!TryGetAdministrator(component, out var administrator))
        {
            await UpdateEphemeralResponseAsync(component, "Only server admins can edit role panels.");
            return;
        }

        var role = component.Data.Roles?.SingleOrDefault();

        if (role is null)
        {
            await UpdateEphemeralResponseAsync(component, "Please choose a valid role.");
            return;
        }

        await UpdateEphemeralResponseAsync(component, "Updating the role panel...");

        if (!SelfAssignableRoleValidator.TryValidate(administrator.Guild, role, out var errorMessage))
        {
            await RestoreAdminEditorAsync(component, state, administrator.Guild, errorMessage);
            return;
        }

        await EditPanelAsync(component, state, administrator.Guild, panel =>
        {
            if (panel.RoleIds.Contains(role.Id))
            {
                return (panel, $"{role.Mention} is already in this panel.");
            }

            if (panel.RoleIds.Count >= RolePanelMessageBuilder.MaxRoles)
            {
                return (panel, $"A role panel can contain at most {RolePanelMessageBuilder.MaxRoles} roles.");
            }

            return (
                panel with { RoleIds = [.. panel.RoleIds, role.Id] },
                $"Added {role.Mention} to **{panel.Title}**.");
        });
    }

    private async Task RemoveRoleAsync(
        SocketMessageComponent component,
        RolePanelInteractionState state)
    {
        if (!TryGetAdministrator(component, out var administrator))
        {
            await UpdateEphemeralResponseAsync(component, "Only server admins can edit role panels.");
            return;
        }

        var selectedValue = component.Data.Values.SingleOrDefault();

        if (!ulong.TryParse(selectedValue, out var roleId))
        {
            await UpdateEphemeralResponseAsync(component, "Please choose a valid role.");
            return;
        }

        await UpdateEphemeralResponseAsync(component, "Updating the role panel...");

        await EditPanelAsync(component, state, administrator.Guild, panel =>
        {
            var role = administrator.Guild.GetRole(roleId);

            if (!panel.RoleIds.Contains(roleId))
            {
                return (panel, "That role is no longer in this panel.");
            }

            if (panel.RoleIds.Count == 1)
            {
                return (panel, "A role panel must contain at least one role.");
            }

            return (
                panel with { RoleIds = panel.RoleIds.Where(id => id != roleId).ToArray() },
                role is null
                    ? $"Removed the deleted role from **{panel.Title}**."
                    : $"Removed {role.Mention} from **{panel.Title}**.");
        });
    }

    private async Task EditPanelAsync(
        SocketMessageComponent component,
        RolePanelInteractionState state,
        SocketGuild guild,
        Func<RolePanel, (RolePanel Panel, string Response)> edit)
    {
        try
        {
            using (await _messageMutationLock.AcquireAsync(state.MessageId))
            {
                var loadedPanel = await LoadPanelAsync(guild, state.ChannelId, state.MessageId);

                if (loadedPanel is null)
                {
                    await ModifyEphemeralResponseAsync(component, "That role panel no longer exists.");
                    return;
                }

                var (updatedPanel, response) = edit(loadedPanel.Value.Panel);

                if (!ReferenceEquals(updatedPanel, loadedPanel.Value.Panel))
                {
                    await UpdatePanelMessageAsync(loadedPanel.Value.Message, updatedPanel);
                }

                await ModifyEphemeralResponseAsync(
                    component,
                    $"{response} Changes are visible on the panel; no channel message was posted.",
                    RolePanelMenuBuilder.BuildAdminEditor(
                        updatedPanel,
                        guild,
                        state.ChannelId,
                        state.MessageId));
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to edit role panel {MessageId}.",
                state.MessageId);
            await ModifyEphemeralResponseAsync(component, "I could not update that role panel.");
        }
    }

    private static bool TryGetAdministrator(
        SocketMessageComponent component,
        out SocketGuildUser administrator)
    {
        administrator = component.User as SocketGuildUser ?? null!;
        return administrator is not null && administrator.GuildPermissions.Administrator;
    }
}
