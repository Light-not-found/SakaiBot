namespace SakaiBot.Models
{
    public class GuildSettings
    {
        public ulong GuildId { get; set; }
        public ulong? ModChannelId { get; set; }
        public ulong? BirthdayChannelId { get; set; }
        public string? LogWebhookUrl { get; set; }
    }
}
