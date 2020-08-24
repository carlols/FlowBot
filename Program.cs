using DSharpPlus;
using DSharpPlus.EventArgs;
using DSharpPlus.CommandsNext;
using System;
using System.Threading.Tasks;

namespace Flowbot
{
    public class Program
    {
        static CommandsNextModule commands;

        public static async Task Main(string[] args)
        {
            var discordClient = new DiscordClient(new DiscordConfiguration
            {
                Token = "NjUyOTcwODMwNzMyNTkxMTA2.XewMeQ.Grem8JxcVc7-eSbdhgCtlKaw4gc",
                TokenType = TokenType.Bot,
                UseInternalLogHandler = true,
                LogLevel = LogLevel.Debug
            });

            commands = discordClient.UseCommandsNext(new CommandsNextConfiguration
            {
                StringPrefix = ";;"
            });


            discordClient.MessageCreated += OnMessageCreated;

            commands.RegisterCommands<RoleCommands>();

            await discordClient.ConnectAsync();
            await Task.Delay(-1);
        }

        private static async Task OnMessageCreated(MessageCreateEventArgs e)
        {
            if (string.Equals(e.Message.Content, "hello", StringComparison.OrdinalIgnoreCase))
            {
                await e.Message.RespondAsync(e.Message.Author.Username);
            }
        }
    }
}
