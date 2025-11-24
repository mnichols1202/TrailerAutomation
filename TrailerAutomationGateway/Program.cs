using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Basic configuration
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TrailerAutomation Gateway API",
        Version = "v1",
        Description = "Gateway for TrailerAutomation devices (Pi Zero 2W, ESP32-S3, etc.)"
    });
});

// If/when you want Razor Pages later, uncomment both lines below:
// builder.Services.AddRazorPages();

// Simple in-memory state or services can be added here
builder.Services.AddSingleton<IMdnsAnnouncer, MdnsAnnouncer>();

var app = builder.Build();

// Use Swagger in Development (you can enable in Production if desired)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "TrailerAutomation Gateway API v1");
        c.RoutePrefix = string.Empty; // Swagger at root: http://host/
    });
}

// Optional: HTTPS redirection when you put this behind TLS
// app.UseHttpsRedirection();

// If/when you enable Razor Pages:
// app.UseStaticFiles();
// app.UseRouting();
// app.MapRazorPages();

// Start mDNS advertisement (stub for now)
var mdnsAnnouncer = app.Services.GetRequiredService<IMdnsAnnouncer>();
mdnsAnnouncer.StartAdvertising("TrailerAutomationGateway", 5000);

// Minimal API endpoints
app.MapGet("/api/heartbeat", () =>
{
    return Results.Ok(new
    {
        status = "OK",
        timestamp = DateTime.UtcNow,
        service = "TrailerAutomationGateway"
    });
})
.WithName("Heartbeat")
.WithOpenApi();

app.MapGet("/api/ping", (string? message) =>
{
    return Results.Ok(new
    {
        request = string.IsNullOrWhiteSpace(message) ? "ping" : message,
        response = "pong",
        timestamp = DateTime.UtcNow
    });
})
.WithName("Ping")
.WithOpenApi();

// You can add more endpoints later, for example:
// - Device registration (from Pi Zero 2W client)
// - Status reporting
// - Command dispatch

app.Run();

public interface IMdnsAnnouncer
{
    void StartAdvertising(string serviceName, int port);
}

public sealed class MdnsAnnouncer : IMdnsAnnouncer
{
    public void StartAdvertising(string serviceName, int port)
    {
        // TODO: Implement true mDNS/Bonjour advertisement here using a suitable library.
        // For now this is just a placeholder so the architecture is in place.
        Console.WriteLine($"[mDNS] Advertising service '{serviceName}' on port {port} (stub).");
    }
}
