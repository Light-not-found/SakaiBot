using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using SakaiBot.Data;
using SakaiBot.Models;
using System.Threading.Tasks;

namespace SakaiBot.Commands
{
    public class ConfigurationSlashModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly AppDbContext _dbContext;

        public ConfigurationSlashModule(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [SlashCommand("setmodchannel", "Set the channel for moderation logs.")]
        [RequireUserPermission(GuildPermission.ManageGuild)]
        [RequireBotPermission(GuildPermission.SendMessages)]
        public async Task SetModChannelAsync(ITextChannel channel)
        {
            var settings = await GetSettingsAsync();
            settings.ModChannelId = channel.Id;
            await _dbContext.SaveChangesAsync();
            await RespondAsync($"Moderation logs will be posted in {channel.Mention}.", ephemeral: true);
        }

        [SlashCommand("setbirthdaychannel", "Set the channel for birthday messages.")]
        [RequireUserPermission(GuildPermission.ManageGuild)]
        [RequireBotPermission(GuildPermission.SendMessages)]
        public async Task SetBirthdayChannelAsync(ITextChannel channel)
        {
            var settings = await GetSettingsAsync();
            settings.BirthdayChannelId = channel.Id;
            await _dbContext.SaveChangesAsync();
            await RespondAsync($"Birthday messages will be posted in {channel.Mention}.", ephemeral: true);
        }

        [SlashCommand("setlogwebhook", "Set the moderation webhook URL.")]
        [RequireUserPermission(GuildPermission.ManageGuild)]
        public async Task SetLogWebhookAsync(string webhookUrl)
        {
            if (!Uri.TryCreate(webhookUrl, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps
                || !uri.Host.Equals("discord.com", System.StringComparison.OrdinalIgnoreCase)
                    && !uri.Host.Equals("discordapp.com", System.StringComparison.OrdinalIgnoreCase))
            {
                await RespondAsync("Please provide a valid Discord HTTPS webhook URL.", ephemeral: true);
                return;
            }

            var settings = await GetSettingsAsync();
            settings.LogWebhookUrl = webhookUrl;
            await _dbContext.SaveChangesAsync();
            await RespondAsync("The moderation webhook has been saved.", ephemeral: true);
        }

        private async Task<GuildSettings> GetSettingsAsync()
        {
            var guildId = Context.Guild?.Id ?? 0;
            var settings = await _dbContext.GuildSettings.FirstOrDefaultAsync(x => x.GuildId == guildId);
            if (settings is null)
            {
                settings = new GuildSettings { GuildId = guildId };
                await _dbContext.GuildSettings.AddAsync(settings);
            }

            return settings;
        }
    }
}
