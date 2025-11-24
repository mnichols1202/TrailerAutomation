using Microsoft.OpenApi.Models;
using TrailerAutomationGateway;

var builder = WebApplication.CreateBuilder(args);

// Make sure the gateway listens on a fixed port
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
        Description = "Gateway for TrailerAutomation devices"
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
        c.RoutePrefix = string.Empty;
    });
}

// Start mDNS advertising BEFORE serving requests
MdnsHost.Start("TrailerAutomationGateway", gatewayPort);

// Simple heartbeat endpoint
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

app.Run();
