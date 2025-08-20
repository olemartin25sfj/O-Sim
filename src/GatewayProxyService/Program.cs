using System.Text;
using System.Text.Json;
using GatewayProxyService;
using NATS.Client;
using OSim.Shared.Messages;

// Cache for latest navigation data
NavigationData? lastNavigationData = null;
object navDataLock = new();

// Load static routes catalog at startup (types in Routes.cs)
var routesFile = Path.Combine(AppContext.BaseDirectory, "Data", "routes.json");
var routesRaw = JsonSerializer.Deserialize<List<RouteRaw>>(File.ReadAllText(routesFile)) ?? new();
var routes = routesRaw.Select(r => RouteComputed.From(r)).ToList();
var routeMetas = new List<RouteMeta>();
var routeDetails = new Dictionary<string, RouteDetail>(StringComparer.OrdinalIgnoreCase);
foreach (var r in routes)
{
    routeMetas.Add(r.meta);
    routeDetails[r.detail.Id] = r.detail;
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// NATS connection singleton
builder.Services.AddSingleton<IConnection>(_ =>
{
    int retries = 10;
    for (int i = 0; i < retries; i++)
    {
        try
        {
            return new ConnectionFactory().CreateConnection("nats://nats:4222");
        }
        catch when (i < retries - 1)
        {
            Thread.Sleep(2000);
        }
    }
    throw new InvalidOperationException("Could not connect to NATS after multiple attempts");
});

var app = builder.Build();

app.UseCors();

app.UseWebSockets();

// GET /api/routes?bbox=minLat,minLon,maxLat,maxLon&limit=10
app.MapGet("/api/routes", (HttpRequest req) =>
{
    string? bbox = req.Query["bbox"].FirstOrDefault();
    int limit = 50;
    if (int.TryParse(req.Query["limit"], out var l) && l > 0 && l < limit)
        limit = l;

    IEnumerable<RouteMeta> result = routeMetas;
    if (!string.IsNullOrWhiteSpace(bbox))
    {
        var parts = bbox.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 4 || !double.TryParse(parts[0], out var minLat) || !double.TryParse(parts[1], out var minLon) || !double.TryParse(parts[2], out var maxLat) || !double.TryParse(parts[3], out var maxLon))
        {
            return Results.BadRequest(new { error = "bbox must be 'minLat,minLon,maxLat,maxLon'" });
        }
        result = result.Where(r => !(r.MaxLat < minLat || r.MinLat > maxLat || r.MaxLon < minLon || r.MinLon > maxLon));
    }
    return Results.Ok(result.Take(limit));
});

// GET /api/routes/{id}
app.MapGet("/api/routes/{id}", (string id) =>
{
    if (routeDetails.TryGetValue(id, out var detail))
        return Results.Ok(detail);
    return Results.NotFound();
});

// API Proxy endpoints for simulator commands
app.MapPost("/api/simulator/speed", async (HttpContext context, IConnection nats) =>
{
    try
    {
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();
        var command = JsonSerializer.Deserialize<SetSpeedCommand>(body);

        if (command == null)
            return Results.BadRequest("Invalid command format");

        var json = JsonSerializer.Serialize(command);
        nats.Publish("sim.commands.setspeed", Encoding.UTF8.GetBytes(json));

        app.Logger.LogInformation("Proxied speed command: {Speed} knots", command.TargetSpeedKnots);
        return Results.Ok(new { Message = $"Speed set to {command.TargetSpeedKnots}" });
    }
    catch (Exception ex)
    {
        app.Logger.LogError("Error proxying speed command: {Error}", ex.Message);
        return Results.BadRequest(ex.Message);
    }
});

app.MapPost("/api/simulator/stop", (HttpContext context, IConnection nats) =>
{
    try
    {
        // Send speed=0 command via NATS
        var stopCommand = new SetSpeedCommand(DateTime.UtcNow, 0.0);
        var json = JsonSerializer.Serialize(stopCommand);
        nats.Publish("sim.commands.setspeed", Encoding.UTF8.GetBytes(json));

        app.Logger.LogInformation("Proxied stop command (speed=0)");
        return Results.Ok(new { Message = "Stopped" });
    }
    catch (Exception ex)
    {
        app.Logger.LogError("Error proxying stop command: {Error}", ex.Message);
        return Results.StatusCode(500);
    }
});

