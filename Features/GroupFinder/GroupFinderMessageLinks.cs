using Discord.WebSocket;

namespace FlowBot;

public static class GroupFinderMessageLinks
{
    public static string? Create(SocketMessageComponent component)
    {
        if (component.Channel is not SocketGuildChannel guildChannel)
        {
            return null;
        }

        return $"https://discord.com/channels/{guildChannel.Guild.Id}/{component.Channel.Id}/{component.Message.Id}";
    }
}
