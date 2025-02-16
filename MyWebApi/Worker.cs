using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

// Worker service per la conversione da JPEG a PNG
public class Worker : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<Worker> _logger;

    public Worker(IConfiguration configuration, ILogger<Worker> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    // Modello per i messaggi RabbitMQ
    public class ConvertMessage
    {
        required public string FileName { get; set; }
        required public string ContentType { get; set; }
        required public byte[] FileBytes { get; set; }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = _configuration.GetValue<string>("RabbitMQ:ConnectionString");
        if (string.IsNullOrEmpty(connectionString))
        {
            _logger.LogError("La stringa di connessione a RabbitMQ non è configurata.");
            return;
        }

        var factory = new ConnectionFactory() { Uri = new Uri(connectionString) };
        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();
        {
            //Classic queue
                //await channel.QueueDeclareAsync(
                //queue: "convert_queue",
                //durable: true,
                //exclusive: false,
                //autoDelete: false,
                //arguments: null
                // );

            // Quorum queue
            await channel.QueueDeclareAsync(
                queue: "convert_queue",
                durable: true,       // Rendi la coda persistente
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    { "x-queue-type", "quorum" } // Specifica che è una Quorum Queue
                }
            );

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = JsonSerializer.Deserialize<ConvertMessage>(body);

                if (message == null || message.FileBytes == null)
                {
                    _logger.LogError("Messaggio ricevuto non valido.");
                    await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
                    return;
                }

                _logger.LogInformation($"Ricevuto messaggio: {message.FileName}");

                try
                {
                    using var image = Image.Load(message.FileBytes);
                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Size = new Size(image.Width, image.Height),
                        Mode = ResizeMode.Pad,
                        Position = AnchorPositionMode.Center
                    }));

                    using var pngStream = new MemoryStream();
                    image.SaveAsPng(pngStream);

                    var pngBytes = pngStream.ToArray();
                    var pngUrl = await SavePng(message.FileName, pngBytes);

                    _logger.LogInformation($"Conversione completata: {pngUrl}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Errore durante la conversione di {message.FileName}");
                }

                await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
            };

            await channel.BasicConsumeAsync(queue: "convert_queue", autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
    }

    private async Task<string> SavePng(string fileName, byte[] pngBytes)
    {
        var outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
        Directory.CreateDirectory(outputDirectory); // Crea la cartella se non esiste

        var filePath = Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(fileName)}.png");
        await System.IO.File.WriteAllBytesAsync(filePath, pngBytes);

        return $"/images/{Path.GetFileNameWithoutExtension(fileName)}.png";
    }
}
