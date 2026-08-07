using System;

namespace SakaiBot.Models
{
    public class EconomyAccount
    {
        public int Id { get; set; }
        public ulong GuildId { get; set; }
        public ulong UserId { get; set; }
        public long Balance { get; set; } = 1000;
        public DateTime? LastDailyClaimedAt { get; set; }
    }
}
