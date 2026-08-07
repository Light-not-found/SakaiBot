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

            _token = configuration["DISCORD:Token"] ?? configuration["DISCORD__TOKEN"] ?? string.Empty;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _client.Log += LogAsync;
            _client.Ready += OnReadyAsync;
            _client.InteractionCreated += OnInteractionCreatedAsync;

            await RegisterCommandsAsync();

            if (string.IsNullOrWhiteSpace(_token))
            {
                _logger.LogError("Discord bot token is not configured. Set DISCORD__TOKEN in environment variables or secrets.");
                return;
            }

            await _client.LoginAsync(TokenType.Bot, _token);
            await _client.StartAsync();

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private Task OnReadyAsync()
        {
            _logger.LogInformation("Discord bot connected as {BotUser}", _client.CurrentUser?.Username);
            return Task.CompletedTask;
        }

        private async Task OnInteractionCreatedAsync(SocketInteraction interaction)
        {
            var context = new SocketInteractionContext(_client, interaction);
            await _interactionService.ExecuteCommandAsync(context, _services);
        }

        private async Task RegisterCommandsAsync()
        {
            await _interactionService.AddModulesAsync(Assembly.GetExecutingAssembly(), _services);
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
