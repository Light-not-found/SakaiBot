using Discord.Interactions;
using System.Threading.Tasks;

namespace SakaiBot.Commands
{
    public class BotInfoSlashModule : InteractionModuleBase
    {
        [SlashCommand("botinfo", "Get information about the bot.")]
        public async Task BotInfoAsync()
        {
            await RespondAsync("SakaiBot is online and ready to moderate your server!", ephemeral: true);
        }
    }
}
