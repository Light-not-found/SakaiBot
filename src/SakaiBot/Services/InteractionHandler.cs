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
            _logger.LogInformation("Starting InteractionHandler");
            _client.Ready += OnReadyAsync;
            _client.InteractionCreated += OnInteractionCreatedAsync;
            _interactionService.Log += LogAsync;

            try
            {
                await _interactionService.AddModulesAsync(Assembly.GetExecutingAssembly(), _services);
                _logger.LogInformation("Loaded interaction modules successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load interaction modules.");
                throw;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private async Task OnReadyAsync()
        {
            var guildId = _configuration.GetValue<ulong?>("DISCORD_GUILD_ID");
            _logger.LogInformation("Discord ready event fired. GuildId configured: {GuildId}", guildId?.ToString() ?? "none");

            try
            {
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register slash commands.");
            }
        }

        private async Task OnInteractionCreatedAsync(SocketInteraction interaction)
        {
            try
            {
                var context = new SocketInteractionContext(_client, interaction);
                await _interactionService.ExecuteCommandAsync(context, _services);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Interaction execution failed.");
            }
        }

        private Task LogAsync(LogMessage message)
        {
            _logger.Log(
                message.Severity switch
                {
                    LogSeverity.Critical => LogLevel.Critical,
                    LogSeverity.Error => LogLevel.Error,
                    LogSeverity.Warning => LogLevel.Warning,
                    LogSeverity.Info => LogLevel.Information,
                    LogSeverity.Verbose => LogLevel.Debug,
                    LogSeverity.Debug => LogLevel.Trace,
                    _ => LogLevel.Information,
                },
                message.Exception,
                "[InteractionService:{Source}] {Message}",
                message.Source,
                message.Message);

            return Task.CompletedTask;
        }
    }
}
