using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using SimulatorService;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

app.MapGet("/api/simulator/status", () =>
{
    return Results.Ok(new { Status = "Running", Timestamp = DateTime.UtcNow });
});

app.Run("http://0.0.0.0:5001");
