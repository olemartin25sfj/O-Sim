using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NATS.Client;
using OSim.Shared.Messages;

class Program
{
    static async Task Main(string[] args)
    {
        var opts = ConnectionFactory.GetDefaultOptions();
        opts.Url = "nats://localhost:4222";
        using var connection = new ConnectionFactory().CreateConnection(opts);

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