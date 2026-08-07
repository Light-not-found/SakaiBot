using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using SakaiBot.Models;
using SakaiBot.Services;
using System.Threading.Tasks;

namespace SakaiBot.Commands
{
    public class ModerationSlashModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly ModerationLogger _moderationLogger;

        public ModerationSlashModule(ModerationLogger moderationLogger)
        {
            _moderationLogger = moderationLogger;
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
            await user.BanAsync(0, reason);
            await _moderationLogger.LogAsync(CreatePunishment(user.Id, Context.User.Id, PunishmentType.Ban, reason));
            await RespondAsync($"Banned {user.Username}#{user.Discriminator} for: {reason}", ephemeral: true);
        }

        [SlashCommand("kick", "Kick a user from the server.")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        [RequireBotPermission(GuildPermission.KickMembers)]
        public async Task KickAsync(SocketGuildUser user, string reason = "No reason provided")
        {
            await user.KickAsync(reason);
            await _moderationLogger.LogAsync(CreatePunishment(user.Id, Context.User.Id, PunishmentType.Kick, reason));
            await RespondAsync($"Kicked {user.Username}#{user.Discriminator} for: {reason}", ephemeral: true);
        }

        [SlashCommand("mute", "Mute a user in the server.")]
        [RequireUserPermission(GuildPermission.MuteMembers)]
        [RequireBotPermission(GuildPermission.MuteMembers)]
        public async Task MuteAsync(SocketGuildUser user, string reason = "No reason provided")
        {
            await user.ModifyAsync(x => x.Mute = true);
            await _moderationLogger.LogAsync(CreatePunishment(user.Id, Context.User.Id, PunishmentType.Mute, reason));
            await RespondAsync($"Muted {user.Username}#{user.Discriminator} for: {reason}", ephemeral: true);
        }

        [SlashCommand("unmute", "Unmute a user in the server.")]
        [RequireUserPermission(GuildPermission.MuteMembers)]
        [RequireBotPermission(GuildPermission.MuteMembers)]
        public async Task UnmuteAsync(SocketGuildUser user)
        {
            await user.ModifyAsync(x => x.Mute = false);
            await _moderationLogger.LogAsync(CreatePunishment(user.Id, Context.User.Id, PunishmentType.Unmute, "Unmuted user"));
            await RespondAsync($"Unmuted {user.Username}#{user.Discriminator}.", ephemeral: true);
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
            await _moderationLogger.LogAsync(CreatePunishment(user.Id, Context.User.Id, PunishmentType.Warn, reason));
            var embed = new EmbedBuilder()
                .WithTitle("User Warned")
                .WithDescription($"{user.Mention} was warned by {Context.User.Mention}.")
                .AddField("Reason", reason)
                .WithColor(Color.Orange)
                .Build();

            await RespondAsync(embed: embed, ephemeral: true);
        }
    }
}
