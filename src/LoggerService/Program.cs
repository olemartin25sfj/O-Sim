using System.Globalization;
using System.Text;
using System.Text.Json;
using CsvHelper;
using NATS.Client;
using OSim.Shared.Messages;

class Program
{
    static void Main(string[] args)
    {
        var opts = ConnectionFactory.GetDefaultOptions();
        opts.Url = "nats://localhost:4222";
        using var connection = new ConnectionFactory().CreateConnection(opts);

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