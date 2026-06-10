using Webhook.N8n.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Discovers objects/events from the shared core assembly (the mock domain) and wires
// outbound delivery via Webhook.Publisher. Pass extra assemblies to expose objects
// defined elsewhere — no other changes required.
builder.Services.AddN8nIntegration(builder.Configuration);

var app = builder.Build();

// Gate every /n8n/* route behind the API key before MVC routing runs.
app.UseN8nApiKeyAuth();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

// Exposed so the test project can drive the app via WebApplicationFactory.
public partial class Program;
