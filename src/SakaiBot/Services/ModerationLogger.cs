using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SakaiBot.Data;
using SakaiBot.Models;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace SakaiBot.Services
{
    public class ModerationLogger
    {
        private readonly AppDbContext _dbContext;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ModerationLogger> _logger;
        private readonly string? _webhookUrl;

        public ModerationLogger(AppDbContext dbContext, IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<ModerationLogger> logger)
        {
            _dbContext = dbContext;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _webhookUrl = configuration["MODERATION_WEBHOOK_URL"];
        }

        public async Task LogAsync(Punishment punishment)
        {
            punishment.CaseId = Guid.NewGuid().ToString("N");
            punishment.CreatedAt = DateTime.UtcNow;

            await _dbContext.Punishments.AddAsync(punishment);
            await _dbContext.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(_webhookUrl))
            {
                await SendWebhookAsync(punishment);
            }
        }

        private async Task SendWebhookAsync(Punishment punishment)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var payload = new
                {
                    username = "SakaiBot ModLog",
                    embeds = new[]
                    {
                        new
                        {
                            title = "Moderation action",
                            description = $"**Action:** {punishment.Action}\n**User:** <@{punishment.UserId}>\n**Moderator:** <@{punishment.ModeratorId}>\n**Reason:** {punishment.Reason}",
                            fields = new[]
                            {
                                new { name = "Guild ID", value = punishment.GuildId.ToString(), inline = true },
                                new { name = "Case ID", value = punishment.CaseId, inline = true },
                                new { name = "Timestamp", value = punishment.CreatedAt.ToString("u"), inline = false }
                            },
                            color = 16728320
                        }
                    }
                };

                var response = await client.PostAsJsonAsync(_webhookUrl, payload);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Moderation webhook failed with status {StatusCode}: {Body}", response.StatusCode, body);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send moderation webhook.");
            }
        }
    }
}
