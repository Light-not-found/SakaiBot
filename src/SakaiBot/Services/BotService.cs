using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace SakaiBot.Services
{
    public class BotService : BackgroundService
    {
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _interactionService;
        private readonly IServiceProvider _services;
        private readonly IConfiguration _configuration;
        private readonly ILogger<BotService> _logger;
        private string _token = string.Empty;

        public BotService(DiscordSocketClient client, InteractionService interactionService, IConfiguration configuration, ILogger<BotService> logger, IServiceProvider services)
        {
            _client = client;
            _interactionService = interactionService;
            _configuration = configuration;
            _logger = logger;
            _services = services;

            _token = configuration["DISCORD:Token"]
                ?? configuration["DISCORD__TOKEN"]
                ?? configuration["DISCORD_TOKEN"]
                ?? Environment.GetEnvironmentVariable("DISCORD__TOKEN")
                ?? Environment.GetEnvironmentVariable("DISCORD_TOKEN")
                ?? string.Empty;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _client.Log += LogAsync;
            _client.Ready += OnReadyAsync;

            _logger.LogInformation("Discord token loaded: {HasToken}", string.IsNullOrWhiteSpace(_token) ? "no" : "yes");

            if (string.IsNullOrWhiteSpace(_token))
            {
                _logger.LogError("Discord bot token is not configured. Set DISCORD__TOKEN in environment variables or secrets.");
                return;
            }

            try
            {
                await _client.LoginAsync(TokenType.Bot, _token);
                await _client.StartAsync();
                _logger.LogInformation("Discord client login started.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Discord login failed.");
                return;
            }

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private Task OnReadyAsync()
        {
            _logger.LogInformation("Discord bot connected as {BotUser} ({BotId})", _client.CurrentUser?.Username, _client.CurrentUser?.Id);
            return Task.CompletedTask;
        }

        private Task OnInteractionCreatedAsync(SocketInteraction interaction)
        {
            return Task.CompletedTask;
        }

        private Task RegisterCommandsAsync()
        {
            return Task.CompletedTask;
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
                "[{Source}] {Message}",
                message.Source,
                message.Message);

            return Task.CompletedTask;
        }
    }
}
