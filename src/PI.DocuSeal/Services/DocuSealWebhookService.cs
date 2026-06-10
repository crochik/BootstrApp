using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using Crochik.Mongo;
using MongoDB.Bson;
using PI.DocuSeal.Models;
using PI.Shared.Constants;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Services;

namespace PI.DocuSeal.Services;

public class DocuSealWebhookService
{
    private readonly DocuSealWebhookConfiguration _config;
    private readonly ILogger<DocuSealWebhookService> _logger;
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;

    public DocuSealWebhookService(IOptions<DocuSealWebhookConfiguration> config, ILogger<DocuSealWebhookService> logger, MongoConnection connection, ObjectTypeService objectTypeService)
    {
        _config = config.Value;
        _logger = logger;
        _connection = connection;
        _objectTypeService = objectTypeService;
    }

    public bool VerifyWebhookSignature(string payload, string signature, string secret)
    {
        try
        {
            if (string.IsNullOrEmpty(signature))
            {
                _logger.LogWarning("Webhook signature is missing");
                return false;
            }

            // DocuSeal uses HMAC-SHA256 for webhook signatures
            // Signature format: "sha256=<hash>"
            if (!signature.StartsWith("sha256="))
            {
                _logger.LogWarning("Invalid webhook signature format: {Signature}", signature);
                return false;
            }

            var expectedHash = signature.Substring(7); // Remove "sha256=" prefix
            var computedHash = ComputeHmacSha256(payload, secret);

            var isValid = string.Equals(expectedHash, computedHash, StringComparison.OrdinalIgnoreCase);

            if (!isValid)
            {
                _logger.LogWarning("Webhook signature verification failed");
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying webhook signature");
            return false;
        }
    }

    public async Task<bool> ProcessWebhookEventAsync(IEntityContext context, BsonDocument webhookEvent)
    {
        var integration = await _connection.Filter<DocuSealIntegrationConfiguration>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.EntityId, context.EntityId)
            .Eq(x => x.IntegrationId, IntegrationIds.DocuSeal)
            .FirstOrDefaultAsync();

        if (integration == null) throw new ForbiddenException("Integration");

        var now = DateTime.UtcNow;
        var evt = new Event
        {
            Id = Guid.CreateVersion7(),
            CreatedOn = now,
            AccountId = context.AccountId.Value,
            EntityId = context.EntityId.Value,
            Body = webhookEvent,
            FlowId = integration.EventFlowId,
            ObjectStatusId = integration.EventObjectStatusId,
        };

        if (webhookEvent.TryGetValue("data", out var dataObj) && dataObj is BsonDocument data)
        {
            if (data.TryGetValue("id", out var idObj))
            {
                var submission = await _connection.Filter<DocuSealSubmission>()
                    .Eq(x => x.AccountId, context.AccountId)
                    .Eq(x => x.ExternalId, idObj.ToInt32())
                    .FirstOrDefaultAsync();

                if (submission != null)
                {
                    evt.EntityId = submission.EntityId;
                    evt.Parent = submission.Parent;
                    evt.SubmissionId = submission.Id;
                }
            }
        }
        
        await _connection.InsertAsync(evt);
        await _objectTypeService.FireCreateEventAsync(context, evt);

        return true;
    }
    
    public bool IsEventTypeAllowed(string eventType)
    {
        return _config.AllowedEventTypes.Contains(eventType, StringComparer.OrdinalIgnoreCase);
    }

    private static string ComputeHmacSha256(string data, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}