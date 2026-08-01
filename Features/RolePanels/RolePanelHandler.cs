using Discord.WebSocket;

namespace FlowBot;

public sealed partial class RolePanelHandler(
    DiscordMessageMutationLock messageMutationLock,
    RoleSelectionUpdater roleSelectionUpdater,
    ILogger<RolePanelHandler> logger)
{
    private readonly DiscordMessageMutationLock _messageMutationLock = messageMutationLock;
    private readonly RoleSelectionUpdater _roleSelectionUpdater = roleSelectionUpdater;
    private readonly ILogger<RolePanelHandler> _logger = logger;

    public async Task HandleAsync(SocketMessageComponent component)
    {
        if (!RolePanelIds.TryParse(component.Data.CustomId, out var state))
        {
            await component.RespondAsync("I could not understand that role panel action.", ephemeral: true);
            return;
        }

        switch (state.Action)
        {
            case RolePanelAction.OpenMemberEditor:
                await OpenMemberEditorAsync(component);
                break;
            case RolePanelAction.SaveMemberRoles:
                await SaveMemberRolesAsync(component, state);
                break;
            case RolePanelAction.AddRole:
                await AddRoleAsync(component, state);
                break;
            case RolePanelAction.RemoveRole:
                await RemoveRoleAsync(component, state);
                break;
            default:
                await component.RespondAsync("I could not understand that role panel action.", ephemeral: true);
                break;
        }
    }
}
