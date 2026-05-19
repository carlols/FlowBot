using Discord;
using Discord.Interactions;

namespace FlowBot;

public sealed class HelpModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("help", "Shows what FlowBot can do.")]
    public async Task HelpAsync()
    {
        var embed = new EmbedBuilder()
            .WithTitle("FlowBot help")
            .WithDescription("A quick guide to the commands available in this server.")
            .WithColor(new Color(88, 166, 255))
            .AddField(
                "/group-finder",
                "Create a joinable group for a game or activity. Use `game-name`, then optionally add a max player count, start time, description, or role ping.")
            .AddField(
                "Group buttons",
                "`Join` and `Leave` update the player list. The group creator can use `Start` to mention everyone registered, or `Close` to remove the group.")
            .AddField(
                "/guess-pull-count",
                "Admins can start a World of Warcraft boss pull-count guessing board. Members use the buttons to add, update, or remove their guesses.")
            .AddField(
                "Import Emoji",
                "Server owners can right-click a message with custom emojis and use `Apps > Import Emoji` to choose one, rename it, and add it to the server.")
            .AddField(
                "/role-message",
                "Admins can create a message that lets members add or remove a server role from themselves.")
            .AddField(
                "/ping",
                "Checks whether FlowBot is awake.")
            .WithFooter("Most FlowBot responses to setup and help commands are private to you.")
            .Build();

        await RespondAsync(embed: embed, ephemeral: true);
    }
}
