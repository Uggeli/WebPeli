
using WebPeli.GameEngine;
using WebPeli.GameEngine.Managers;
using WebPeli.GameEngine.Systems;


var builder = WebApplication.CreateBuilder(args);

// Init managers
builder.Services.AddSingleton<ViewportManager>();
builder.Services.AddSingleton<EntityRegister>();
builder.Services.AddSingleton<MapManager>();
builder.Services.AddSingleton<AiManager>();

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

var Aurinport = builder.Build();

Aurinport.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});
Aurinport.UseStaticFiles();
Aurinport.MapGet("/", async context =>
{
    await context.Response.SendFileAsync(Path.Combine(builder.Environment.WebRootPath, "index.html"));
});
Aurinport.MapGet("/debug", async context =>
{
    await context.Response.SendFileAsync(Path.Combine(builder.Environment.WebRootPath, "debug.html"));
});
Aurinport.MapControllers();
Aurinport.Run();
