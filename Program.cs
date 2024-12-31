
using Microsoft.Extensions.FileProviders;
using WebPeli.Controllers;
using WebPeli.GameEngine;
using WebPeli.GameEngine.Managers;
using WebPeli.GameEngine.Systems;
using WebPeli.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<MessageCapturingProvider>();
builder.Services.AddLogging(builder =>
{
    builder.Services.AddSingleton<ILoggerProvider>(sp => sp.GetRequiredService<MessageCapturingProvider>());
});

builder.Services.AddSingleton(sp => new Dictionary<Plant, PlantRequirements>
{
    { Plant.Tree, PlantTemplates.OakTree },  // For now just using oak as default tree
    { Plant.Grass, PlantTemplates.Grass },   // Basic grass template
    { Plant.Weed, PlantTemplates.Weed },     // Basic weed template
    { Plant.Flower, PlantTemplates.Flower }  // Basic flower template
});

builder.Services.AddSingleton<PlantFSM>();

// Init managers
builder.Services.AddSingleton<ViewportManager>();
builder.Services.AddSingleton<EntityRegister>();
builder.Services.AddSingleton<MapManager>();
builder.Services.AddSingleton<AiManager>();
builder.Services.AddSingleton<AssetManager>();

// Init systems
builder.Services.AddSingleton<TimeSystem>();
builder.Services.AddSingleton<MetabolismSystem>();
builder.Services.AddSingleton<MovementSystem>();
builder.Services.AddSingleton<VegetationSystem>();
builder.Services.AddSingleton<HarvestSystem>();
builder.Services.AddSingleton<HealthSystem>();


// Debug service
builder.Services.AddSingleton<DebugDataService>();

// Start the engine
builder.Services.AddHostedService<GameEngineService>();

// Transport services
builder.Services.AddControllers();

builder.WebHost.UseUrls("http://localhost:5000");

var Aurinport = builder.Build();
var debugDataService = Aurinport.Services.GetRequiredService<DebugDataService>();
_ = debugDataService.StartDebugLoop(Aurinport.Lifetime.ApplicationStopping);

Aurinport.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});
Aurinport.UseStaticFiles();

Aurinport.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "node_modules")),
    RequestPath = "/node_modules"
});

Aurinport.MapGet("/", async context =>
{
    await context.Response.SendFileAsync(Path.Combine(builder.Environment.WebRootPath, "index.html"));
});
Aurinport.MapGet("/debug", async context =>
{
    await context.Response.SendFileAsync(Path.Combine(builder.Environment.WebRootPath, "debug/debug_index.html"));
});
Aurinport.MapGet("/tileEditor", async context =>
{
    await context.Response.SendFileAsync(Path.Combine(builder.Environment.WebRootPath, "tileEditor/tileEditor.html"));
});




Aurinport.MapControllers();
Aurinport.Run();
