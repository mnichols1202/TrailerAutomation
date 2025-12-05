using Microsoft.OpenApi.Models;
using TrailerAutomationGateway;

var builder = WebApplication.CreateBuilder(args);

// Fixed HTTP port for the gateway
const int gatewayPort = 5000;
builder.WebHost.UseUrls($"http://0.0.0.0:{gatewayPort}");

// Services
// ClientRegistry with 60-second heartbeat tolerance (clients send every 60s, allow 3 missed = 180s timeout)
builder.Services.AddSingleton(new ClientRegistry(TimeSpan.FromSeconds(60), maxMissedHeartbeats: 3));
builder.Services.AddSingleton<SensorReadingRepository>();  // LiteDB persistence
builder.Services.AddSingleton<SensorReadingRegistry>();    // sensor registry
builder.Services.AddScoped<DeviceCommandService>();        // device command service
builder.Services.AddHttpClient();                          // HttpClient for Blazor components
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri($"http://localhost:{gatewayPort}") });
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
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

// Middleware
app.UseStaticFiles();
app.UseAntiforgery();

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

// Map Razor Components
app.MapRazorComponents<TrailerAutomationGateway.Components.App>()
    .AddInteractiveServerRenderMode();

// Redirect root to sensor readings page
app.MapGet("/", () => Results.Redirect("/sensors"))
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

    // Check if device is fully registered (has command port and capabilities)
    var client = registry.GetClient(request.ClientId);
    var needsRegistration = client == null || client.CommandPort == 0;

    return Results.Ok(new
    {
        status = "OK",
        clientId = request.ClientId,
        timestampUtc = DateTime.UtcNow,
        needsRegistration = needsRegistration
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

// Register device for command routing
app.MapPost("/api/devices/register", (DeviceRegistrationRequest request, ClientRegistry clientRegistry) =>
{
    try
    {
        clientRegistry.RegisterDevice(
            request.ClientId,
            request.DeviceType,
            request.FriendlyName,
            request.IpAddress,
            request.CommandPort,
            request.Capabilities,
            request.Relays
        );

        Console.WriteLine(
            $"[Device] POST /api/devices/register {DateTime.UtcNow:O} " +
            $"ClientId={request.ClientId} " +
            $"IP={request.IpAddress} " +
            $"Port={request.CommandPort} " +
            $"Capabilities=[{string.Join(", ", request.Capabilities ?? Array.Empty<string>())}]");

        return Results.Ok(new
        {
            status = "OK",
            clientId = request.ClientId,
            timestampUtc = DateTime.UtcNow
        });
    }
    catch (ArgumentException ex)
    {
        Console.WriteLine(
            $"[Device] POST /api/devices/register {DateTime.UtcNow:O} " +
            $"FAILED: {ex.Message}");

        return Results.BadRequest(new
        {
            status = "ERROR",
            message = ex.Message,
            timestampUtc = DateTime.UtcNow
        });
    }
})
.WithName("RegisterDevice")
.WithOpenApi();

// Get all registered devices
app.MapGet("/api/devices", (ClientRegistry clientRegistry) =>
{
    var clients = clientRegistry.GetAllClients();
    Console.WriteLine($"[Device] GET /api/devices {DateTime.UtcNow:O} Count={clients.Count}");
    return Results.Ok(clients);
})
.WithName("GetDevices")
.WithOpenApi();

// Get a single device by id
app.MapGet("/api/devices/{clientId}", (string clientId, ClientRegistry clientRegistry) =>
{
    var client = clientRegistry.GetClient(clientId);
    Console.WriteLine(
        $"[Device] GET /api/devices/{clientId} {DateTime.UtcNow:O} " +
        $"Found={(client != null)}");
    return client is null ? Results.NotFound() : Results.Ok(client);
})
.WithName("GetDeviceById")
.WithOpenApi();

// Send setRelay command to a device
app.MapPost("/api/devices/{clientId}/commands/setRelay", async (
    string clientId,
    SetRelayCommandRequest request,
    DeviceCommandService commandService) =>
{
    Console.WriteLine(
        $"[Device] POST /api/devices/{clientId}/commands/setRelay {DateTime.UtcNow:O} " +
        $"RelayId={request.RelayId} State={request.State}");

    var result = await commandService.SendSetRelayCommandAsync(
        clientId,
        request.RelayId,
        request.State);

    if (result.Success)
    {
        return Results.Ok(result);
    }
    else
    {
        return Results.BadRequest(result);
    }
})
.WithName("SendSetRelayCommand")
.WithOpenApi();

// Toggle relay (for button control)
app.MapPost("/api/devices/{clientId}/relays/{relayId}/toggle", async (
    string clientId,
    string relayId,
    DeviceCommandService commandService) =>
{
    Console.WriteLine(
        $"[Device] POST /api/devices/{clientId}/relays/{relayId}/toggle {DateTime.UtcNow:O}");

    // Send getRelayState command to device
    var getStateCommand = new DeviceCommand
    {
        CommandId = Guid.NewGuid().ToString(),
        Type = "getRelayState",
        Payload = System.Text.Json.JsonSerializer.SerializeToElement(new { relayId = relayId })
    };
    
    var stateResult = await commandService.SendCommandAsync(clientId, getStateCommand);
    
    if (stateResult == null || !stateResult.Success)
    {
        return Results.BadRequest(new { success = false, message = "Failed to get relay state" });
    }
    
    // Parse current state from response
    var dataElement = (System.Text.Json.JsonElement)stateResult.Data;
    var currentState = dataElement.TryGetProperty("state", out var stateProp) 
        ? stateProp.GetString() 
        : "off";
    
    // Toggle to opposite state
    string newState = (currentState == "on") ? "off" : "on";
    
    Console.WriteLine($"[Device] Toggling relay {relayId}: {currentState} -> {newState}");
    
    var result = await commandService.SendSetRelayCommandAsync(clientId, relayId, newState);

    if (result.Success)
    {
        return Results.Ok(result);
    }
    else
    {
        return Results.BadRequest(result);
    }
})
.WithName("ToggleRelay")
.WithOpenApi();

// Receive relay state notification from client (when local button changes relay state)
app.MapPost("/api/devices/{clientId}/relays/{relayId}/state", (
    string clientId,
    string relayId,
    string state,
    ClientRegistry clientRegistry,
    HttpContext httpContext) =>
{
    var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString();
    
    Console.WriteLine(
        $"[RelayState] POST /api/devices/{clientId}/relays/{relayId}/state {DateTime.UtcNow:O} " +
        $"State={state} " +
        $"RemoteIP={remoteIp ?? "n/a"}");
    
    // Update relay state in registry for UI synchronization
    clientRegistry.UpdateRelayState(clientId, relayId, state);
    
    return Results.Ok(new
    {
        status = "OK",
        clientId = clientId,
        relayId = relayId,
        state = state,
        timestampUtc = DateTime.UtcNow
    });
})
.WithName("NotifyRelayState")
.WithOpenApi();

// Receive temperature and humidity readings from a client
app.MapPost("/api/sensor-readings", (SensorReadingRequest request, HttpContext httpContext, SensorReadingRegistry registry) =>
{
    var nowUtc = DateTime.UtcNow;
    var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString();

    // Normalize clientId to avoid null/whitespace issues
    var clientId = string.IsNullOrWhiteSpace(request.ClientId)
        ? "unknown"
        : request.ClientId;

    Console.WriteLine(
        $"[Sensor] POST /api/sensor-readings {nowUtc:O} " +
        $"ClientId={clientId} " +
        $"TempC={request.TemperatureC:F2} " +
        $"Humidity={request.HumidityPercent:F2} " +
        $"RemoteIP={remoteIp ?? "n/a"}");

    registry.RegisterReading(
        clientId,
        request.TemperatureC,
        request.HumidityPercent,
        nowUtc,
        remoteIp
    );

    return Results.Ok(new
    {
        status = "OK",
        timestampUtc = nowUtc
    });
})
.WithName("PostSensorReading")
.WithOpenApi();

// Get all latest sensor readings
app.MapGet("/api/sensor-readings", (SensorReadingRegistry registry) =>
{
    var readings = registry.GetAllReadings();
    Console.WriteLine($"[Sensor] GET /api/sensor-readings {DateTime.UtcNow:O} Count={readings.Count}");
    return Results.Ok(readings);
})
.WithName("GetSensorReadings")
.WithOpenApi();

// Get latest sensor reading for a specific client
app.MapGet("/api/sensor-readings/{clientId}", (string clientId, SensorReadingRegistry registry) =>
{
    var reading = registry.GetReading(clientId);
    Console.WriteLine(
        $"[Sensor] GET /api/sensor-readings/{clientId} {DateTime.UtcNow:O} " +
        $"Found={(reading != null)}");

    return reading is null ? Results.NotFound() : Results.Ok(reading);
})
.WithName("GetSensorReadingByClientId")
.WithOpenApi();

// Get historical sensor readings for a specific client
app.MapGet("/api/sensor-readings/{clientId}/history", (
    string clientId,
    SensorReadingRepository repository,
    DateTime? from = null,
    DateTime? to = null,
    int limit = 1000) =>
{
    // Limit to reasonable range
    limit = Math.Clamp(limit, 1, 10000);

    var readings = repository.GetHistory(clientId, from, to, limit);
    var count = readings.Count();

    Console.WriteLine(
        $"[Sensor] GET /api/sensor-readings/{clientId}/history {DateTime.UtcNow:O} " +
        $"From={from?.ToString("O") ?? "n/a"} To={to?.ToString("O") ?? "n/a"} " +
        $"Limit={limit} Count={count}");

    return Results.Ok(readings);
})
.WithName("GetSensorReadingHistory")
.WithOpenApi();

// Get all historical sensor readings (across all clients)
app.MapGet("/api/sensor-readings/all/history", (
    SensorReadingRepository repository,
    DateTime? from = null,
    DateTime? to = null,
    int limit = 1000) =>
{
    // Limit to reasonable range
    limit = Math.Clamp(limit, 1, 10000);

    var readings = repository.GetAllHistory(from, to, limit);
    var count = readings.Count();

    Console.WriteLine(
        $"[Sensor] GET /api/sensor-readings/all/history {DateTime.UtcNow:O} " +
        $"From={from?.ToString("O") ?? "n/a"} To={to?.ToString("O") ?? "n/a"} " +
        $"Limit={limit} Count={count}");

    return Results.Ok(readings);
})
.WithName("GetAllSensorReadingHistory")
.WithOpenApi();

// Get database statistics
app.MapGet("/api/sensor-readings/stats", (SensorReadingRepository repository) =>
{
    var totalCount = repository.GetTotalCount();
    Console.WriteLine($"[Sensor] GET /api/sensor-readings/stats {DateTime.UtcNow:O} Total={totalCount}");

    return Results.Ok(new
    {
        totalReadings = totalCount,
        timestampUtc = DateTime.UtcNow
    });
})
.WithName("GetSensorReadingStats")
.WithOpenApi();

// Delete all sensor readings (admin function)
app.MapDelete("/api/sensor-readings/reset", (SensorReadingRepository repository) =>
{
    var count = repository.GetTotalCount();
    var deletedCount = repository.DeleteAll();
    
    Console.WriteLine(
        $"[Sensor] DELETE /api/sensor-readings/reset {DateTime.UtcNow:O} " +
        $"Deleted={deletedCount} readings");

    return Results.Ok(new
    {
        status = "OK",
        deletedCount = deletedCount,
        timestampUtc = DateTime.UtcNow
    });
})
.WithName("ResetSensorReadings")
.WithOpenApi();

// mDNS advertising
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
        Console.WriteLine("[Shutdown] Stopped mDNS advertiser");
    }
    catch (Exception ex)
    {
        Console.WriteLine("[Shutdown] Failed to stop mDNS advertiser cleanly:");
        Console.WriteLine(ex);
    }
});

app.Run();
