using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace FlowBot;

public sealed class RoleMessageModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("role-message", "Creates a self-assignable role message in this channel.")]
    [RequireContext(ContextType.Guild)]
    [DefaultMemberPermissions(GuildPermission.Administrator)]
    [RequireUserPermission(GuildPermission.Administrator)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task CreateRoleMessageAsync(
        [Summary("role", "The role members can add or remove from themselves.")] SocketRole role,
        [Summary("message", "A short explanation of what this role is for.")][MaxLength(4096)] string message = "Use this role to receive its related notifications and access.",
        [Summary("title", "Optional friendly title shown instead of the role name.")][MaxLength(256)] string? title = null)
    {
        if (!SelfAssignableRoleValidator.TryValidate(Context.Guild, role, out var errorMessage))
        {
            await RespondAsync(errorMessage, ephemeral: true);
            return;
        }

        var normalizedTitle = string.IsNullOrWhiteSpace(title) ? role.Name : title.Trim();
        var normalizedMessage = string.IsNullOrWhiteSpace(message)
            ? "Use this role to receive its related notifications and access."
            : message.Trim();

        await RespondAsync("Role message created.", ephemeral: true);
        await Context.Channel.SendMessageAsync(
            embed: RoleMessageBuilder.BuildEmbed(role, normalizedTitle, normalizedMessage),
            components: RoleMessageBuilder.BuildComponents(role));
    }
}
