using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using SimulatorService;
using OSim.Shared.Messages;

var builder = WebApplication.CreateBuilder(args);

// Add SimulatorEngine as singleton
var simulatorEngine = new SimulatorEngine();
builder.Services.AddSingleton(simulatorEngine);

// Add Worker with SimulatorEngine
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

app.MapGet("/api/simulator/status", () =>
{
    return Results.Ok(new { Status = "Running", Timestamp = DateTime.UtcNow });
});

app.MapPost("/api/simulator/course", async (HttpContext context, SimulatorEngine engine) =>
{
    try
    {
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();
        var command = System.Text.Json.JsonSerializer.Deserialize<SetCourseCommand>(body);

        if (command == null)
            return Results.BadRequest("Invalid command format");

        engine.SetDesiredCourse(command.TargetCourseDegrees);
        return Results.Ok(new { Message = $"Course set to {command.TargetCourseDegrees}" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
});

app.MapPost("/api/simulator/speed", async (HttpContext context, SimulatorEngine engine) =>
{
    try
    {
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();
        var command = System.Text.Json.JsonSerializer.Deserialize<SetSpeedCommand>(body);

        if (command == null)
            return Results.BadRequest("Invalid command format");

        engine.SetDesiredSpeed(command.TargetSpeedKnots);
        return Results.Ok(new { Message = $"Speed set to {command.TargetSpeedKnots}" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
}); app.Run("http://0.0.0.0:5001");
