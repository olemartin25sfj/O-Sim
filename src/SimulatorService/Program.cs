using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using NATS.Client;
using OSim.Shared.Messages;
using SimulatorService;

var builder = WebApplication.CreateBuilder(args);

// Add SimulatorEngine as singleton
var simulatorEngine = new SimulatorEngine();
builder.Services.AddSingleton(simulatorEngine);

// Autopilot as singleton so API kan sette destinasjon
builder.Services.AddSingleton<AutopilotService>();

// NATS connection singleton (lazy)
builder.Services.AddSingleton<IConnection>(_ =>
{
    var opts = ConnectionFactory.GetDefaultOptions();
    opts.Url = "nats://nats:4222";
    return new ConnectionFactory().CreateConnection(opts);
});

// Add Worker with SimulatorEngine
builder.Services.AddHostedService<Worker>();

// CORS for frontend (localhost:3000 via Traefik/nginx) – dev only allow all
builder.Services.AddCors(options =>
{
    options.AddPolicy("dev", p => p
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// Felles JSON options – gjør navn case-insensitive slik at frontendens camelCase (targetSpeedKnots) matches TargetSpeedKnots
var jsonOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
};

var app = builder.Build();

// Enable CORS before endpoints so preflight (OPTIONS) ikke gir 405
app.UseCors("dev");

// Fallback OPTIONS (catch‑all under /api/simulator/*) for clients som sender preflight
app.MapMethods("/api/simulator/{*any}", new[] { "OPTIONS" }, () => Results.Ok())
    .WithName("SimulatorPreflight");

app.MapGet("/api/simulator/status", () =>
{
    return Results.Ok(new { Status = "Running", Timestamp = DateTime.UtcNow });
});

IResult PublishCommandAndLog<T>(IConnection nats, string topic, T payload, string service, string message)
{
    try
    {
        var json = JsonSerializer.Serialize(payload);
        nats.Publish(topic, System.Text.Encoding.UTF8.GetBytes(json));
        var log = new LogEntry(DateTime.UtcNow, service, "Information", message, null);
        var logJson = JsonSerializer.Serialize(log);
        nats.Publish("log.entries", System.Text.Encoding.UTF8.GetBytes(logJson));
    }
    catch
    {
        // swallow for now – could add retry/backoff
    }
    return Results.Ok();
}

app.MapPost("/api/simulator/position", async (HttpContext context, SimulatorEngine engine, IConnection nats) =>
{
    try
    {
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();
        var command = JsonSerializer.Deserialize<SetPositionCommand>(body, jsonOptions);

        if (command == null)
            return Results.BadRequest("Invalid command format");

        engine.SetInitialPosition(command.Latitude, command.Longitude);
        PublishCommandAndLog(nats, "sim.commands.setposition", command, "SimulatorService",
            $"Position set to {command.Latitude},{command.Longitude}");
        return Results.Ok(new { Message = $"Position set to {command.Latitude}, {command.Longitude}" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
});

app.MapPost("/api/simulator/course", async (HttpContext context, SimulatorEngine engine, IConnection nats) =>
{
    try
    {
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();
        var command = JsonSerializer.Deserialize<SetCourseCommand>(body, jsonOptions);

        if (command == null)
            return Results.BadRequest("Invalid command format");

        engine.SetDesiredCourse(command.TargetCourseDegrees);
        PublishCommandAndLog(nats, "sim.commands.setcourse", command, "SimulatorService",
            $"Course set to {command.TargetCourseDegrees}");
        return Results.Ok(new { Message = $"Course set to {command.TargetCourseDegrees}" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
});

app.MapPost("/api/simulator/speed", async (HttpContext context, SimulatorEngine engine, IConnection nats) =>
{
    try
    {
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();
        var command = JsonSerializer.Deserialize<SetSpeedCommand>(body, jsonOptions);

        if (command == null)
            return Results.BadRequest("Invalid command format");

        engine.SetDesiredSpeed(command.TargetSpeedKnots);
        app.Logger.LogInformation("Desired speed set to {Speed} knots", command.TargetSpeedKnots);
        PublishCommandAndLog(nats, "sim.commands.setspeed", command, "SimulatorService",
            $"Speed set to {command.TargetSpeedKnots}");
        return Results.Ok(new { Message = $"Speed set to {command.TargetSpeedKnots}" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
});

// Start / oppdater en reise (setter evt startposisjon, destinasjon og cruise-fart)
app.MapPost("/api/simulator/journey", async (HttpContext context, SimulatorEngine engine, AutopilotService autopilot, IConnection nats) =>
{
    try
    {
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        double? startLat = root.TryGetProperty("startLatitude", out var slat) ? slat.GetDouble() : null;
        double? startLon = root.TryGetProperty("startLongitude", out var slon) ? slon.GetDouble() : null;
        double? endLat = root.TryGetProperty("endLatitude", out var elat) ? elat.GetDouble() : null;
        double? endLon = root.TryGetProperty("endLongitude", out var elon) ? elon.GetDouble() : null;
        double? cruise = root.TryGetProperty("cruiseSpeedKnots", out var c) ? c.GetDouble() : null;

        if (endLat == null || endLon == null)
            return Results.BadRequest("endLatitude og endLongitude må settes");

        if (startLat.HasValue && startLon.HasValue)
        {
            engine.SetInitialPosition(startLat.Value, startLon.Value);
        }

        if (cruise.HasValue)
        {
            autopilot.SetCruisingSpeed(cruise.Value);
        }

        autopilot.SetDestination(endLat.Value, endLon.Value);

        var journeyCmd = new
        {
            timestampUtc = DateTime.UtcNow,
            startLatitude = startLat,
            startLongitude = startLon,
            endLatitude = endLat,
            endLongitude = endLon,
            cruiseSpeedKnots = cruise
        };
        PublishCommandAndLog(nats, "sim.commands.journey", journeyCmd, "SimulatorService",
            $"Journey start end=({endLat},{endLon}) cruise={cruise}");

        app.Logger.LogInformation("Journey started: start=({StartLat},{StartLon}) end=({EndLat},{EndLon}) cruise={Cruise}",
            startLat, startLon, endLat, endLon, cruise);

        return Results.Ok(new { Message = "Journey started", endLatitude = endLat, endLongitude = endLon, cruiseSpeedKnots = cruise });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
});

// Destination / progress status
app.MapGet("/api/simulator/destination", (SimulatorEngine engine, AutopilotService autopilot) =>
{
    if (!engine.HasDestination || engine.TargetLatitude == null || engine.TargetLongitude == null)
        return Results.Ok(new { hasDestination = false });

    var (hasTarget, distanceNm) = autopilot.GetDestinationStatus(engine.Latitude, engine.Longitude);
    double? etaMinutes = null;
    if (distanceNm.HasValue && engine.Speed > 0.1)
    {
        etaMinutes = (distanceNm.Value / engine.Speed) * 60.0; // nm / kn = hours → *60 = minutes
    }
    return Results.Ok(new
    {
        hasDestination = hasTarget,
        targetLatitude = engine.TargetLatitude,
        targetLongitude = engine.TargetLongitude,
        distanceNm,
        etaMinutes,
        hasArrived = engine.HasArrived
    });
});

app.Run("http://0.0.0.0:5001");
