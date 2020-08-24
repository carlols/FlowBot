using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;

namespace Flowbot
{
    public class RoleCommands
    {
        private List<string> _allowedRoles = new List<string> { "Juicer", "Juicer2", "Juicer3" };

        [Command("hi")]
        public async Task Hi(CommandContext ctx)
        {

            await ctx.RespondAsync($"👋 Hi, {ctx.User.Mention}!. We are in a server called {ctx.Guild.Name}. The owner of the server is {ctx.Guild.Owner.Username}");
        }

        [Command("roles")]
        public async Task ListRoles(CommandContext ctx)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Roles that you can join/leave");

            for (int i = 1; i < Enum.GetNames(typeof(OkRoles)).Length + 1; i++)
            {
                sb.AppendLine(@$"{i}. {(OkRoles)i}");
            }

            await ctx.RespondAsync(sb.ToString());
        }

        private string GetRoleListString()
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 1; i < Enum.GetNames(typeof(OkRoles)).Length + 1; i++)
            {
                sb.AppendLine(@$"{i}. {(OkRoles)i}");
            }

            return sb.ToString();
        }

        [Command("join")]
        public async Task JoinRole(CommandContext ctx, string roleName)
        {
            var roles = ctx.Guild.Roles;
            var member = ctx.Member;

            if (Int32.TryParse(roleName, out int roleNumber))
            {
                var role = ctx.Guild.Roles.FirstOrDefault(x => x.Name == ((OkRoles)roleNumber).ToString());

                if (role == null)
                {
                    await ctx.RespondAsync($"{ctx.Member.Mention} the role you input "); //TODO: cont here
                }
            }
            else
            {
                var role = ctx.Guild.Roles.FirstOrDefault(x => x.Name == roleName);

                if (role != null && _allowedRoles.Any(x => x == role.Name))
                {
                    try
                    {
                        await ctx.Guild.GrantRoleAsync(member, role);
                        await ctx.RespondAsync($"{ctx.Member.Mention} You have been granted the role {roleName}");
                    }
                    catch (System.Exception e)
                    {
                        await ctx.RespondAsync($"Something went wrong when assigning the role to you. {e.Message}");
                    }

                }
                else
                {
                    await ctx.RespondAsync($"There is no role called {roleName} or you tried to set a prohibited role.");

                    await ctx.RespondAsync($"Allowed roles: ");
                    foreach (var item in _allowedRoles)
                    {
                        await ctx.RespondAsync(item);
                    }
                }
            }




        }

        [Command("leave")]
        public async Task LeaveRole(CommandContext ctx, string roleName)
        {
            var roles = ctx.Guild.Roles;
            var member = ctx.Member;
            var role = ctx.Guild.Roles.FirstOrDefault(x => x.Name == roleName);

            if (role != null && _allowedRoles.Any(x => x == role.Name))
            {
                try
                {
                    await ctx.Guild.GrantRoleAsync(member, role);
                    await ctx.RespondAsync($"{ctx.Member.Mention} You have been granted the role {roleName}");
                }
                catch (System.Exception e)
                {
                    await ctx.RespondAsync($"Something went wrong when assigning the role to you. {e.Message}");
                }

            }
            else
            {
                await ctx.RespondAsync($"There is no role called {roleName} or you tried to remove a non-changeable role.");

                await ctx.RespondAsync($"Your changeable roles: ");
                foreach (var item in _allowedRoles)
                {
                    await ctx.RespondAsync(item);
                }
            }
        }

        [Command("roleinfo")]
        public async Task RoleInfo(CommandContext ctx)
        {
            var roles = ctx.Guild.Roles;

            foreach (var item in roles)
            {
                await ctx.RespondAsync($"{ item.Name}");
            }
        }
    }
}
