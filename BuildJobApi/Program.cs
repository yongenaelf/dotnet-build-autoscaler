using Amazon.S3;
using Confluent.Kafka;
using Shared.Services;
using Shared.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

#region ObjectStorageService configuration
var awsAccessKeyId = Environment.GetEnvironmentVariable("MINIO_ACCESS_KEY") ?? "minio";
var awsSecretAccessKey = Environment.GetEnvironmentVariable("MINIO_SECRET_KEY") ?? "minio123";
var clientConfig = new AmazonS3Config
{
    ServiceURL = Environment.GetEnvironmentVariable("MINIO_ENDPOINT") ?? "http://localhost:9000",
    ForcePathStyle = true
};
var bucketName = Environment.GetEnvironmentVariable("MINIO_BUCKET_NAME") ?? "job-requests";
builder.Services.AddSingleton<IObjectStorageService>(new ObjectStorageService(awsAccessKeyId, awsSecretAccessKey, clientConfig, bucketName));
#endregion

#region Event Bus configuration
var kafkaBootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS") ?? "localhost:9093";
var kafkaConfig = new ProducerConfig { BootstrapServers = kafkaBootstrapServers };
builder.Services.AddSingleton<IEventPublishService>(new EventPublishService(kafkaConfig));

var kafkaConsumerConfig = new ConsumerConfig
{
    GroupId = "websocket-consumer-group",
    BootstrapServers = kafkaBootstrapServers,
    AutoOffsetReset = AutoOffsetReset.Earliest
};
builder.Services.AddSingleton<IEventSubscribeService>(new EventSubscribeService(kafkaConsumerConfig));
#endregion

builder.Services.AddSingleton<IVirusScanService>(new VirusScanService(Environment.GetEnvironmentVariable("CLAMAV_CONNECTION_STRING") ?? "tcp://127.0.0.1:3310"));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseRouting();

app.UseWebSockets(new WebSocketOptions()
{
    KeepAliveInterval = TimeSpan.FromMinutes(2)
});

app.UseStaticFiles();

app.MapControllers();

app.MapGet("/", async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync("wwwroot/index.html");
});

app.Run();