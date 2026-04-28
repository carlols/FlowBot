using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace FlowBot;

public sealed class RoleMessageModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("role-message", "Creates a self-assignable role message in this channel.")]
    [RequireContext(ContextType.Guild)]
    [RequireUserPermission(GuildPermission.Administrator)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task CreateRoleMessageAsync(
        [Summary("role", "The role users will receive when they click the button.")] SocketRole role,
        [Summary("message", "The message FlowBot should post above the button.")] string message = "Click the button below to receive this role.")
    {
        if (role.IsEveryone)
        {
            await RespondAsync("I cannot create a self-assign button for @everyone.", ephemeral: true);
            return;
        }

        if (role.IsManaged)
        {
            await RespondAsync("I cannot assign managed integration or bot roles.", ephemeral: true);
            return;
        }

        if (role.Position >= Context.Guild.CurrentUser.Hierarchy)
        {
            await RespondAsync(
                $"I cannot assign {role.Mention} because it is at or above my highest role. Move FlowBot's role above it and try again.",
                ephemeral: true);
            return;
        }

        var component = new ComponentBuilder()
            .WithButton(
                label: $"Get {role.Name}",
                customId: RoleButtonIds.CreateAddRoleId(role.Id),
                style: ButtonStyle.Success)
            .WithButton(
                label: $"Remove {role.Name}",
                customId: RoleButtonIds.CreateRemoveRoleId(role.Id),
                style: ButtonStyle.Danger)
            .Build();

        var embed = new EmbedBuilder()
            .WithTitle(role.Name)
            .WithDescription(message)
            .AddField("Assignable role", role.Mention, inline: true)
            .WithColor(new Color(88, 101, 242))
            .WithFooter("Use the buttons below to add or remove this role.")
            .Build();

        await RespondAsync("Role message created.", ephemeral: true);
        await Context.Channel.SendMessageAsync(embed: embed, components: component);
    }
}
