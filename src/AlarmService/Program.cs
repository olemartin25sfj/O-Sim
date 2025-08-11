using System.Text.Json;
using NATS.Client;
using OSim.Shared.Messages;

// Enkel alarm service som overvåker navigasjons- og miljødata og publiserer AlarmTriggered ved terskelbrudd.
// Regler (første versjon):
// 1. Overspeed: speedKnots > 20
// 2. OffCourse: avvik mellom headingDegrees og courseOverGroundDegrees > 15 grader (hvis begge finnes)
// 3. HighWind: windSpeedKnots > 25
// 4. StrongCurrent: currentSpeedKnots > 4
// Debounce: Publiser maks én alarm per type per 30 sek hvis fortsatt aktiv.

var lastAlarm = new Dictionary<string, DateTime>();
TimeSpan alarmCooldown = TimeSpan.FromSeconds(30);

IConnection? connection = null;
var retries = 10;
for (int i = 0; i < retries; i++)
{
    try
    {
        var opts = ConnectionFactory.GetDefaultOptions();
        opts.Url = "nats://nats:4222";
        connection = new ConnectionFactory().CreateConnection(opts);
        Console.WriteLine("AlarmService: Tilkoblet NATS");
        break;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"AlarmService: NATS-tilkobling feilet ({i + 1}/{retries}): {ex.Message}");
        await Task.Delay(2000);
    }
}

if (connection == null)
{
    Console.WriteLine("AlarmService: Avslutter pga manglende NATS-tilkobling");
    return;
}

void PublishAlarm(string type, string message, AlarmSeverity severity)
{
    var now = DateTime.UtcNow;
    if (lastAlarm.TryGetValue(type, out var last) && now - last < alarmCooldown)
    {
        return; // debounce
    }
    lastAlarm[type] = now;

    var alarm = new AlarmTriggered(
        TimestampUtc: now,
        AlarmType: type,
        Message: message,
        Severity: severity
    );
    var json = JsonSerializer.Serialize(alarm);
    connection.Publish("alarm.triggers", System.Text.Encoding.UTF8.GetBytes(json));
    Console.WriteLine($"AlarmService publiserte alarm: {json}");
}

// Abonner på navigasjonsdata
connection.SubscribeAsync("sim.sensors.nav", (s, a) =>
{
    try
    {
        var json = System.Text.Encoding.UTF8.GetString(a.Message.Data);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        double? speed = root.TryGetProperty("speedKnots", out var sp) && sp.TryGetDouble(out var sv) ? sv :
                         root.TryGetProperty("SpeedKnots", out var sp2) && sp2.TryGetDouble(out var sv2) ? sv2 : null;
        double? heading = root.TryGetProperty("headingDegrees", out var hd) && hd.TryGetDouble(out var hv) ? hv :
                           root.TryGetProperty("HeadingDegrees", out var hd2) && hd2.TryGetDouble(out var hv2) ? hv2 :
                           root.TryGetProperty("heading", out var hd3) && hd3.TryGetDouble(out var hv3) ? hv3 : null;
        double? cog = root.TryGetProperty("courseOverGroundDegrees", out var cg) && cg.TryGetDouble(out var cv) ? cv :
                       root.TryGetProperty("CourseOverGroundDegrees", out var cg2) && cg2.TryGetDouble(out var cv2) ? cv2 : null;

        if (speed.HasValue && speed.Value > 20)
        {
            PublishAlarm("Overspeed", $"Speed {speed:F1} kts > 20", AlarmSeverity.Warning);
        }
        if (heading.HasValue && cog.HasValue)
        {
            var diff = Math.Abs(heading.Value - cog.Value);
            if (diff > 180) diff = 360 - diff;
            if (diff > 15)
            {
                PublishAlarm("OffCourse", $"Heading/COG diff {diff:F1}° > 15°", AlarmSeverity.Info);
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"AlarmService nav parse error: {ex.Message}");
    }
});

// Abonner på miljødata
connection.SubscribeAsync("sim.sensors.env", (s, a) =>
{
    try
    {
        var json = System.Text.Encoding.UTF8.GetString(a.Message.Data);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        double? wind = root.TryGetProperty("windSpeedKnots", out var ws) && ws.TryGetDouble(out var wsv) ? wsv :
                        root.TryGetProperty("WindSpeedKnots", out var ws2) && ws2.TryGetDouble(out var wsv2) ? wsv2 : null;
        double? current = root.TryGetProperty("currentSpeedKnots", out var cs) && cs.TryGetDouble(out var csv) ? csv :
                           root.TryGetProperty("CurrentSpeedKnots", out var cs2) && cs2.TryGetDouble(out var csv2) ? csv2 :
                           root.TryGetProperty("CurrentSpeed", out var cs3) && cs3.TryGetDouble(out var csv3) ? csv3 : null;

        if (wind.HasValue && wind.Value > 25)
        {
            PublishAlarm("HighWind", $"Wind {wind:F1} kts > 25", AlarmSeverity.Warning);
        }
        if (current.HasValue && current.Value > 4)
        {
            PublishAlarm("StrongCurrent", $"Current {current:F1} kts > 4", AlarmSeverity.Info);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"AlarmService env parse error: {ex.Message}");
    }
});

Console.WriteLine("AlarmService kjører. Ctrl+C for å avslutte.");
await Task.Delay(Timeout.Infinite);
