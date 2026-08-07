using Discord.Interactions;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SakaiBot.Commands
{
    public class FunSlashModule : InteractionModuleBase<SocketInteractionContext>
    {
        private static readonly string[] Fortunes = new[]
        {
            "Today is a great day for a new beginning.",
            "Someone will make you smile soon.",
            "A surprise gift is heading your way.",
            "Your hard work will pay off in unexpected ways.",
            "Take a chance today; it could change everything.",
            "A fun new friend may appear in your life.",
            "You are closer to your goal than you think.",
            "Good news will arrive from an old connection.",
            "Be bold and creative; others will notice.",
            "A small act of kindness will come back to you."
        };

        [SlashCommand("roll-dice", "Roll dice using standard notation like 1d20 or 2d6+3.")]
        public async Task RollDiceAsync(string notation = "1d20")
        {
            if (!TryParseDiceNotation(notation, out var count, out var sides, out var modifier))
            {
                await RespondAsync("Please provide dice in the form `NdM` or `NdM+K`, like `2d6+1`.", ephemeral: true);
                return;
            }

            var random = new Random();
            var rolls = new int[count];
            var sum = 0;

            for (var i = 0; i < count; i++)
            {
                rolls[i] = random.Next(1, sides + 1);
                sum += rolls[i];
            }

            var total = sum + modifier;
            var rollText = string.Join(", ", rolls);
            var modifierText = modifier == 0 ? string.Empty : modifier > 0 ? $" + {modifier}" : $" - {Math.Abs(modifier)}";

            await RespondAsync($"You rolled: {rollText}{modifierText} = **{total}**", ephemeral: false);
        }

        [SlashCommand("fortune", "Get a random fortune.")]
        public async Task FortuneAsync()
        {
            var random = new Random();
            var fortune = Fortunes[random.Next(Fortunes.Length)];
            await RespondAsync($"🔮 {fortune}", ephemeral: false);
        }

        [SlashCommand("meme", "Get a random meme caption idea.")]
        public async Task MemeAsync()
        {
            var random = new Random();
            var captions = new[]
            {
                "When the bot is online but the server still sleeps.",
                "That moment when your code compiles on the first try.",
                "Me: I'll just write one more feature.",
                "When the birthday reminder hits exactly on time.",
                "Discord: 4014. Bot: I only want to moderate.",
                "Roll initiative! The DM says it's going to be a wild night.",
                "When the bot sees the bad word and reacts with a timeout.",
            };

            await RespondAsync($"{captions[random.Next(captions.Length)]}", ephemeral: false);
        }

        private static bool TryParseDiceNotation(string notation, out int count, out int sides, out int modifier)
        {
            count = 0;
            sides = 0;
            modifier = 0;

            var match = Regex.Match(notation.Trim(), "^(\\d+)d(\\d+)([+-]\\d+)?$", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return false;
            }

            if (!int.TryParse(match.Groups[1].Value, out count) || !int.TryParse(match.Groups[2].Value, out sides))
            {
                return false;
            }

            if (count < 1 || count > 20 || sides < 2 || sides > 1000)
            {
                return false;
            }

            if (match.Groups[3].Success)
            {
                int.TryParse(match.Groups[3].Value, out modifier);
            }

            return true;
        }
    }
}
