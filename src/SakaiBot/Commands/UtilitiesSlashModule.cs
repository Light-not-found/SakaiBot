using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System;
using System.Threading.Tasks;

namespace SakaiBot.Commands
{
    public class UtilitiesSlashModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("userinfo", "Show information about a member.")]
        public async Task UserInfoAsync(SocketGuildUser user)
        {
            var embed = new EmbedBuilder()
                .WithTitle($"User info: {user.Username}")
                .WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                .AddField("User ID", user.Id, inline: true)
                .AddField("Joined", user.JoinedAt?.ToString("yyyy-MM-dd") ?? "Unknown", inline: true)
                .AddField("Account created", user.CreatedAt.ToString("yyyy-MM-dd"), inline: true)
                .WithColor(user.Roles.Count > 0 ? Color.Blue : Color.LightGrey)
                .Build();

            await RespondAsync(embed: embed, ephemeral: false);
        }

        [SlashCommand("serverinfo", "Show information about this server.")]
        public async Task ServerInfoAsync()
        {
            if (Context.Guild is null)
            {
                await RespondAsync("This command can only be used in a server.", ephemeral: true);
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle(Context.Guild.Name)
                .WithThumbnailUrl(Context.Guild.IconUrl)
                .AddField("Server ID", Context.Guild.Id, inline: true)
                .AddField("Members", Context.Guild.MemberCount, inline: true)
                .AddField("Created", Context.Guild.CreatedAt.ToString("yyyy-MM-dd"), inline: true)
                .WithColor(Color.Blue)
                .Build();

            await RespondAsync(embed: embed, ephemeral: false);
        }

        [SlashCommand("avatar", "Show a member's avatar.")]
        public async Task AvatarAsync(SocketGuildUser? user = null)
        {
            user ??= Context.User as SocketGuildUser;
            var avatarUrl = user?.GetAvatarUrl(ImageFormat.Auto, 1024) ?? user?.GetDefaultAvatarUrl();
            await RespondAsync(embed: new EmbedBuilder()
                .WithTitle($"{user?.Username ?? Context.User.Username}'s avatar")
                .WithImageUrl(avatarUrl)
                .WithColor(Color.Blue)
                .Build());
        }

        [SlashCommand("poll", "Create a simple yes/no poll.")]
        public async Task PollAsync(string question)
        {
            if (question.Length < 3 || question.Length > 300)
            {
                await RespondAsync("The poll question must be between 3 and 300 characters.", ephemeral: true);
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle("Poll")
                .WithDescription(question)
                .AddField("Vote", "Use the reactions below to vote.")
                .WithColor(Color.Blue)
                .WithFooter("Yes: ✅   No: ❌")
                .Build();

            await RespondAsync(embed: embed);
            var message = await GetOriginalResponseAsync();
            await message.AddReactionAsync(new Emoji("✅"));
            await message.AddReactionAsync(new Emoji("❌"));
        }
    }
}
