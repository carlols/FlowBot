using Discord.Interactions;
using Discord.WebSocket;

namespace FlowBot;

public sealed class DiscordInteractionRouter(
    DiscordSocketClient client,
    InteractionService interactions,
    RoleButtonHandler roleButtonHandler,
    RolePanelHandler rolePanelHandler,
    GroupFinderButtonHandler groupFinderButtonHandler,
    PullCountGuessHandler pullCountGuessHandler,
    EmojiImportHandler emojiImportHandler,
    RoleVoiceMoveHandler roleVoiceMoveHandler,
    IServiceProvider services,
    ILogger<DiscordInteractionRouter> logger)
{
    public async Task RouteAsync(SocketInteraction interaction)
    {
        if (interaction is SocketMessageComponent component)
        {
            if (RolePanelIds.IsRolePanelInteraction(component.Data.CustomId))
            {
                await rolePanelHandler.HandleAsync(component);
                return;
            }

            if (RoleButtonIds.IsRoleButton(component.Data.CustomId))
            {
                await roleButtonHandler.HandleAsync(component);
                return;
            }

            if (GroupFinderButtonIds.IsGroupFinderButton(component.Data.CustomId))
            {
                await groupFinderButtonHandler.HandleAsync(component);
                return;
            }

            if (PullCountGuessIds.IsPullCountGuessInteraction(component.Data.CustomId))
            {
                await pullCountGuessHandler.HandleComponentAsync(component);
                return;
            }

            if (EmojiImportIds.IsEmojiImportInteraction(component.Data.CustomId))
            {
                await emojiImportHandler.HandleComponentAsync(component);
                return;
            }

            if (RoleVoiceMoveButtonIds.IsRoleVoiceMoveButton(component.Data.CustomId))
            {
                await roleVoiceMoveHandler.HandleAsync(component);
                return;
            }
        }

        if (interaction is SocketModal modal)
        {
            if (GroupFinderButtonIds.IsGroupFinderButton(modal.Data.CustomId))
            {
                await groupFinderButtonHandler.HandleModalAsync(modal);
                return;
            }

            if (PullCountGuessIds.IsPullCountGuessInteraction(modal.Data.CustomId))
            {
                await pullCountGuessHandler.HandleModalAsync(modal);
                return;
            }

            if (EmojiImportIds.IsEmojiImportInteraction(modal.Data.CustomId))
            {
                await emojiImportHandler.HandleModalAsync(modal);
                return;
            }
        }

        var context = new SocketInteractionContext(client, interaction);
        var result = await interactions.ExecuteCommandAsync(context, services);

        if (!result.IsSuccess)
        {
            logger.LogWarning("Interaction failed: {Error} {Reason}", result.Error, result.ErrorReason);
        }
    }
}
