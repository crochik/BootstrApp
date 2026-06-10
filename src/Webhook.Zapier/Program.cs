using Webhook.Zapier.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Discovers objects/events from this assembly (the mock domain). Pass additional
// assemblies here to expose objects defined elsewhere — no other changes required.
builder.Services.AddZapierIntegration(builder.Configuration);

var app = builder.Build();

// Gate every /zapier/* route behind the API key before MVC routing runs.
app.UseZapierApiKeyAuth();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

// Exposed so the test project can drive the app via WebApplicationFactory.
public partial class Program;
