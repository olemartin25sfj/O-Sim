using System.Text;
using System.Text.Json;
using NATS.Client;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseWebSockets();

app.Map("/ws/nav", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        using var nats = new ConnectionFactory().CreateConnection("nats://nats:4222");
        using var ws = await context.WebSockets.AcceptWebSocketAsync();

        var cts = new CancellationTokenSource();
        var sub = nats.SubscribeAsync("sim.sensors.nav", (s, a) =>
        {
            var json = Encoding.UTF8.GetString(a.Message.Data);
            var buffer = Encoding.UTF8.GetBytes(json);
            ws.SendAsync(buffer, System.Net.WebSockets.WebSocketMessageType.Text, true, cts.Token).Wait();
        });

        var buffer2 = new byte[1024];
        while (ws.State == System.Net.WebSockets.WebSocketState.Open)
        {
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer2), cts.Token);
            if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Close)
                break;
        }
        cts.Cancel();
        sub.Unsubscribe();
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

app.Run("http://0.0.0.0:5000");
