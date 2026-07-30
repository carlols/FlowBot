using Discord;
using Discord.WebSocket;

namespace FlowBot;

public sealed class RoleVoiceMoveHandler(VoiceMemberMover voiceMemberMover)
{
    public async Task HandleAsync(SocketMessageComponent component)
    {
        if (!RoleVoiceMoveButtonIds.TryParse(component.Data.CustomId, out var state))
        {
            await component.RespondAsync("I could not identify this voice move button.", ephemeral: true);
            return;
        }

        if (!CanUseControls(component.User))
        {
            await component.RespondAsync("Only server admins can use voice move controls.", ephemeral: true);
            return;
        }

        if (state.Action == RoleVoiceMoveAction.Close)
        {
            await CloseAsync(component);
            return;
        }

        await MoveGroupAsync(component, state);
    }

    private static async Task CloseAsync(SocketMessageComponent component)
    {
        await component.DeferAsync(ephemeral: true);
        await component.Message.DeleteAsync();
        await component.FollowupAsync("Voice move controls closed.", ephemeral: true);
    }

    private async Task MoveGroupAsync(SocketMessageComponent component, RoleVoiceMoveButtonState state)
    {
        if (component.User is not SocketGuildUser adminUser)
        {
            await component.RespondAsync("Voice move controls can only be used inside a server.", ephemeral: true);
            return;
        }

        var guild = adminUser.Guild;
        var role = guild.GetRole(state.RoleId);
        var destinationChannel = guild.GetVoiceChannel(state.DestinationChannelId);

        if (role is null)
        {
            await component.RespondAsync("The configured role no longer exists.", ephemeral: true);
            return;
        }

        if (destinationChannel is null)
        {
            await component.RespondAsync("The configured destination voice channel no longer exists.", ephemeral: true);
            return;
        }

        if (!VoiceMemberMover.CanMoveTo(guild, destinationChannel, out var permissionMessage))
        {
            await component.RespondAsync(permissionMessage, ephemeral: true);
            return;
        }

        var matchingUsers = guild.VoiceChannels
            .SelectMany(channel => channel.ConnectedUsers)
            .Where(user => HasRole(user, role.Id))
            .ToArray();

        await component.DeferAsync(ephemeral: true);
        var result = await voiceMemberMover.MoveAsync(guild, matchingUsers, destinationChannel);

        await component.FollowupAsync(
            BuildMoveSummary(role, destinationChannel, result),
            ephemeral: true);
    }

    private static bool CanUseControls(SocketUser user) =>
        user is SocketGuildUser guildUser && guildUser.GuildPermissions.Administrator;

    private static bool HasRole(SocketGuildUser user, ulong roleId) =>
        user.Roles.Any(role => role.Id == roleId);

    private static string BuildMoveSummary(
        SocketRole role,
        SocketVoiceChannel destinationChannel,
        VoiceMoveResult result)
    {
        var summary = result.MovedUsers.Count > 0
            ? $"Moved {result.MovedUsers.Count} connected member(s) with {role.Mention} to {destinationChannel.Mention}."
            : $"No connected members with {role.Mention} need to be moved to {destinationChannel.Mention}.";

        if (result.AlreadyInDestination.Count > 0)
        {
            summary += $" {result.AlreadyInDestination.Count} matching member(s) were already there.";
        }

        if (result.FailedUsers.Count > 0)
        {
            var failedMentions = string.Join(", ", result.FailedUsers.Take(10).Select(user => user.Mention));
            var remainingCount = result.FailedUsers.Count - 10;
            var remainingText = remainingCount > 0 ? $", and {remainingCount} more" : string.Empty;
            summary += $" Failed to move {result.FailedUsers.Count} member(s): {failedMentions}{remainingText}.";
        }

        return summary;
    }
}
