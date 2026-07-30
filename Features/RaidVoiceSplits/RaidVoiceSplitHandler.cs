using Discord;
using Discord.WebSocket;

namespace FlowBot;

public sealed class RaidVoiceSplitHandler(ILogger<RaidVoiceSplitHandler> logger)
{
    public async Task HandleAsync(SocketMessageComponent component)
    {
        if (!RaidVoiceSplitButtonIds.TryParse(component.Data.CustomId, out var state))
        {
            await component.RespondAsync("I could not identify this raid voice split button.", ephemeral: true);
            return;
        }

        if (!CanUseControls(component.User))
        {
            await component.RespondAsync("Only server admins can use raid voice split controls.", ephemeral: true);
            return;
        }

        if (state.Action == RaidVoiceSplitAction.Close)
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
        await component.FollowupAsync("Raid voice split controls closed.", ephemeral: true);
    }

    private async Task MoveGroupAsync(SocketMessageComponent component, RaidVoiceSplitButtonState state)
    {
        if (component.User is not SocketGuildUser adminUser)
        {
            await component.RespondAsync("Raid voice split controls can only be used inside a server.", ephemeral: true);
            return;
        }

        var guild = adminUser.Guild;
        var role = guild.GetRole(state.RoleId);
        var targetChannel = guild.GetVoiceChannel(state.TargetChannelId);

        if (role is null)
        {
            await component.RespondAsync("The configured raid split role no longer exists.", ephemeral: true);
            return;
        }

        if (targetChannel is null)
        {
            await component.RespondAsync("The configured target voice channel no longer exists.", ephemeral: true);
            return;
        }

        if (guild.CurrentUser is { } currentUser)
        {
            var targetPermissions = currentUser.GetPermissions(targetChannel);
            if (!targetPermissions.Connect || !targetPermissions.MoveMembers)
            {
                await component.RespondAsync(
                    $"Flowbot needs `Connect` and `Move Members` in {targetChannel.Mention} before it can move users there.",
                    ephemeral: true);
                return;
            }
        }

        var connectedUsers = guild.VoiceChannels
            .SelectMany(channel => channel.ConnectedUsers)
            .Where(user => !user.IsBot)
            .DistinctBy(user => user.Id)
            .ToArray();

        var alreadyInTarget = connectedUsers
            .Where(user => user.VoiceChannel?.Id == targetChannel.Id && HasRole(user, role.Id))
            .ToArray();

        var usersToMove = connectedUsers
            .Where(user => user.VoiceChannel?.Id != targetChannel.Id && HasRole(user, role.Id))
            .ToArray();

        if (usersToMove.Length == 0)
        {
            var alreadyText = alreadyInTarget.Length == 0
                ? string.Empty
                : $" {alreadyInTarget.Length} matching member(s) are already in {targetChannel.Mention}.";
            await component.RespondAsync(
                $"No connected members with {role.Mention} need to be moved.{alreadyText}",
                ephemeral: true);
            return;
        }

        await component.DeferAsync(ephemeral: true);

        var movedUsers = new List<SocketGuildUser>();
        var failedUsers = new List<SocketGuildUser>();

        foreach (var user in usersToMove)
        {
            try
            {
                await guild.MoveAsync(user, targetChannel);
                movedUsers.Add(user);
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Failed to move raid split member {UserId} to voice channel {ChannelId}.",
                    user.Id,
                    targetChannel.Id);
                failedUsers.Add(user);
            }
        }

        await component.FollowupAsync(
            BuildMoveSummary(role, targetChannel, movedUsers, alreadyInTarget, failedUsers),
            ephemeral: true);
    }

    private static bool CanUseControls(SocketUser user) =>
        user is SocketGuildUser guildUser && guildUser.GuildPermissions.Administrator;

    private static bool HasRole(SocketGuildUser user, ulong roleId) =>
        user.Roles.Any(role => role.Id == roleId);

    private static string BuildMoveSummary(
        SocketRole role,
        SocketVoiceChannel targetChannel,
        IReadOnlyCollection<SocketGuildUser> movedUsers,
        IReadOnlyCollection<SocketGuildUser> alreadyInTarget,
        IReadOnlyCollection<SocketGuildUser> failedUsers)
    {
        var summary = $"Moved {movedUsers.Count} connected member(s) with {role.Mention} to {targetChannel.Mention}.";

        if (alreadyInTarget.Count > 0)
        {
            summary += $" {alreadyInTarget.Count} matching member(s) were already there.";
        }

        if (failedUsers.Count > 0)
        {
            summary += $" Failed to move {failedUsers.Count} member(s): {string.Join(", ", failedUsers.Select(user => user.Mention))}.";
        }

        return summary;
    }
}
