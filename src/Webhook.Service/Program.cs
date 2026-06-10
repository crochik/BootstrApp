using Webhook.Service.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Webhook definitions live in their own JSON file so they can be edited (and
// hot-reloaded) independently of the rest of appsettings.
builder.Configuration.AddJsonFile("webhooks.json", optional: true, reloadOnChange: true);

builder.Services.AddControllers();
builder.Services.AddWebhookEngine(builder.Configuration);

// Register custom IWebhookHandler implementations here, e.g.:
//   builder.Services.AddWebhookHandler<MyOrderCreatedHandler>();

var app = builder.Build();

// Buffer the request body so the controller can read the exact raw bytes even
// when MVC has already consumed the stream for form model binding. Required for
// signature schemes (HMAC, Twilio, ECDSA) that sign the raw body.
app.Use(async (context, next) =>
{
    context.Request.EnableBuffering();
    await next();
});

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

// Exposed so the test project can drive the app via WebApplicationFactory.
public partial class Program;
