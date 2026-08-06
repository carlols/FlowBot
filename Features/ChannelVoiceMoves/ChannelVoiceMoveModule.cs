using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace FlowBot;

public sealed class ChannelVoiceMoveModule(VoiceMemberMover voiceMemberMover)
    : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("move-channel-members", "Move everyone currently connected to one voice channel into another.")]
    [RequireContext(ContextType.Guild)]
    [RequireUserPermission(GuildPermission.Administrator)]
    [RequireBotPermission(GuildPermission.MoveMembers)]
    public async Task MoveChannelMembersAsync(
        [Summary("from-channel", "Voice channel whose current members should be moved.")] SocketVoiceChannel sourceChannel,
        [Summary("to-channel", "Voice channel the current members should be moved into.")] SocketVoiceChannel destinationChannel)
    {
        if (sourceChannel.Id == destinationChannel.Id)
        {
            await RespondAsync("The source and destination channels must be different.", ephemeral: true);
            return;
        }

        if (!CanMoveFrom(Context.Guild, sourceChannel, out var sourcePermissionMessage))
        {
            await RespondAsync(sourcePermissionMessage, ephemeral: true);
            return;
        }

        if (!VoiceMemberMover.CanMoveTo(Context.Guild, destinationChannel, out var destinationPermissionMessage))
        {
            await RespondAsync(destinationPermissionMessage, ephemeral: true);
            return;
        }

        await DeferAsync(ephemeral: true);

        var membersToMove = sourceChannel.ConnectedUsers
            .Where(user => !user.IsBot)
            .ToArray();

        if (membersToMove.Length == 0)
        {
            await FollowupAsync($"There are no members to move from {sourceChannel.Mention}.", ephemeral: true);
            return;
        }

        var result = await voiceMemberMover.MoveAsync(Context.Guild, membersToMove, destinationChannel);

        await FollowupAsync(
            BuildMoveSummary(sourceChannel, destinationChannel, result),
            ephemeral: true);
    }

    private static bool CanMoveFrom(
        SocketGuild guild,
        SocketVoiceChannel sourceChannel,
        out string errorMessage)
    {
        var permissions = guild.CurrentUser.GetPermissions(sourceChannel);
        if (permissions.ViewChannel && permissions.MoveMembers)
        {
            errorMessage = string.Empty;
            return true;
        }

        errorMessage = $"Flowbot needs View Channel and Move Members in {sourceChannel.Mention} before it can move members from there.";
        return false;
    }

    private static string BuildMoveSummary(
        SocketVoiceChannel sourceChannel,
        SocketVoiceChannel destinationChannel,
        VoiceMoveResult result)
    {
        var summary = result.MovedUsers.Count > 0
            ? $"Moved {result.MovedUsers.Count} member(s) from {sourceChannel.Mention} to {destinationChannel.Mention}."
            : $"No members were moved from {sourceChannel.Mention} to {destinationChannel.Mention}.";

        if (result.FailedUsers.Count > 0)
        {
            var failedMentions = string.Join(", ", result.FailedUsers.Take(10).Select(user => user.Mention));
            var remainingCount = result.FailedUsers.Count - 10;
            var remainingText = remainingCount > 0 ? $", and {remainingCount} more" : string.Empty;
            summary += $" Failed to move {result.FailedUsers.Count}: {failedMentions}{remainingText}.";
        }

        return summary;
    }
}