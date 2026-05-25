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
        [Summary("target-channel", "Voice channel the role group should be moved into.")] SocketVoiceChannel targetChannel)
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

        var botPermissions = Context.Guild.CurrentUser.GetPermissions(targetChannel);
        if (!botPermissions.Connect || !botPermissions.MoveMembers)
        {
            await RespondAsync(
                $"FlowBot needs `Connect` and `Move Members` in {targetChannel.Mention} before it can move users there.",
                ephemeral: true);
            return;
        }

        await RespondAsync("Raid voice split controls created.", ephemeral: true);
        await Context.Channel.SendMessageAsync(
            embed: RaidVoiceSplitMessageBuilder.BuildEmbed(roleToMove, targetChannel),
            components: RaidVoiceSplitMessageBuilder.BuildComponents(roleToMove, targetChannel));
    }
}
