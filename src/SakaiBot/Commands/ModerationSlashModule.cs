using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using SakaiBot.Data;
using SakaiBot.Models;
using SakaiBot.Services;
using System.Linq;
using System.Threading.Tasks;

namespace SakaiBot.Commands
{
    public class ModerationSlashModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly ModerationLogger _moderationLogger;
        private readonly AppDbContext _dbContext;

        public ModerationSlashModule(ModerationLogger moderationLogger, AppDbContext dbContext)
        {
            _moderationLogger = moderationLogger;
            _dbContext = dbContext;
        }

        private Punishment CreatePunishment(ulong userId, ulong moderatorId, PunishmentType action, string reason)
            => new()
            {
                GuildId = Context.Guild?.Id ?? 0,
                UserId = userId,
                ModeratorId = moderatorId,
                Action = action,
                Reason = string.IsNullOrWhiteSpace(reason) ? "No reason provided" : reason,
            };

        [SlashCommand("ban", "Ban a user from the server.")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        [RequireBotPermission(GuildPermission.BanMembers)]
        public async Task BanAsync(SocketGuildUser user, string reason = "No reason provided")
        {
            await DeferAsync(ephemeral: true);
            await user.BanAsync(0, reason);
            await _moderationLogger.LogAsync(CreatePunishment(user.Id, Context.User.Id, PunishmentType.Ban, reason));
            await ModifyOriginalResponseAsync(properties => properties.Content = $"Banned {user.Username}#{user.Discriminator} for: {reason}. Case saved.");
        }

        [SlashCommand("kick", "Kick a user from the server.")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        [RequireBotPermission(GuildPermission.KickMembers)]
        public async Task KickAsync(SocketGuildUser user, string reason = "No reason provided")
        {
            await DeferAsync(ephemeral: true);
            await user.KickAsync(reason);
            await _moderationLogger.LogAsync(CreatePunishment(user.Id, Context.User.Id, PunishmentType.Kick, reason));
            await ModifyOriginalResponseAsync(properties => properties.Content = $"Kicked {user.Username}#{user.Discriminator} for: {reason}. Case saved.");
        }

        [SlashCommand("mute", "Mute a user in the server.")]
        [RequireUserPermission(GuildPermission.MuteMembers)]
        [RequireBotPermission(GuildPermission.MuteMembers)]
        public async Task MuteAsync(SocketGuildUser user, string reason = "No reason provided")
        {
            await DeferAsync(ephemeral: true);
            await user.ModifyAsync(x => x.Mute = true);
            await _moderationLogger.LogAsync(CreatePunishment(user.Id, Context.User.Id, PunishmentType.Mute, reason));
            await ModifyOriginalResponseAsync(properties => properties.Content = $"Muted {user.Username}#{user.Discriminator} for: {reason}. Case saved.");
        }

        [SlashCommand("unmute", "Unmute a user in the server.")]
        [RequireUserPermission(GuildPermission.MuteMembers)]
        [RequireBotPermission(GuildPermission.MuteMembers)]
        public async Task UnmuteAsync(SocketGuildUser user)
        {
            await DeferAsync(ephemeral: true);
            await user.ModifyAsync(x => x.Mute = false);
            await _moderationLogger.LogAsync(CreatePunishment(user.Id, Context.User.Id, PunishmentType.Unmute, "Unmuted user"));
            await ModifyOriginalResponseAsync(properties => properties.Content = $"Unmuted {user.Username}#{user.Discriminator}. Case saved.");
        }

        [SlashCommand("clear", "Delete a number of messages from the channel.")]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        [RequireBotPermission(GuildPermission.ManageMessages)]
        public async Task ClearAsync(int count)
        {
            if (count <= 0 || count > 100)
            {
                await RespondAsync("Please specify a number between 1 and 100.", ephemeral: true);
                return;
            }

            var messages = await Context.Channel.GetMessagesAsync(count + 1).FlattenAsync();
            await ((ITextChannel)Context.Channel).DeleteMessagesAsync(messages);
            await RespondAsync($"Deleted {messages.Count()} messages.", ephemeral: true);
        }

        [SlashCommand("warn", "Warn a user with a reason.")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task WarnAsync(SocketGuildUser user, string reason = "No reason provided")
        {
            await DeferAsync(ephemeral: true);
            await _moderationLogger.LogAsync(CreatePunishment(user.Id, Context.User.Id, PunishmentType.Warn, reason));
            var embed = new EmbedBuilder()
                .WithTitle("User Warned")
                .WithDescription($"{user.Mention} was warned by {Context.User.Mention}. The punishment was saved to the database.")
                .AddField("Reason", reason)
                .WithColor(Color.Orange)
                .Build();

            await ModifyOriginalResponseAsync(properties => properties.Embed = embed);
        }

        [SlashCommand("punishments", "View a user's moderation history.")]
        [RequireUserPermission(GuildPermission.ModerateMembers)]
        public async Task PunishmentsAsync(SocketGuildUser user)
        {
            await DeferAsync(ephemeral: true);

            var punishments = await _dbContext.Punishments
                .AsNoTracking()
                .Where(x => x.GuildId == Context.Guild!.Id && x.UserId == user.Id)
                .OrderByDescending(x => x.CreatedAt)
                .Take(25)
                .ToListAsync();

            if (punishments.Count == 0)
            {
                await ModifyOriginalResponseAsync(properties => properties.Content = $"{user.Mention} has no recorded punishments in this server.");
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle($"Punishment history: {user.Username}")
                .WithDescription($"Showing the {punishments.Count} most recent case{(punishments.Count == 1 ? string.Empty : "s")}.")
                .WithColor(Color.DarkRed)
                .WithCurrentTimestamp();

            foreach (var punishment in punishments)
            {
                var moderator = Context.Guild.GetUser(punishment.ModeratorId);
                var moderatorName = moderator?.Username ?? $"<@{punishment.ModeratorId}>";
                embed.AddField(
                    $"Case {punishment.CaseId[..8]} | {punishment.Action}",
                    $"Reason: {punishment.Reason}\nModerator: {moderatorName}\nDate: <t:{new DateTimeOffset(punishment.CreatedAt).ToUnixTimeSeconds()}:f>",
                    inline: false);
            }

            await ModifyOriginalResponseAsync(properties => properties.Embed = embed.Build());
        }
    }
}
