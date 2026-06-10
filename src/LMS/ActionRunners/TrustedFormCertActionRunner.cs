using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Crochik.Mongo;
using LMS.Models;
using Messages.Flow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using PI.Shared.Constants;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Services;
using PI.Shared.Services.ActionRunners;
using TrustedForm;

namespace LMS.ActionRunners;

public class TrustedFormCertActionRunner : AbstractObjectRunner<TrustedFormCertActionOptions>
{
    private readonly ActiveProspectConfig _config;
    private readonly HttpClient _client;
    public override Guid ActionId => ActionIds.TrustedFormCert;

    public TrustedFormCertActionRunner(ILogger<TrustedFormCertActionRunner> logger, MongoConnection connection, ObjectTypeService objectTypeService, IConfiguration configuration, IHttpClientFactory clientFactory)
        : base(logger, connection, objectTypeService)
    {
        _config = configuration.GetSection("ActiveProspect").Get<ActiveProspectConfig>();
        _client = clientFactory.CreateClient(nameof(TrustedFormCertActionRunner));
    }

    protected async ValueTask<FlowEvent[]> RunAsync_Disabled(ActionRunnerContext context, TrustedFormCertActionOptions options)
    {
        var tags = new List<string> { "TrustedForm: DISABLED" };

        return await getResponseAsync("Didn't find Certificate");

        async Task<FlowEvent[]> getResponseAsync(string description = null)
        {
            if (tags.Count > 0)
            {
                await _connection.Filter<Transaction>()
                    .Eq(x => x.Id, context.ObjectId)
                    .Update
                    .AddToSetEach(x => x.Tags, tags)
                    .UpdateOneAsync();
            }

            return getEvents().ToArray();

            IEnumerable<FlowEvent> getEvents()
            {
                var output = options.Output.FirstOrDefault(x => x.Name == TrustedFormCertActionOptions.SuccessEvent);
                if (output?.EventId.HasValue ?? false)
                {
                    yield return new GenericFlowEvent(context.Event)
                    {
                        Action = nameof(ActionIds.TrustedFormCert),
                        EventTypeId = output.EventId,
                        Description = description ?? output.Description,
                    };
                }
            }
        }
    }

