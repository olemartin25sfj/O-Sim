using AutopilotService;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

// Status endpoint
app.MapGet("/api/autopilot/status", () =>
{
    return Results.Ok(new { Status = "Running", Timestamp = DateTime.UtcNow });
});

app.Run("http://0.0.0.0:5005");
