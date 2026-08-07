var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { status = "ok", service = "SakaiBot" }));
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
