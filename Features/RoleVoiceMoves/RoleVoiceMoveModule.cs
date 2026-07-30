using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace FlowBot;

public sealed class RoleVoiceMoveModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("move-role-to-channel", "Create admin controls for moving a role's connected members between voice channels.")]
    [RequireContext(ContextType.Guild)]
    [RequireUserPermission(GuildPermission.Administrator)]
    [RequireBotPermission(GuildPermission.MoveMembers)]
    public async Task CreateRoleVoiceMoveAsync(
        [Summary("role", "Members with this role will be moved when a button is clicked.")] SocketRole role,
        [Summary("destination-channel", "Voice channel the role members should be moved into.")] SocketVoiceChannel destinationChannel,
        [Summary("return-channel", "Optional voice channel to move the role members back into.")] SocketVoiceChannel? returnChannel = null)
    {
        if (role.IsEveryone)
        {
            await RespondAsync("I cannot create voice move controls for @everyone.", ephemeral: true);
            return;
        }

        if (role.IsManaged)
        {
            await RespondAsync("Managed integration or bot roles cannot be used for voice moves.", ephemeral: true);
            return;
        }

        if (returnChannel?.Id == destinationChannel.Id)
        {
            await RespondAsync("The destination and return channels must be different.", ephemeral: true);
            return;
        }

        if (!VoiceMemberMover.CanMoveTo(Context.Guild, destinationChannel, out var destinationPermissionMessage))
        {
            await RespondAsync(destinationPermissionMessage, ephemeral: true);
            return;
        }

        if (returnChannel is not null
            && !VoiceMemberMover.CanMoveTo(Context.Guild, returnChannel, out var returnPermissionMessage))
        {
            await RespondAsync(returnPermissionMessage, ephemeral: true);
            return;
        }

        await DeferAsync(ephemeral: true);
        await Context.Channel.SendMessageAsync(
            embed: RoleVoiceMoveMessageBuilder.BuildEmbed(role, destinationChannel, returnChannel),
            components: RoleVoiceMoveMessageBuilder.BuildComponents(role, destinationChannel, returnChannel));
        await FollowupAsync("Voice move controls created.", ephemeral: true);
    }

}
