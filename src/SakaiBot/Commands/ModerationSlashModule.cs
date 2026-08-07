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
        public async Task MuteAsync(SocketGuildUser user, int duration = 10, string reason = "No reason provided")
        {
            if (duration < 1 || duration > 40320)
            {
                await RespondAsync("Mute duration must be between 1 minute and 28 days.", ephemeral: true);
                return;
            }

            await DeferAsync(ephemeral: true);
            await user.SetTimeOutAsync(TimeSpan.FromMinutes(duration));
            await _moderationLogger.LogAsync(CreatePunishment(user.Id, Context.User.Id, PunishmentType.Mute, reason));
            await ModifyOriginalResponseAsync(properties => properties.Content = $"Muted {user.Username}#{user.Discriminator} for {duration} minutes: {reason}. Case saved.");
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
            var punishment = CreatePunishment(user.Id, Context.User.Id, PunishmentType.Warn, reason);
            await _moderationLogger.LogAsync(punishment);
            var warningCount = await _dbContext.Punishments.CountAsync(x =>
                x.GuildId == Context.Guild!.Id && x.UserId == user.Id && x.Action == PunishmentType.Warn);
            var escalation = string.Empty;
            if (warningCount >= 3)
            {
                await user.SetTimeOutAsync(TimeSpan.FromHours(1));
                escalation = " They reached 3 warnings, so they were timed out for 1 hour.";
            }

            var embed = new EmbedBuilder()
                .WithTitle("User Warned")
                .WithDescription($"{user.Mention} was warned by {Context.User.Mention}. The punishment was saved to the database.{escalation}")
                .AddField("Reason", reason)
                .AddField("Warnings", warningCount, inline: true)
                .WithColor(Color.Orange)
                .Build();

            await ModifyOriginalResponseAsync(properties => properties.Embed = embed);
        }

        [SlashCommand("modlog", "View a user's moderation history.")]
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

        [SlashCommand("clearwarnings", "Remove all warnings from a user.")]
        [RequireUserPermission(GuildPermission.ModerateMembers)]
        public async Task ClearWarningsAsync(SocketGuildUser user)
        {
            await DeferAsync(ephemeral: true);
            var warnings = await _dbContext.Punishments
                .Where(x => x.GuildId == Context.Guild!.Id && x.UserId == user.Id && x.Action == PunishmentType.Warn)
                .ToListAsync();

            if (warnings.Count == 0)
            {
                await ModifyOriginalResponseAsync(properties => properties.Content = $"{user.Mention} has no warnings.");
                return;
            }

            _dbContext.Punishments.RemoveRange(warnings);
            await _dbContext.SaveChangesAsync();
            await ModifyOriginalResponseAsync(properties => properties.Content = $"Cleared {warnings.Count} warning{(warnings.Count == 1 ? string.Empty : "s")} for {user.Mention}.");
        }

        [SlashCommand("removewarning", "Remove a warning from a user's moderation history.")]
        [RequireUserPermission(GuildPermission.ModerateMembers)]
        public async Task RemoveWarningAsync(SocketGuildUser user, string caseid)
        {
            await DeferAsync(ephemeral: true);

            var normalizedCaseId = caseid.Trim();
            var matches = await _dbContext.Punishments
                .Where(x => x.GuildId == Context.Guild!.Id
                    && x.UserId == user.Id
                    && x.Action == PunishmentType.Warn
                    && x.CaseId.StartsWith(normalizedCaseId))
                .ToListAsync();

            if (matches.Count == 0)
            {
                await ModifyOriginalResponseAsync(properties => properties.Content = "No warning matched that case ID for this user.");
                return;
            }

            if (matches.Count > 1)
            {
                await ModifyOriginalResponseAsync(properties => properties.Content = "That case ID is not specific enough. Use more characters from the case ID.");
                return;
            }

            var warning = matches[0];
            _dbContext.Punishments.Remove(warning);
            await _dbContext.SaveChangesAsync();

            await ModifyOriginalResponseAsync(properties => properties.Content = $"Removed warning case `{warning.CaseId[..8]}` from {user.Mention}.");
        }

        [SlashCommand("appeal", "Submit a moderation appeal.")]
        public async Task AppealAsync(string message)
        {
            if (Context.Guild is null)
            {
                await RespondAsync("This command can only be used in a server.", ephemeral: true);
                return;
            }

            if (message.Length < 10 || message.Length > 1000)
            {
                await RespondAsync("Your appeal must be between 10 and 1,000 characters.", ephemeral: true);
                return;
            }

            var appeal = new Appeal
            {
                GuildId = Context.Guild.Id,
                UserId = Context.User.Id,
                Message = message,
                CreatedAt = DateTime.UtcNow
            };

            await _dbContext.Appeals.AddAsync(appeal);
            await _dbContext.SaveChangesAsync();
            await RespondAsync("Your appeal has been submitted for moderator review.", ephemeral: true);
        }
    }
}