app.MapPost("/api/simulator/course", async (HttpContext context, IConnection nats) =>
{
    try
    {
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();
        var command = JsonSerializer.Deserialize<SetCourseCommand>(body);

        if (command == null)
            return Results.BadRequest("Invalid command format");

        var json = JsonSerializer.Serialize(command);
        nats.Publish("sim.commands.setcourse", Encoding.UTF8.GetBytes(json));

        app.Logger.LogInformation("Proxied course command: {Course} degrees", command.TargetCourseDegrees);
        return Results.Ok(new { Message = $"Course set to {command.TargetCourseDegrees}" });
    }
    catch (Exception ex)
    {
        app.Logger.LogError("Error proxying course command: {Error}", ex.Message);
        return Results.BadRequest(ex.Message);
    }
});

app.MapPost("/api/simulator/journey", async (HttpContext context, IConnection nats) =>
{
    try
    {
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();

        // Forward the journey request directly to SimulatorService
        using var httpClient = new HttpClient();
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync("http://simulatorservice:5001/api/simulator/journey", content);

        if (response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            app.Logger.LogInformation("Successfully proxied journey command to SimulatorService");

            // Parse response to extract journey details for logging
            try
            {
                var jsonDoc = JsonDocument.Parse(body);
                var root = jsonDoc.RootElement;
                var startLat = root.TryGetProperty("startLatitude", out var sLat) ? sLat.GetDouble() : (double?)null;
                var startLon = root.TryGetProperty("startLongitude", out var sLon) ? sLon.GetDouble() : (double?)null;
                var endLat = root.TryGetProperty("endLatitude", out var eLat) ? eLat.GetDouble() : (double?)null;
                var endLon = root.TryGetProperty("endLongitude", out var eLon) ? eLon.GetDouble() : (double?)null;
                var waypointCount = root.TryGetProperty("routeWaypoints", out var wp) && wp.ValueKind == JsonValueKind.Array ? wp.GetArrayLength() : 0;

                app.Logger.LogInformation("Journey started: Start({StartLat},{StartLon}) End({EndLat},{EndLon}) Waypoints={Count}",
                    startLat, startLon, endLat, endLon, waypointCount);
            }
            catch { /* Ignore parsing errors for logging */ }

            // Return the original response from SimulatorService
            return Results.Content(responseBody, "application/json");
        }
        else
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            app.Logger.LogError("SimulatorService journey request failed: {StatusCode} - {Error}", response.StatusCode, errorBody);
            return Results.StatusCode((int)response.StatusCode);
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogError("Error proxying journey command: {Error}", ex.Message);
        return Results.BadRequest(ex.Message);
    }
});

app.MapGet("/api/simulator/destination", () =>
{
    NavigationData? navData;
    lock (navDataLock)
    {
        navData = lastNavigationData;
    }

    if (navData == null)
    {
        // No data available yet
        return Results.Ok(new { hasDestination = false });
    }

    // Return destination status based on cached navigation data
    double? distanceNm = null;
    double? etaMinutes = null;

    // Calculate distance if we have destination
    if (navData.HasDestination && navData.TargetLatitude.HasValue && navData.TargetLongitude.HasValue)
    {
        distanceNm = CalculateDistanceNauticalMiles(
            navData.Latitude, navData.Longitude,
            navData.TargetLatitude.Value, navData.TargetLongitude.Value);

        // Calculate ETA if ship is moving
        if (navData.SpeedKnots > 0.1)
        {
            etaMinutes = (distanceNm / navData.SpeedKnots) * 60; // Convert hours to minutes
        }
    }

    return Results.Ok(new
    {
        hasDestination = navData.HasDestination,
        targetLatitude = navData.TargetLatitude,
        targetLongitude = navData.TargetLongitude,
        distanceNm = distanceNm,
        etaMinutes = etaMinutes,
        hasArrived = navData.HasArrived
    });
});

app.MapGet("/api/simulator/nav", () =>
{
    // Placeholder - real nav data comes via WebSocket, but some clients might poll
    return Results.Ok(new
    {
        timestampUtc = DateTime.UtcNow,
        latitude = 59.0,
        longitude = 10.0,
        speedKnots = 0.0,
        headingDegrees = 0.0,
        hasDestination = false
    });
});

