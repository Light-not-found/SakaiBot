using Microsoft.EntityFrameworkCore;
using SakaiBot.Data;

DotNetEnv.Env.Load();
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var databaseConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration.GetConnectionString("Postgres")
    ?? builder.Configuration["DATABASE_URL"]
    ?? builder.Configuration["Database:ConnectionString"];

if (string.IsNullOrWhiteSpace(databaseConnectionString))
{
    throw new InvalidOperationException("Database connection string is not configured. Set ConnectionStrings:DefaultConnection, ConnectionStrings:Postgres, DATABASE_URL, or Database:ConnectionString.");
}

static string NormalizeConnectionString(string connectionString)
{
    var trimmed = connectionString.Trim();
    if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
    {
        trimmed = trimmed[1..^1];
    }
    return trimmed;
}

databaseConnectionString = NormalizeConnectionString(databaseConnectionString);

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

builder.Services.AddHttpClient();
builder.Services.AddScoped<SakaiBot.Services.ModerationLogger>();

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
