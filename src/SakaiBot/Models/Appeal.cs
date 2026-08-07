using System;

namespace SakaiBot.Models
{
    public class Appeal
    {
        public int Id { get; set; }
        public ulong GuildId { get; set; }
        public ulong UserId { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool Resolved { get; set; }
    }
}
