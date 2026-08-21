using Discord;
using Discord.Interactions;

namespace FlowBot;

public sealed class HelpModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("help", "Shows what Flowbot can do.")]
    public async Task HelpAsync()
    {
        var embed = new EmbedBuilder()
            .WithTitle("Flowbot help")
            .WithDescription("A quick guide to the commands available in this server.")
            .WithColor(new Color(88, 166, 255))
            .AddField(
                "/group-finder",
                "Create a joinable group for a game or activity. Use `game-name`, then optionally add a max player count, start time, description, or role ping.")
            .AddField(
                "Group buttons",
                "`Join` and `Leave` update the player list. The group creator can use `Scramble Teams`, `Edit Time`, `Start`, `Move Players`, and `Close` to manage the group. Server admins can also use `Move Players`.")
            .AddField(
                "/guess-pull-count",
                "Admins can start a World of Warcraft boss pull-count guessing board. Members use the buttons to add, update, or remove their guesses.")
            .AddField(
                "/move-role-to-channel",
                "Admins can create controls that move connected members with a selected role to a destination and optional return voice channel.")
            .AddField(
                "/move-channel-members",
                "Admins can immediately move everyone currently connected to one voice channel into another.")
            .AddField(
                "Import Emoji",
                "Server owners and members with `Big Lord` can right-click a message with custom emojis and use `Apps > Import Emoji` to choose one, rename it, and add it to the server.")
            .AddField(
                "/import-7tv-emoji",
                "Server owners and members with `Big Lord` can import a Discord-sized 7TV emote from a link or emote ID, rename it, and add it to the server.")
            .AddField(
                "/role-panel",
                "Admins can create a compact panel for several self-assignable roles. Members manage their selection privately; admins edit the panel through `Apps > Manage Role Panel`.")
            .AddField(
                "/role-message",
                "Admins can highlight one self-assignable role with a compact add-or-remove button.")
            .AddField(
                "/roll",
                "Roll a public random number from 1-100, or choose a different maximum with `maximum`.")
            .AddField(
                "/ping",
                "Checks whether Flowbot is awake.")
            .WithFooter("Most Flowbot responses to setup and help commands are private to you.")
            .Build();

        await RespondAsync(embed: embed, ephemeral: true);
    }
}
