using Microsoft.EntityFrameworkCore;
using SakaiBot.Data;

DotNetEnv.Env.Load();
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var databaseConnectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
    ?? builder.Configuration["ConnectionStrings:Postgres"]
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
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
    options.UseNpgsql(databaseConnectionString, npgsqlOptions =>
        npgsqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null)));

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
app.MapGet("/health/db", async (AppDbContext dbContext) =>
{
    var healthy = await dbContext.Database.CanConnectAsync();
    return healthy
        ? Results.Ok(new { status = "ok", database = "connected" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
});
app.MapGet("/dashboard", async (HttpRequest request, AppDbContext dbContext, string? search) =>
{
    var dashboardToken = Environment.GetEnvironmentVariable("DASHBOARD_TOKEN");
    if (string.IsNullOrWhiteSpace(dashboardToken)
        || request.Headers["X-Dashboard-Token"] != dashboardToken)
    {
        return Results.Unauthorized();
    }

    var query = dbContext.Punishments.AsNoTracking().AsQueryable();
    if (!string.IsNullOrWhiteSpace(search))
    {
        if (ulong.TryParse(search, out var userId))
        {
            query = query.Where(x => x.UserId == userId);
        }
        else
        {
            query = query.Where(x => x.CaseId.StartsWith(search));
        }
    }

    var cases = await query.OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync();
    var rows = string.Join("", cases.Select(item => $"<tr><td>{System.Net.WebUtility.HtmlEncode(item.CaseId[..8])}</td><td>{item.Action}</td><td>{item.UserId}</td><td>{System.Net.WebUtility.HtmlEncode(item.Reason)}</td><td>{item.CreatedAt:u}</td></tr>"));
    var html = $"<!doctype html><html><head><meta charset='utf-8'><title>SakaiBot Mod Dashboard</title><style>body{{font-family:system-ui;margin:2rem;background:#111827;color:#f9fafb}}input,button{{padding:.6rem;margin-right:.4rem}}table{{width:100%;border-collapse:collapse;margin-top:1rem}}td,th{{padding:.6rem;border-bottom:1px solid #374151;text-align:left}}</style></head><body><h1>SakaiBot moderation dashboard</h1><form><input name='search' value='{System.Net.WebUtility.HtmlEncode(search ?? string.Empty)}' placeholder='User ID or case ID'><button>Search</button></form><table><thead><tr><th>Case</th><th>Action</th><th>User</th><th>Reason</th><th>Created</th></tr></thead><tbody>{rows}</tbody></table></body></html>";
    return Results.Content(html, "text/html");
});

app.Run();
