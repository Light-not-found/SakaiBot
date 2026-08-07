using Microsoft.EntityFrameworkCore;
using SakaiBot.Data;

DotNetEnv.Env.Load();
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var databaseConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration["DATABASE_URL"]
    ?? builder.Configuration["Database:ConnectionString"];

if (string.IsNullOrWhiteSpace(databaseConnectionString))
{
    throw new InvalidOperationException("Database connection string is not configured. Set ConnectionStrings:DefaultConnection or DATABASE_URL.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(databaseConnectionString));

builder.Services.AddSingleton(provider => new Discord.WebSocket.DiscordSocketClient(new Discord.WebSocket.DiscordSocketConfig
{
    GatewayIntents = Discord.GatewayIntents.Guilds,
    AlwaysDownloadUsers = false,
}));

builder.Services.AddSingleton(provider => new Discord.Interactions.InteractionService(provider.GetRequiredService<Discord.WebSocket.DiscordSocketClient>(), new Discord.Interactions.InteractionServiceConfig
{
    DefaultRunMode = Discord.Interactions.RunMode.Async
}));

builder.Services.AddHostedService<SakaiBot.Services.BotService>();
builder.Services.AddHostedService<SakaiBot.Services.InteractionHandler>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.EnsureCreated();
}

app.MapGet("/", () => Results.Ok(new { status = "ok", service = "SakaiBot" }));
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
