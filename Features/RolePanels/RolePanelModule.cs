using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace FlowBot;

public sealed class RolePanelModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("role-panel", "Creates a panel where members can privately manage several roles.")]
    [RequireContext(ContextType.Guild)]
    [DefaultMemberPermissions(GuildPermission.Administrator)]
    [RequireUserPermission(GuildPermission.Administrator)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task CreateRolePanelAsync(
        [Summary("first-role", "The first self-assignable role in this panel.")] SocketRole firstRole,
        [Summary("title", "The heading shown on the panel.")][MaxLength(256)] string title = "Choose your roles",
        [Summary("description", "Optional guidance shown above the role list.")][MaxLength(2048)] string? description = null)
    {
        if (!SelfAssignableRoleValidator.TryValidate(Context.Guild, firstRole, out var errorMessage))
        {
            await RespondAsync(errorMessage, ephemeral: true);
            return;
        }

        var normalizedTitle = string.IsNullOrWhiteSpace(title) ? "Choose your roles" : title.Trim();
        var normalizedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        var panel = new RolePanel(normalizedTitle, normalizedDescription, [firstRole.Id]);

        await RespondAsync(
            "Role panel created. Right-click it and choose `Apps > Manage Role Panel` to add or remove roles.",
            ephemeral: true);
        await Context.Channel.SendMessageAsync(
            embed: RolePanelMessageBuilder.BuildEmbed(panel),
            components: RolePanelMessageBuilder.BuildComponents());
    }

    [MessageCommand("Manage Role Panel")]
    [RequireContext(ContextType.Guild)]
    [DefaultMemberPermissions(GuildPermission.Administrator)]
    [RequireUserPermission(GuildPermission.Administrator)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task ManageRolePanelAsync(IMessage message)
    {
        if (message.Author.Id != Context.Client.CurrentUser.Id
            || !RolePanelMessageParser.TryParse(message, out var panel))
        {
            await RespondAsync("That message is not a Flowbot role panel.", ephemeral: true);
            return;
        }

        await RespondAsync(
            $"Editing **{panel.Title}**. Changes update the panel directly and are only reported here.",
            components: RolePanelMenuBuilder.BuildAdminEditor(
                panel,
                Context.Guild,
                message.Channel.Id,
                message.Id),
            ephemeral: true);
    }
}
