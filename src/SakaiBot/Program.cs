DotNetEnv.Env.Load();
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

builder.Services.AddSingleton(provider => new Discord.WebSocket.DiscordSocketClient(new Discord.WebSocket.DiscordSocketConfig
{
    GatewayIntents = Discord.GatewayIntents.Guilds | Discord.GatewayIntents.GuildMembers,
    AlwaysDownloadUsers = true,
}));

builder.Services.AddSingleton(provider => new Discord.Interactions.InteractionService(provider.GetRequiredService<Discord.WebSocket.DiscordSocketClient>(), new Discord.Interactions.InteractionServiceConfig
{
    DefaultRunMode = Discord.Interactions.RunMode.Async
}));

builder.Services.AddHostedService<SakaiBot.Services.BotService>();
builder.Services.AddHostedService<SakaiBot.Services.InteractionHandler>();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { status = "ok", service = "SakaiBot" }));
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