    protected override async ValueTask<FlowEvent[]> RunAsync(ActionRunnerContext context, TrustedFormCertActionOptions options)
    {
        if (context.ObjectType.Name != Transaction.ObjectTypeName)
        {
            throw new BadRequestException($"Only supported for {Transaction.ObjectTypeName}");
        }

        var runContext = context.Run.BuildHandlebarsContext(context.Event);
        var tags = new List<string>();

        if (!TryGet(context, runContext, options.Certificate, out string certificateUrl) || string.IsNullOrWhiteSpace(certificateUrl))
        {
            _logger.LogInformation("Nothing to do, couldn't find certificate at {Path}", options.Certificate);

            tags.Add(TrustedFormCertActionOptions.TAG_NOT_PROVIDED);

            return await getResponseAsync("Didn't find Certificate");
        }

        var trustedFormCertId = certificateUrl.Split("/")[^1];
        var cert = await _connection.Filter<TrustedFormCertificate>()
            .Eq(x => x.AccountId, context.Event.AccountId)
            .Eq(x => x.Id, trustedFormCertId)
            .FirstOrDefaultAsync();

        if (cert != null)
        {
            _logger.LogInformation("Has already seen {TrustedFormCertificate}", trustedFormCertId);

            if (!options.Retain)
            {
                tags.Add(TrustedFormCertActionOptions.TAG_DUPLICATE);
                return await getResponseAsync("Certificate has already been validated");
            }

            if (cert.Retained)
            {
                return await getResponseAsync("Certificate has already been retained");
            }
        }

        if (!TryGet(context, runContext, options.Phone, out string phone)) phone = null;
        if (!TryGet(context, runContext, options.Email, out string email)) email = null;
        if (phone == null || email == null) throw new BadRequestException($"Email or Phone Required");

        var resp = default(CertificateOperationResponse);
        try
        {
            var request = new CertificateOperationRequest
            {
                MatchLead = new MatchLeadPhoneEmailParameters
                {
                    Email = email,
                    Phone = phone,
                },
            };

            // insights
            if (options.Insights?.Count > 0 && cert?.Insights == null)
            {
                var insights = options.Insights.Values
                    .Select(x => x switch
                    {
                        "age_seconds" => InsightsParameters.PropertiesEnum.AgeSeconds,
                        "domain" => InsightsParameters.PropertiesEnum.Domain,
                        "ip" => InsightsParameters.PropertiesEnum.Ip,
                        "approx_ip_geo" => InsightsParameters.PropertiesEnum.ApproxIpGeo,
                        "form_input_method" => InsightsParameters.PropertiesEnum.FormInputMethod,
                        "seconds_on_page" => InsightsParameters.PropertiesEnum.SecondsOnPage,
                        "bot_detected" => InsightsParameters.PropertiesEnum.BotDetected,
                        _ => default(InsightsParameters.PropertiesEnum?),
                    })
                    .Where(x => x.HasValue)
                    .Select(x => x.Value)
                    .ToList();

                request.Insights = new InsightsParameters
                {
                    Properties = insights,
                };
            }

            // retain
            if (options.Retain)
            {
                if (!TryGet(context, runContext, options.Vendor, out string vendor))
                {
                    vendor = "LeadsPiper.com";
                }

                if (!TryGet(context, runContext, options.VendorId, out string vendorId))
                {
                    vendorId = context.ObjectId.ToString();
                }

                request.Retain = new RetainParameters
                {
                    Vendor = vendor,
                    Reference = vendorId,
                };
            }

            resp = await request.Execute(_client, _config.APIKey, trustedFormCertId);
        }
        catch (RequestException ex)
        {
            _logger.LogInformation("API error: {TrustedFormId}: {ErrorCode}, {Body}", trustedFormCertId, ex.StatusCode, ex.Message);

            if (ex.StatusCode == HttpStatusCode.NotFound)
            {
                tags.Add(TrustedFormCertActionOptions.TAG_NOT_FOUND);
            }
            else
            {
                tags.Add($"TrustedForm: API {ex.StatusCode}");
            }

            return await getResponseAsync(ex.Message);
        }

        try
        {
            if (
                resp?.Outcome != CertificateOperationResponse.OutcomeEnum.Success ||
                resp.MatchLead?.Result?.EmailMatch != true ||
                resp.MatchLead?.Result?.PhoneMatch != true
            )
            {
                // operation failed or not a match
                tags.Add(TrustedFormCertActionOptions.TAG_INVALID);

                return await getResponseAsync(resp.Reason);
            }

            var parsedInsights = new Dictionary<string, object>();

            if (resp.Insights?.Properties != null)
            {
                var insights = resp.Insights.Properties;
                if (insights.SecondsOnPage.HasValue) parsedInsights.Add(TrustedFormCertActionOptions.SecondsOnPage, insights.SecondsOnPage);
                if (!string.IsNullOrWhiteSpace(insights.Domain)) parsedInsights.Add(TrustedFormCertActionOptions.Domain, insights.Domain);
                if (!string.IsNullOrWhiteSpace(insights.Ip)) parsedInsights.Add(TrustedFormCertActionOptions.Ip, insights.Ip);
                if (insights.ApproxIpGeo != null) parsedInsights.Add(TrustedFormCertActionOptions.ApproxIpGeo, insights.ApproxIpGeo);
                if (insights.FormInputMethod != null) parsedInsights.Add(TrustedFormCertActionOptions.FormInputMethod, insights.FormInputMethod);
                if (insights.BotDetected != null) parsedInsights.Add(TrustedFormCertActionOptions.BotDetected, insights.BotDetected);
                if (insights.AgeSeconds != null) parsedInsights.Add(TrustedFormCertActionOptions.AgeSeconds, insights.AgeSeconds);
            }

            if (options.Insights?.Count > 0 && parsedInsights.Count == 0)
            {
                tags.Add(TrustedFormCertActionOptions.TAG_NO_INSIGHTS);
            }

            var updates = new Dictionary<string, object>();
            if (options.Insights?.Count > 0 && parsedInsights.Count > 0)
            {
                foreach (var kvp in options.Insights)
                {
                    if (!parsedInsights.TryGetValue(kvp.Value, out var value)) continue;
                    SetObjectValue(updates, kvp.Key, value);
                }
            }

            if (updates.Count > 0)
            {
                // other updates (other than tags)
                var expando = await _objectTypeService.GetExpandoObjectByIdAsync(context.EntityContext, context.ObjectType, context.ObjectId);
                var result = await _objectTypeService.UpdateObjectAsync(context.EntityContext, context.ObjectType, updates, context.ObjectId, expando, new ObjectTypeService.UpdateObjectOptions
                {
                    PartialUpdate = true,
                });

                if (!result)
                {
                    _logger.LogError("Failed to set insights");
                }
            }

            var upsertCert = _connection.Filter<TrustedFormCertificate>()
                    .Eq(x => x.AccountId, context.Run.AccountId)
                    .Eq(x => x.Id, trustedFormCertId)
                    .Update
                    .SetOnInsert(x => x.AccountId, context.Run.AccountId)
                    .SetOnInsert(x => x.Id, trustedFormCertId)
                    .SetOnInsert(x => x.CreatedOn, DateTime.UtcNow)
                    .Set(x => x.Validated, true)
                ;

            if (parsedInsights.Count > 0)
            {
                upsertCert.Set(x => x.Insights, parsedInsights);
            }

            if (options.Retain)
            {
                upsertCert.Set(x => x.Retained, true);
            }

            await upsertCert.UpdateAndGetOneAsync(true);

            tags.AddRange(getTags());

            return await getResponseAsync();

            IEnumerable<string> getTags()
            {
                yield return TrustedFormCertActionOptions.TAG;

                if (options.Retain) yield return TrustedFormCertActionOptions.TAG_RETAINED;

                if (parsedInsights.TryGetValue(TrustedFormCertActionOptions.AgeSeconds, out var ageSecondsObj) && ageSecondsObj is int ageSeconds)
                {
                    if (ageSeconds < 60) yield return TrustedFormCertActionOptions.TAG_FRESH;
                    else if (ageSeconds < 300) yield return TrustedFormCertActionOptions.TAG_5MINUTES;
                    else if (ageSeconds < 600) yield return TrustedFormCertActionOptions.TAG_10MINUTES;
                    else if (ageSeconds < 1800) yield return TrustedFormCertActionOptions.TAG_30MINUTES;
                    else if (ageSeconds < 3600) yield return TrustedFormCertActionOptions.TAG_1HOUR;
                    else if (ageSeconds < 12 * 60 * 60) yield return TrustedFormCertActionOptions.TAG_12HOURS;
                    else if (ageSeconds < 24 * 60 * 60) yield return TrustedFormCertActionOptions.TAG_24HOURS;
                    else yield return TrustedFormCertActionOptions.TAG_STALE;
                }

                // 8ff8394e0c544936a28466564a30ddd9dd679913.masked.trustedform.com
                if (parsedInsights.TryGetStrParam(TrustedFormCertActionOptions.Domain, out var domain))
                {
                    if (domain.EndsWith(".masked.trustedform.com"))
                    {
                        yield return $"TrustedForm: {domain.Split('.')[0][^8..]}";
                    }
                    else
                    {
                        yield return domain;
                    }
                }

                if (parsedInsights.TryGetValue(TrustedFormCertActionOptions.BotDetected, out var detected) && detected is bool botDetected && botDetected)
                {
                    yield return TrustedFormCertActionOptions.TAG_BOT;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process trusted form action: {Response}", resp?.ToJson() ?? "[NULL]");
            tags.Add($"TrustedForm: ERROR");

            return await getResponseAsync(ex.Message);
        }

        async Task<FlowEvent[]> getResponseAsync(string description = null)
        {
            if (tags.Count > 0)
            {
                await _connection.Filter<Transaction>()
                    .Eq(x => x.Id, context.ObjectId)
                    .Update
                    .AddToSetEach(x => x.Tags, tags)
                    .UpdateOneAsync();
            }

            return getEvents().ToArray();

            IEnumerable<FlowEvent> getEvents()
            {
                var output = options.Output.FirstOrDefault(x => x.Name == TrustedFormCertActionOptions.SuccessEvent);
                if (output?.EventId.HasValue ?? false)
                {
                    yield return new GenericFlowEvent(context.Event)
                    {
                        Action = nameof(ActionIds.TrustedFormCert),
                        EventTypeId = output.EventId,
                        Description = description ?? output.Description,
                    };
                }
            }
        }
    }
}