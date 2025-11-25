using Microsoft.OpenApi.Models;
using TrailerAutomationGateway;

var builder = WebApplication.CreateBuilder(args);

// Fixed HTTP port for the gateway (Pi 5 / PC)
const int gatewayPort = 5000;
builder.WebHost.UseUrls($"http://0.0.0.0:{gatewayPort}");

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TrailerAutomation Gateway API",
        Version = "v1",
        Description = "Gateway for TrailerAutomation RV ecosystem (Pi 4/5 or PC)."
    });
});

var app = builder.Build();

// Swagger UI in development (or always, if you prefer)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "TrailerAutomation Gateway API v1");
        c.RoutePrefix = "swagger";
    });
}

// Redirect root to swagger for convenience
app.MapGet("/", () => Results.Redirect("/swagger"))
   .ExcludeFromDescription();

// Simple heartbeat endpoint for clients to test connectivity
app.MapGet("/api/heartbeat", (HttpContext ctx) =>
{
    var remote = ctx.Connection.RemoteIpAddress;
    Console.WriteLine($"[Heartbeat] Request from {(remote?.ToString() ?? "unknown")}" );
    return Results.Ok(new
    {
        status = "OK",
        timestampUtc = DateTime.UtcNow,
        service = "TrailerAutomationGateway"
    });
})
.WithName("Heartbeat")
.WithOpenApi();

// Wire up mDNS advertising when the app starts / stops
app.Lifetime.ApplicationStarted.Register(() =>
{
    try
    {
        // Instance name can be whatever you like; keep it stable so the clients recognize it.
        MdnsHost.Start("TrailerAutomationGateway", gatewayPort);
        Console.WriteLine($"[mDNS] Advertised TrailerAutomationGateway on port {gatewayPort}.");
    }
    catch (Exception ex)
    {
        Console.WriteLine("[mDNS] Failed to start mDNS advertising.");
        Console.WriteLine(ex);
    }
});

app.Lifetime.ApplicationStopping.Register(() =>
{
    try
    {
        MdnsHost.Stop();
        Console.WriteLine("[mDNS] Stopped mDNS advertising.");
    }
    catch (Exception ex)
    {
        Console.WriteLine("[mDNS] Failed to stop mDNS advertising cleanly.");
        Console.WriteLine(ex);
    }
});

app.Run();
