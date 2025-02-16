using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Threading.Tasks;
using System.IO;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Text;
using System.Collections.Generic;
using System.Linq;

[ApiController]
[Route("api/[controller]")]
public class ConvertController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConvertController> _logger;

    public ConvertController(IConfiguration configuration, ILogger<ConvertController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("convert")]
    public async Task<IActionResult> Convert()
    {
        var files = Request.Form.Files;
        if (files == null || files.Count == 0)
        {
            return BadRequest("Nessun file caricato");
        }

        var connectionString = _configuration.GetValue<string>("RabbitMQ:ConnectionString");
        if (string.IsNullOrEmpty(connectionString))
        {
            _logger.LogError("La stringa di connessione a RabbitMQ non è configurata.");
            return StatusCode(500, "Errore interno del server.");
        }

        var factory = new ConnectionFactory() { Uri = new Uri(connectionString) };
        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();
        {
            //Classic queue
            //await channel.QueueDeclareAsync(queue: "convert_queue", durable: false, exclusive: false, autoDelete: false, arguments: null);

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


            // Suddividi i file in batch (ad esempio, 10 file per batch)
            var batchSize = 10;
            var batches = files.Chunk(batchSize);

            foreach (var batch in batches)
            {
                foreach (var file in batch)
                {
                    var message = new { FileName = file.FileName, ContentType = file.ContentType, FileBytes = await GetFileBytes(file) };
                    var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

                    await channel.BasicPublishAsync(exchange: "", routingKey: "convert_queue", body: body);
                }

                // Attendi un po' tra un batch e l'altro (opzionale)
                await Task.Delay(250); // 250 ms di attesa tra i batch
            }
        }

        return Ok("Conversione in corso...");
    }

    private async Task<byte[]> GetFileBytes(IFormFile file)
    {
        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }
}