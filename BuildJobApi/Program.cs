using Amazon.S3;
using BuildJobApi.Hubs;
using BuildJobApi.Services;
using BuildJobApi.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

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

builder.Services.AddSingleton<IVirusScanService>(new VirusScanService(Environment.GetEnvironmentVariable("CLAMAV_CONNECTION_STRING") ?? "tcp://127.0.0.1:3310"));
builder.Services.AddSingleton<IHubCallerService, HubCallerService>();
builder.Services.AddSingleton<IBuildService, BuildService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseHttpsRedirection();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseRouting();

app.UseStaticFiles();

app.MapControllers();

app.MapGet("/", async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync("wwwroot/index.html");
});

app.MapHub<BuildOutputHub>("/buildOutputHub");

app.Run();