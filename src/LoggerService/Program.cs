using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading;
using CsvHelper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using NATS.Client;
using OSim.Shared.Messages;

class Program
{
    static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();

        app.MapGet("/api/logs/status", () =>
        {
            return Results.Ok(new { Status = "Running", Timestamp = DateTime.UtcNow });
        });

        // Start HTTP server
        _ = app.RunAsync("http://0.0.0.0:5003");
        var opts = ConnectionFactory.GetDefaultOptions();
        opts.Url = "nats://nats:4222";
        IConnection? connection = null;
        int retries = 10;
        for (int i = 0; i < retries; i++)
        {
            try
            {
                connection = new ConnectionFactory().CreateConnection(opts);
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"NATS-tilkobling feilet ({i + 1}/{retries}): {ex.Message}");
                Thread.Sleep(2000);
            }
        }
        if (connection == null)
        {
            Console.WriteLine("Kunne ikke koble til NATS etter flere forsøk. Avslutter.");
            return;
        }

        using var writer = new StreamWriter("logg.csv", append: true, Encoding.UTF8);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        void HandleMessage(string subject, byte[] data)
        {
            var json = Encoding.UTF8.GetString(data);
            csv.WriteField(DateTime.UtcNow);
            csv.WriteField(subject);
            csv.WriteField(json);
            csv.NextRecord();
            writer.Flush();
            Console.WriteLine($"Logget melding fra {subject}: {json}");
        }

        string[] emner = { "sim.sensors.nav", "sim.sensors.env", "log.entries" };
        foreach (var emne in emner)
        {
            connection.SubscribeAsync(emne, (s, a) => HandleMessage(emne, a.Message.Data));
        }

        Console.WriteLine("LoggerService kjører. Trykk Ctrl+C for å avslutte.");
        Thread.Sleep(Timeout.Infinite);
    }
}