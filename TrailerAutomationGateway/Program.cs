using Microsoft.OpenApi.Models;
using TrailerAutomationGateway;

var builder = WebApplication.CreateBuilder(args);

// Fixed HTTP port for the gateway
const int gatewayPort = 5000;
builder.WebHost.UseUrls($"http://0.0.0.0:{gatewayPort}");

// Services
builder.Services.AddSingleton<ClientRegistry>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TrailerAutomation Gateway API",
        Version = "v1",
        Description = "Gateway for TrailerAutomation RV ecosystem."
    });
});

var app = builder.Build();

// Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "TrailerAutomation Gateway API v1");
        c.RoutePrefix = "swagger";
    });
}

// Redirect root to swagger
app.MapGet("/", () => Results.Redirect("/swagger"))
   .ExcludeFromDescription();

// Simple GET heartbeat for manual checks
app.MapGet("/api/heartbeat", () =>
{
    Console.WriteLine($"[Gateway] GET /api/heartbeat {DateTime.UtcNow:O}");
    return Results.Ok(new
    {
        status = "OK",
        timestampUtc = DateTime.UtcNow,
        service = "TrailerAutomationGateway"
    });
})
.WithName("HeartbeatGet")
.WithOpenApi();

// POST heartbeat: client identifies itself here
app.MapPost("/api/heartbeat", (HeartbeatRequest request, HttpContext httpContext, ClientRegistry registry) =>
{
    var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString();

    Console.WriteLine(
        $"[Gateway] POST /api/heartbeat {DateTime.UtcNow:O} " +
        $"ClientId={request.ClientId} " +
        $"Type={request.DeviceType ?? "n/a"} " +
        $"Name={request.FriendlyName ?? "n/a"} " +
        $"RemoteIP={remoteIp ?? "n/a"}");

    registry.RegisterHeartbeat(
        request.ClientId,
        request.DeviceType,
        request.FriendlyName,
        remoteIp
    );

    return Results.Ok(new
    {
        status = "OK",
        clientId = request.ClientId,
        timestampUtc = DateTime.UtcNow
    });
})
.WithName("HeartbeatPost")
.WithOpenApi();

// List all active clients
app.MapGet("/api/clients", (ClientRegistry registry) =>
{
    var clients = registry.GetAllClients();
    Console.WriteLine($"[Gateway] GET /api/clients {DateTime.UtcNow:O} Count={clients.Count}");
    return Results.Ok(clients);
})
.WithName("GetClients")
.WithOpenApi();

// Get a single client by id
app.MapGet("/api/clients/{clientId}", (string clientId, ClientRegistry registry) =>
{
    var client = registry.GetClient(clientId);
    Console.WriteLine(
        $"[Gateway] GET /api/clients/{clientId} {DateTime.UtcNow:O} " +
        $"Found={(client != null)}");
    return client is null ? Results.NotFound() : Results.Ok(client);
})
.WithName("GetClientById")
.WithOpenApi();

// mDNS advertising (assuming you already have MdnsHost implemented)
app.Lifetime.ApplicationStarted.Register(() =>
{
    try
    {
        MdnsHost.Start("TrailerAutomationGateway", gatewayPort);
        Console.WriteLine($"[mDNS] Advertising TrailerAutomationGateway on port {gatewayPort}");
    }
    catch (Exception ex)
    {
        Console.WriteLine("[Startup] Failed to start mDNS advertiser:");
        Console.WriteLine(ex);
    }
});

app.Lifetime.ApplicationStopping.Register(() =>
{
    try
    {
        MdnsHost.Stop();
        Console.WriteLine("[mDNS] Stopped mDNS advertiser");
    }
    catch (Exception ex)
    {
        Console.WriteLine("[Shutdown] Failed to stop mDNS advertiser cleanly:");
        Console.WriteLine(ex);
    }
});

app.Run();
