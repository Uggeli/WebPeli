
using WebPeli.GameEngine;
using WebPeli.GameEngine.Managers;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ViewportManager>();
builder.Services.AddSingleton<EntityRegister>();

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
Aurinport.MapControllers();
Aurinport.Run();
