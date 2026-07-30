using Discord.WebSocket;

namespace FlowBot;

public static class EmojiImportPermissions
{
    public const string ImportRoleName = "Big Lord";

    public static string DeniedMessage =>
        $"Only the server owner or members with the `{ImportRoleName}` role can import emojis with Flowbot.";

    public static bool CanImportEmojis(SocketGuild guild, SocketUser user)
    {
        if (user.Id == guild.OwnerId)
        {
            return true;
        }

        return user is SocketGuildUser guildUser
            && guildUser.Roles.Any(role => string.Equals(role.Name, ImportRoleName, StringComparison.Ordinal));
    }
}
