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

        // NATS connection singleton
        IConnection? globalConnection = null;
        int retries = 10;

        // Prøv å koble til NATS med retry
        for (int i = 0; i < retries; i++)
        {
            try
            {
                var opts = ConnectionFactory.GetDefaultOptions();
                opts.Url = "nats://nats:4222";
                globalConnection = new ConnectionFactory().CreateConnection(opts);
                Console.WriteLine("Koblet til NATS-server");
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"NATS-tilkobling feilet ({i + 1}/{retries}): {ex.Message}");
                await Task.Delay(2000);
            }
        }

        if (globalConnection == null)
        {
            Console.WriteLine("Kunne ikke koble til NATS etter flere forsøk. Avslutter.");
            return;
        }

        builder.Services.AddSingleton(globalConnection);
        var app = builder.Build();

        app.MapGet("/api/environment/status", () =>
        {
            return Results.Ok(new { Status = "Running", Timestamp = DateTime.UtcNow });
        });

        app.MapPost("/api/environment/setmode", async (HttpContext context, IConnection nats) =>
        {
            try
            {
                using var reader = new StreamReader(context.Request.Body);
                var body = await reader.ReadToEndAsync();
                var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.TryGetProperty("mode", out var modeElement) && modeElement.ValueKind == JsonValueKind.String)
                {
                    var modeString = modeElement.GetString();
                    if (Enum.TryParse<EnvironmentMode>(modeString, true, out var mode))
                    {
                        var command = new SetEnvironmentModeCommand(DateTime.UtcNow, mode);
                        var json = JsonSerializer.Serialize(command);
                        nats.Publish("env.commands.setmode", System.Text.Encoding.UTF8.GetBytes(json));
                        return Results.Ok(new { Message = $"Environment mode set to {mode}" });
                    }
                }
                return Results.BadRequest("Invalid mode");
            }
            catch (Exception ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        // Start bakgrunnstjeneste for miljødata-publisering
        _ = Task.Run(async () =>
        {
            var rand = new Random();
            var currentMode = EnvironmentMode.Dynamic;

            // Abonner på miljømodus-kommandoer
            globalConnection.SubscribeAsync("env.commands.setmode", (sender, args) =>
            {
                try
                {
                    var json = System.Text.Encoding.UTF8.GetString(args.Message.Data);
                    var command = JsonSerializer.Deserialize<SetEnvironmentModeCommand>(json);
                    if (command != null)
                    {
                        currentMode = command.Mode;
                        Console.WriteLine($"EnvironmentService: Modus endret til {currentMode}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"EnvironmentService: Feil ved behandling av setmode-kommando: {ex.Message}");
                }
            });

            while (true)
            {
                var env = GenerateEnvironmentData(currentMode, rand);

                var json = JsonSerializer.Serialize(env);
                globalConnection.Publish("sim.sensors.env", System.Text.Encoding.UTF8.GetBytes(json));
                Console.WriteLine($"Publiserte miljødata ({currentMode}): {json}");

                await Task.Delay(1000);
            }
        });

        app.Run("http://0.0.0.0:5002");
    }

    private static EnvironmentData GenerateEnvironmentData(EnvironmentMode mode, Random rand)
    {
        return mode switch
        {
            EnvironmentMode.Static => new EnvironmentData(
                TimestampUtc: DateTime.UtcNow,
                Mode: mode,
                WindSpeedKnots: 5.0,
                WindDirectionDegrees: 270.0,
                CurrentSpeedKnots: 1.0,
                CurrentDirectionDegrees: 90.0,
                WaveHeightMeters: 0.5,
                WaveDirectionDegrees: 270.0,
                WavePeriodSeconds: 4.0
            ),
            EnvironmentMode.Calm => new EnvironmentData(
                TimestampUtc: DateTime.UtcNow,
                Mode: mode,
                WindSpeedKnots: rand.NextDouble() * 3.0,
                WindDirectionDegrees: rand.Next(0, 360),
                CurrentSpeedKnots: rand.NextDouble() * 0.5,
                CurrentDirectionDegrees: rand.Next(0, 360),
                WaveHeightMeters: rand.NextDouble() * 0.3,
                WaveDirectionDegrees: rand.Next(0, 360),
                WavePeriodSeconds: 3 + rand.NextDouble() * 2
            ),
            EnvironmentMode.Storm => new EnvironmentData(
                TimestampUtc: DateTime.UtcNow,
                Mode: mode,
                WindSpeedKnots: 20 + rand.NextDouble() * 15,
                WindDirectionDegrees: rand.Next(0, 360),
                CurrentSpeedKnots: 2 + rand.NextDouble() * 3,
                CurrentDirectionDegrees: rand.Next(0, 360),
                WaveHeightMeters: 3 + rand.NextDouble() * 4,
                WaveDirectionDegrees: rand.Next(0, 360),
                WavePeriodSeconds: 6 + rand.NextDouble() * 6
            ),
            EnvironmentMode.Dynamic => new EnvironmentData(
                TimestampUtc: DateTime.UtcNow,
                Mode: mode,
                WindSpeedKnots: 5 + rand.NextDouble() * 10,
                WindDirectionDegrees: rand.Next(0, 360),
                CurrentSpeedKnots: 1 + rand.NextDouble() * 2,
                CurrentDirectionDegrees: rand.Next(0, 360),
                WaveHeightMeters: rand.NextDouble() * 2,
                WaveDirectionDegrees: rand.Next(0, 360),
                WavePeriodSeconds: 3 + rand.NextDouble() * 5
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }
}