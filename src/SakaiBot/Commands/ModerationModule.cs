using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System.Linq;
using System.Threading.Tasks;

namespace SakaiBot.Commands
{
    public class ModerationModule : ModuleBase<SocketCommandContext>
    {
        [Command("ban")]
        [Summary("Bans a user from the server.")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        [RequireBotPermission(GuildPermission.BanMembers)]
        public async Task BanAsync(SocketGuildUser user, [Remainder] string reason = "No reason provided")
        {
            if (user == null)
            {
                await ReplyAsync("Please specify a user to ban.");
                return;
            }

            await user.BanAsync(0, reason);
            await ReplyAsync($"Banned {user.Username}#{user.Discriminator} for: {reason}");
        }

        [Command("kick")]
        [Summary("Kicks a user from the server.")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        [RequireBotPermission(GuildPermission.KickMembers)]
        public async Task KickAsync(SocketGuildUser user, [Remainder] string reason = "No reason provided")
        {
            if (user == null)
            {
                await ReplyAsync("Please specify a user to kick.");
                return;
            }

            await user.KickAsync(reason);
            await ReplyAsync($"Kicked {user.Username}#{user.Discriminator} for: {reason}");
        }

        [Command("mute")]
        [Summary("Mutes a user by setting their mute status.")]
        [RequireUserPermission(GuildPermission.MuteMembers)]
        [RequireBotPermission(GuildPermission.MuteMembers)]
        public async Task MuteAsync(SocketGuildUser user, [Remainder] string reason = "No reason provided")
        {
            if (user == null)
            {
                await ReplyAsync("Please specify a user to mute.");
                return;
            }

            await user.ModifyAsync(x => x.Mute = true);
            await ReplyAsync($"Muted {user.Username}#{user.Discriminator} for: {reason}");
        }

        [Command("unmute")]
        [Summary("Unmutes a user.")]
        [RequireUserPermission(GuildPermission.MuteMembers)]
        [RequireBotPermission(GuildPermission.MuteMembers)]
        public async Task UnmuteAsync(SocketGuildUser user)
        {
            if (user == null)
            {
                await ReplyAsync("Please specify a user to unmute.");
                return;
            }

            await user.ModifyAsync(x => x.Mute = false);
            await ReplyAsync($"Unmuted {user.Username}#{user.Discriminator}.");
        }

        [Command("clear")]
        [Summary("Deletes a number of messages from the current channel.")]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        [RequireBotPermission(GuildPermission.ManageMessages)]
        public async Task ClearAsync(int count)
        {
            if (count <= 0 || count > 100)
            {
                await ReplyAsync("Please specify a number between 1 and 100.");
                return;
            }

            var messages = await Context.Channel.GetMessagesAsync(count + 1).FlattenAsync();
            await ((ITextChannel)Context.Channel).DeleteMessagesAsync(messages);
            await ReplyAsync($"Deleted {messages.Count()} messages.");
        }

        [Command("warn")]
        [Summary("Warns a member with a reason.")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task WarnAsync(SocketGuildUser user, [Remainder] string reason = "No reason provided")
        {
            if (user == null)
            {
                await ReplyAsync("Please specify a user to warn.");
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle("User Warned")
                .WithDescription($"{user.Mention} was warned by {Context.User.Mention}.")
                .AddField("Reason", reason)
                .WithColor(Color.Orange)
                .Build();

            await ReplyAsync(embed: embed);
        }
    }
}
