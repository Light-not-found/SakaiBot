using System;

namespace SakaiBot.Models
{
    public enum PunishmentType
    {
        Ban,
        Kick,
        Mute,
        Unmute,
        Warn
    }

    public class Punishment
    {
        public int Id { get; set; }
        public ulong GuildId { get; set; }
        public ulong UserId { get; set; }
        public ulong ModeratorId { get; set; }
        public PunishmentType Action { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string CaseId { get; set; } = string.Empty;
    }
}
