using System.Text;
using System.Text.Json;
using GatewayProxyService;
using NATS.Client;

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
        var sub = nats.SubscribeAsync("sim.sensors.nav", (s, a) =>
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
                // Unngå spam – logg én gang og lukk
                app.Logger.LogDebug("WebSocket send feilet: {Message}", ex.Message);
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
            try { sub.Unsubscribe(); } catch { }
        }
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

app.Run("http://0.0.0.0:5000");
