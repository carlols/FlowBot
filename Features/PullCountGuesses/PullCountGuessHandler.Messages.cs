using Discord;
using Discord.WebSocket;

namespace FlowBot;

public sealed partial class PullCountGuessHandler
{
    private static async Task<(IUserMessage Message, PullCountGuessSession Session)?> LoadCurrentBoardAsync(
        IMessageChannel channel,
        ulong messageId)
    {
        var message = await channel.GetMessageAsync(messageId, CacheMode.AllowDownload);

        return message is IUserMessage userMessage
            && PullCountGuessMessageBuilder.TryReadSession(userMessage, out var session)
                ? (userMessage, session)
                : null;
    }

    private static Task UpdateBoardAsync(IUserMessage message, PullCountGuessSession session) =>
        message.ModifyAsync(properties =>
        {
            properties.Embed = PullCountGuessMessageBuilder.BuildEmbed(session);
            properties.Components = PullCountGuessMessageBuilder.BuildComponents(session);
        });

    private static Task UpdateEphemeralResponseAsync(SocketMessageComponent component, string content) =>
        component.UpdateAsync(properties =>
        {
            properties.Content = content;
            properties.Components = new ComponentBuilder().Build();
        });
}