app.Map("/ws/nav", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        IConnection? natsConnection = null;
        int retries = 10;

        // Prøv å koble til NATS med retry
        for (int i = 0; i < retries; i++)
        {
            try
            {
                natsConnection = new ConnectionFactory().CreateConnection("nats://nats:4222");
                app.Logger.LogInformation("Koblet til NATS-server");
                break;
            }
            catch (Exception ex)
            {
                app.Logger.LogWarning($"NATS-tilkobling feilet ({i + 1}/{retries}): {ex.Message}");
                await Task.Delay(2000);
            }
        }

        if (natsConnection == null)
        {
            app.Logger.LogError("Kunne ikke koble til NATS etter flere forsøk.");
            context.Response.StatusCode = 503;
            return;
        }

        using var nats = natsConnection;
        using var ws = await context.WebSockets.AcceptWebSocketAsync();

        var cts = new CancellationTokenSource();
        var subNav = nats.SubscribeAsync("sim.sensors.nav", (s, a) =>
        {
            try
            {
                if (ws.State != System.Net.WebSockets.WebSocketState.Open) return;
                var json = Encoding.UTF8.GetString(a.Message.Data);

                // Cache navigation data for API endpoint
                try
                {
                    var navData = JsonSerializer.Deserialize<NavigationData>(json);
                    if (navData != null)
                    {
                        lock (navDataLock)
                        {
                            lastNavigationData = navData;
                        }
                    }
                }
                catch (Exception cacheEx)
                {
                    app.Logger.LogDebug("Navigation data caching failed: {Message}", cacheEx.Message);
                }

                var buffer = Encoding.UTF8.GetBytes(json);
                _ = ws.SendAsync(buffer, System.Net.WebSockets.WebSocketMessageType.Text, true, cts.Token);
            }
            catch (Exception ex)
            {
                // Unngå spam – logg én gang og lukk
                app.Logger.LogDebug("WebSocket send feilet: {Message}", ex.Message);
                cts.Cancel();
            }
        });

        var subEnv = nats.SubscribeAsync("sim.sensors.env", (s, a) =>
        {
            try
            {
                if (ws.State != System.Net.WebSockets.WebSocketState.Open) return;
                var json = Encoding.UTF8.GetString(a.Message.Data);
                var buffer = Encoding.UTF8.GetBytes(json);
                _ = ws.SendAsync(buffer, System.Net.WebSockets.WebSocketMessageType.Text, true, cts.Token);
            }
            catch (Exception ex)
            {
                app.Logger.LogDebug("WebSocket send feilet (env): {Message}", ex.Message);
                cts.Cancel();
            }
        });

        var subAlarm = nats.SubscribeAsync("alarm.triggers", (s, a) =>
        {
            try
            {
                if (ws.State != System.Net.WebSockets.WebSocketState.Open) return;
                var json = Encoding.UTF8.GetString(a.Message.Data);
                var buffer = Encoding.UTF8.GetBytes(json);
                _ = ws.SendAsync(buffer, System.Net.WebSockets.WebSocketMessageType.Text, true, cts.Token);
            }
            catch (Exception ex)
            {
                app.Logger.LogDebug("WebSocket send feilet (alarm): {Message}", ex.Message);
                cts.Cancel();
            }
        });

        var buffer2 = new byte[1]; // vi forventer ikke data fra klienten; liten buffer
        try
        {
            while (ws.State == System.Net.WebSockets.WebSocketState.Open && !cts.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer2), cts.Token);
                if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    break;
                }
            }
        }
        catch (System.Net.WebSockets.WebSocketException)
        {
            // Stillegående – typisk klienten lukket uten handshake
            app.Logger.LogDebug("WebSocket frakoblet uventet (klientlukking)");
        }
        catch (OperationCanceledException) { }
        finally
        {
            cts.Cancel();
            try { subNav.Unsubscribe(); } catch { }
            try { subEnv.Unsubscribe(); } catch { }
            try { subAlarm.Unsubscribe(); } catch { }
        }
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

app.Run("http://0.0.0.0:5000");

// Helper method to calculate distance in nautical miles
static double CalculateDistanceNauticalMiles(double lat1, double lon1, double lat2, double lon2)
{
    // Convert latitude and longitude from degrees to radians
    double lat1Rad = lat1 * Math.PI / 180.0;
    double lon1Rad = lon1 * Math.PI / 180.0;
    double lat2Rad = lat2 * Math.PI / 180.0;
    double lon2Rad = lon2 * Math.PI / 180.0;

    // Haversine formula
    double deltaLat = lat2Rad - lat1Rad;
    double deltaLon = lon2Rad - lon1Rad;

    double a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
               Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
               Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);

    double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

    // Earth's radius in nautical miles (approximately 3440.065 nautical miles)
    double earthRadiusNm = 3440.065;
    double distanceNm = earthRadiusNm * c;

    return distanceNm;
}
