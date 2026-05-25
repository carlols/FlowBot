using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace FlowBot;

public sealed class RaidVoiceSplitModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("raid-voice-split", "Create admin controls for moving a raid split into a voice channel.")]
    [RequireContext(ContextType.Guild)]
    [RequireUserPermission(GuildPermission.Administrator)]
    [RequireBotPermission(GuildPermission.MoveMembers)]
    public async Task CreateRaidVoiceSplitAsync(
        [Summary("role-to-move", "Members with this role will be moved when the button is clicked.")] SocketRole roleToMove,
        [Summary("target-channel", "Voice channel the role group should be moved into.")] SocketVoiceChannel targetChannel,
        [Summary("main-channel", "Optional voice channel to move the role group back into.")] SocketVoiceChannel? mainChannel = null)
    {
        if (roleToMove.IsEveryone)
        {
            await RespondAsync("I cannot create a raid voice split for @everyone.", ephemeral: true);
            return;
        }

        if (roleToMove.IsManaged)
        {
            await RespondAsync("Managed integration or bot roles are not valid raid split roles.", ephemeral: true);
            return;
        }

        if (!CanMoveMembersTo(targetChannel, out var targetPermissionMessage))
        {
            await RespondAsync(
                targetPermissionMessage,
                ephemeral: true);
            return;
        }

        if (mainChannel is not null && !CanMoveMembersTo(mainChannel, out var mainPermissionMessage))
        {
            await RespondAsync(mainPermissionMessage, ephemeral: true);
            return;
        }

        await RespondAsync("Raid voice split controls created.", ephemeral: true);
        await Context.Channel.SendMessageAsync(
            embed: RaidVoiceSplitMessageBuilder.BuildEmbed(roleToMove, targetChannel, mainChannel),
            components: RaidVoiceSplitMessageBuilder.BuildComponents(roleToMove, targetChannel, mainChannel));
    }

    private bool CanMoveMembersTo(SocketVoiceChannel channel, out string errorMessage)
    {
        var botPermissions = Context.Guild.CurrentUser.GetPermissions(channel);
        if (botPermissions.Connect && botPermissions.MoveMembers)
        {
            errorMessage = string.Empty;
            return true;
        }

        errorMessage = $"FlowBot needs `Connect` and `Move Members` in {channel.Mention} before it can move users there.";
        return false;
    }
}
