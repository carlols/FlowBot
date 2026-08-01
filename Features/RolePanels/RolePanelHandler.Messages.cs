using Discord;
using Discord.WebSocket;

namespace FlowBot;

public sealed partial class RolePanelHandler
{
    private static async Task<(IUserMessage Message, RolePanel Panel)?> LoadPanelAsync(
        SocketGuild guild,
        ulong channelId,
        ulong messageId)
    {
        if (guild.GetChannel(channelId) is not IMessageChannel channel)
        {
            return null;
        }

        var message = await channel.GetMessageAsync(messageId, CacheMode.AllowDownload);

        return message is IUserMessage userMessage
            && RolePanelMessageParser.TryParse(userMessage, out var panel)
                ? (userMessage, panel)
                : null;
    }

    private static async Task RestoreAdminEditorAsync(
        SocketMessageComponent component,
        RolePanelInteractionState state,
        SocketGuild guild,
        string content)
    {
        var loadedPanel = await LoadPanelAsync(guild, state.ChannelId, state.MessageId);

        await ModifyEphemeralResponseAsync(
            component,
            content,
            loadedPanel is null
                ? null
                : RolePanelMenuBuilder.BuildAdminEditor(
                    loadedPanel.Value.Panel,
                    guild,
                    state.ChannelId,
                    state.MessageId));
    }

    private static Task UpdatePanelMessageAsync(IUserMessage message, RolePanel panel) =>
        message.ModifyAsync(properties =>
        {
            properties.Embed = RolePanelMessageBuilder.BuildEmbed(panel);
            properties.Components = RolePanelMessageBuilder.BuildComponents();
        });

    private static Task UpdateEphemeralResponseAsync(
        SocketMessageComponent component,
        string content) =>
        component.UpdateAsync(properties =>
        {
            properties.Content = content;
            properties.Components = new ComponentBuilder().Build();
        });

    private static Task ModifyEphemeralResponseAsync(
        SocketMessageComponent component,
        string content,
        MessageComponent? components = null) =>
        component.ModifyOriginalResponseAsync(properties =>
        {
            properties.Content = content;
            properties.Components = components ?? new ComponentBuilder().Build();
        });
}
