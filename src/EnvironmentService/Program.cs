using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using NATS.Client;
using OSim.Shared.Messages;

class Program
{
    static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();

        app.MapGet("/api/environment/status", () =>
        {
            return Results.Ok(new { Status = "Running", Timestamp = DateTime.UtcNow });
        });

        // Start HTTP server
        _ = app.RunAsync("http://0.0.0.0:5002");

        IConnection? connection = null;
        int retries = 10;

        // Prøv å koble til NATS med retry
        for (int i = 0; i < retries; i++)
        {
            try
            {
                var opts = ConnectionFactory.GetDefaultOptions();
                opts.Url = "nats://nats:4222"; // Endret fra localhost til nats for Docker
                connection = new ConnectionFactory().CreateConnection(opts);
                Console.WriteLine("Koblet til NATS-server");
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"NATS-tilkobling feilet ({i + 1}/{retries}): {ex.Message}");
                await Task.Delay(2000);
            }
        }

        if (connection == null)
        {
            Console.WriteLine("Kunne ikke koble til NATS etter flere forsøk. Avslutter.");
            return;
        }

        using (connection)
        {
            var rand = new Random();

            while (true)
            {
                var env = new EnvironmentData
                {
                    Timestamp = DateTime.UtcNow,
                    WindSpeedKnots = 5 + rand.NextDouble() * 10,
                    WindDirection = rand.Next(0, 360),
                    CurrentSpeed = 1 + rand.NextDouble() * 2,
                    CurrentDirection = rand.Next(0, 360),
                    WaveHeight = rand.NextDouble() * 2,
                    WaveDirection = rand.Next(0, 360),
                    WavePeriod = 3 + rand.NextDouble() * 5
                };

                var json = JsonSerializer.Serialize(env);
                connection.Publish("sim.sensors.env", System.Text.Encoding.UTF8.GetBytes(json));
                Console.WriteLine($"Publiserte miljødata: {json}");

                await Task.Delay(1000);
            }
        }
    }
}