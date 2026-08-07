using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace SakaiBot.Services
{
    public class InteractionHandler : IHostedService
    {
        private readonly InteractionService _interactionService;
        private readonly DiscordSocketClient _client;
        private readonly IServiceProvider _services;
        private readonly IConfiguration _configuration;
        private readonly ILogger<InteractionHandler> _logger;

        public InteractionHandler(
            InteractionService interactionService,
            DiscordSocketClient client,
            IServiceProvider services,
            IConfiguration configuration,
            ILogger<InteractionHandler> logger)
        {
            _interactionService = interactionService;
            _client = client;
            _services = services;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _client.Ready += OnReadyAsync;
            _client.InteractionCreated += OnInteractionCreatedAsync;

            await _interactionService.AddModulesAsync(Assembly.GetExecutingAssembly(), _services);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private async Task OnReadyAsync()
        {
            _logger.LogInformation("Registering slash commands...");
            var guildId = _configuration.GetValue<ulong?>("DISCORD_GUILD_ID");

            if (guildId.HasValue)
            {
                await _interactionService.RegisterCommandsToGuildAsync(guildId.Value);
                _logger.LogInformation("Registered slash commands to guild {GuildId}", guildId.Value);
            }
            else
            {
                await _interactionService.RegisterCommandsGloballyAsync();
                _logger.LogInformation("Registered slash commands globally.");
            }
        }

        private async Task OnInteractionCreatedAsync(SocketInteraction interaction)
        {
            var context = new SocketInteractionContext(_client, interaction);
            await _interactionService.ExecuteCommandAsync(context, _services);
        }
    }
}
