using Discord.WebSocket;

namespace FlowBot;

public sealed class VoiceMemberMover(ILogger<VoiceMemberMover> logger)
{
    public async Task<VoiceMoveResult> MoveAsync(
        SocketGuild guild,
        IEnumerable<SocketGuildUser> users,
        SocketVoiceChannel destinationChannel)
    {
        var connectedUsers = users
            .Where(user => !user.IsBot && user.VoiceChannel is not null)
            .DistinctBy(user => user.Id)
            .ToArray();
        var alreadyInDestination = connectedUsers
            .Where(user => user.VoiceChannel?.Id == destinationChannel.Id)
            .ToArray();
        var usersToMove = connectedUsers
            .Where(user => user.VoiceChannel?.Id != destinationChannel.Id)
            .ToArray();

        var attempts = await Task.WhenAll(
            usersToMove.Select(user => MoveUserAsync(guild, user, destinationChannel)));

        return new VoiceMoveResult(
            attempts.Where(attempt => attempt.Succeeded).Select(attempt => attempt.User).ToArray(),
            alreadyInDestination,
            attempts.Where(attempt => !attempt.Succeeded).Select(attempt => attempt.User).ToArray());
    }

    public static bool CanMoveTo(SocketGuild guild, SocketVoiceChannel destinationChannel, out string errorMessage)
    {
        var permissions = guild.CurrentUser.GetPermissions(destinationChannel);
        if (permissions.ViewChannel && permissions.Connect && permissions.MoveMembers)
        {
            errorMessage = string.Empty;
            return true;
        }

        errorMessage = $"Flowbot needs View Channel, Connect, and Move Members in {destinationChannel.Mention} before it can move users there.";
        return false;
    }

    private async Task<MoveAttempt> MoveUserAsync(
        SocketGuild guild,
        SocketGuildUser user,
        SocketVoiceChannel destinationChannel)
    {
        try
        {
            await guild.MoveAsync(user, destinationChannel);
            return new MoveAttempt(user, Succeeded: true);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to move member {UserId} to voice channel {ChannelId}.",
                user.Id,
                destinationChannel.Id);
            return new MoveAttempt(user, Succeeded: false);
        }
    }

    private sealed record MoveAttempt(SocketGuildUser User, bool Succeeded);
}
