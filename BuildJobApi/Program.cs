using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Confluent.Kafka;
using System.IO;
using System;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseRouting();

// Define the Kafka configuration
var kafkaBootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS") ?? "localhost:9092";
var kafkaConfig = new ProducerConfig { BootstrapServers = kafkaBootstrapServers };

// Define the endpoint for uploading files
app.MapPost("/upload", async (IFormFile file) =>
{
    if (file == null || file.Length == 0)
    {
        return Results.BadRequest("No file uploaded.");
    }

    // Generate a unique job ID
    var jobId = Guid.NewGuid().ToString();

    // Construct a new file name using the job ID and the .zip extension
    var fileExtension = Path.GetExtension(file.FileName);
    var newFileName = $"{jobId}{fileExtension}";

    var filePath = Path.Combine("uploads", newFileName);

    try
    {
        // Ensure the uploads directory exists
        if (!Directory.Exists("uploads"))
        {
            Directory.CreateDirectory("uploads");
        }

        // Save the uploaded file with the new name
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // Send a Kafka message with the filename
        using (var producer = new ProducerBuilder<Null, string>(kafkaConfig).Build())
        {
            var topic = "build_jobs";  // Define your Kafka topic
            var message = $"File uploaded: {newFileName}";

            await producer.ProduceAsync(topic, new Message<Null, string> { Value = message });
            // Use synchronous Flush to ensure delivery
            producer.Flush(TimeSpan.FromSeconds(10));
        }

        return Results.Ok(new { Message = "File uploaded successfully", JobId = jobId, FilePath = filePath });
    }
    catch (Exception ex)
    {
        // Return an object result with error message and status code
        return Results.Problem(detail: ex.Message, statusCode: 500, title: "Internal Server Error");
    }
}).DisableAntiforgery();

app.Run();
