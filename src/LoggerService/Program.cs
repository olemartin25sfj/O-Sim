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

        // Skriv header hvis filen nettopp ble opprettet (enkel sjekk på lengde)
        if (writer.BaseStream.Length == 0)
        {
            csv.WriteField("LoggedAtUtc");
            csv.WriteField("Subject");
            csv.WriteField("PayloadRaw");
            csv.WriteField("TimestampUtc");
            csv.WriteField("Latitude");
            csv.WriteField("Longitude");
            csv.WriteField("HeadingDegrees");
            csv.WriteField("SpeedKnots");
            csv.WriteField("WindSpeedKnots");
            csv.WriteField("WindDirectionDegrees");
            csv.WriteField("AlarmType");
            csv.WriteField("AlarmSeverity");
            csv.NextRecord();
            writer.Flush();
        }

        void HandleMessage(string subject, byte[] data)
        {
            var json = Encoding.UTF8.GetString(data);

            DateTime? ts = null;
            double? lat = null, lon = null, hdg = null, spd = null;
            double? windSpd = null, windDir = null;
            string? alarmType = null, alarmSeverity = null;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string[] tsCandidates = { "timestampUtc", "TimestampUtc", "timestamp", "Timestamp" };
                foreach (var c in tsCandidates)
                {
                    if (root.TryGetProperty(c, out var p) && p.ValueKind == JsonValueKind.String && DateTime.TryParse(p.GetString(), out var parsed)) { ts = parsed; break; }
                }

                if (root.TryGetProperty("latitude", out var latEl) && latEl.TryGetDouble(out var latV)) lat = latV;
                if (root.TryGetProperty("longitude", out var lonEl) && lonEl.TryGetDouble(out var lonV)) lon = lonV;
                if (root.TryGetProperty("headingDegrees", out var hdgEl) && hdgEl.TryGetDouble(out var hdgV)) hdg = hdgV;
                else if (root.TryGetProperty("heading", out var hdgEl2) && hdgEl2.TryGetDouble(out var hdgV2)) hdg = hdgV2;
                if (root.TryGetProperty("speedKnots", out var spdEl) && spdEl.TryGetDouble(out var spdV)) spd = spdV;
                else if (root.TryGetProperty("speed", out var spdEl2) && spdEl2.TryGetDouble(out var spdV2)) spd = spdV2;
                if (root.TryGetProperty("windSpeedKnots", out var wse) && wse.TryGetDouble(out var wsv)) windSpd = wsv;
                if (root.TryGetProperty("windDirectionDegrees", out var wde) && wde.TryGetDouble(out var wdv)) windDir = wdv;
                else if (root.TryGetProperty("windDirection", out var wde2) && wde2.TryGetDouble(out var wdv2)) windDir = wdv2;
                if (root.TryGetProperty("alarmType", out var at) && at.ValueKind == JsonValueKind.String) alarmType = at.GetString();
                else if (root.TryGetProperty("AlarmType", out var at2) && at2.ValueKind == JsonValueKind.String) alarmType = at2.GetString();
                if (root.TryGetProperty("severity", out var sev) && sev.ValueKind == JsonValueKind.String) alarmSeverity = sev.GetString();
                else if (root.TryGetProperty("Severity", out var sev2) && sev2.ValueKind == JsonValueKind.String) alarmSeverity = sev2.GetString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Feil ved parsing av JSON i LoggerService: {ex.Message}");
            }

            csv.WriteField(DateTime.UtcNow.ToString("O"));
            csv.WriteField(subject);
            csv.WriteField(json);
            csv.WriteField(ts?.ToString("O") ?? string.Empty);
            csv.WriteField(lat?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            csv.WriteField(lon?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            csv.WriteField(hdg?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            csv.WriteField(spd?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            csv.WriteField(windSpd?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            csv.WriteField(windDir?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            csv.WriteField(alarmType ?? string.Empty);
            csv.WriteField(alarmSeverity ?? string.Empty);
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