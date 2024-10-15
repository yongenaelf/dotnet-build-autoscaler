using BuildJobApi.Hubs;
using BuildJobApi.Interfaces;
using BuildJobApi.Services;
using BuildJobShared.Services;
using BuildJobShared.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

builder.Services.AddSingleton<IObjectStorageService, ObjectStorageService>();
builder.Services.AddSingleton<IKafkaService, KafkaService>();
builder.Services.AddSingleton<IVirusScanService, VirusScanService>();
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