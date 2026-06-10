// using System;
// using System.Collections.Generic;
// using System.Threading.Tasks;
// using System.Linq;
// using Microsoft.Extensions.Logging;
// using System.Threading;
// using Microsoft.Extensions.Configuration;
// using PI.Shared.Data;
// using PI.Shared.O365;

// namespace Services
// {
//     public class RenewServiceForTenants
//     {
//         private readonly ILogger<RenewServiceForTenants> _logger;
//         private readonly IO365TenantAdapter _tenantRepo;
//         private readonly CalendarService _calendar;
//         private readonly Config _config;

//         public RenewServiceForTenants(
//             ILogger<RenewServiceForTenants> logger,
//             IConfiguration config,
//             IO365TenantAdapter tenantRepo,
//             CalendarService calendar
//             )
//         {
//             this._logger = logger;
//             this._tenantRepo = tenantRepo;
//             this._calendar = calendar;

//             _config = config.GetSection("RenewSubscription").Get<Config>();
//         }

//         private async Task<List<TenantSubscription>> LoadAsync()
//         {
//             var list = new List<TenantSubscription>();
//             var tenants = await _tenantRepo.GetAsync();

//             foreach (var tenant in tenants)
//             {
//                 try
//                 {
//                     var s = await _calendar.GetSubscriptionsForTenantAsync(tenant.Id.Value);
//                     _logger.LogDebug("Found {subscriptions} subscriptions for {tenantId}", s.Count, tenant.Id);

//                     foreach (var subscription in s)
//                     {
//                         list.Add(new TenantSubscription
//                         {
//                             TenantId = tenant.Id.Value,
//                             Id = subscription.Id,
//                             ExpiresOn = subscription.ExpiresOn
//                         });
//                     }
//                 }
//                 catch (Exception ex)
//                 {
//                     _logger.LogError(ex, "Failed to get subscriptions for {tenantId}", tenant.Id);
//                 }
//             }

//             return list;
//         }

//         public void Start()
//         {
//             _logger.LogInformation("Starting");

//             Task.Run(() => RunAsync());
//         }

//         private async Task RunAsync()
//         {
//             while (true)
//             {
//                 var threshold = DateTime.UtcNow
//                     .AddDays(_config.Threshold.Days)
//                     .AddHours(_config.Threshold.Hours)
//                     .AddMinutes(_config.Threshold.Minutes);

//                 _logger.LogDebug("Start Renew: {threshold}", threshold);

//                 var all = await LoadAsync();
//                 var list = all.Where(s => s.ExpiresOn.CompareTo(threshold) < 0);
//                 await ProcessAsync(list);

//                 _logger.LogTrace("Goes to sleep");
//                 await Task.Delay(TimeSpan.FromMinutes(_config.SleepMinutes));
//             }
//         }

//         private async Task ProcessAsync(IEnumerable<TenantSubscription> list)
//         {
//             foreach (var s in list)
//             {
//                 _logger.LogDebug("Renew {subscriptionId} for {tenantId}", s.Id, s.TenantId);
//                 try
//                 {
//                     var subscription = await _calendar.RenewSubscriptionAsync(s.TenantId, s.Id);
//                     _logger.LogDebug("New Expiration Time for {subscriptionId} is {expiration}", s.Id, subscription.ExpirationDateTime);

//                     s.ExpiresOn = subscription.ExpirationDateTime.Value;
//                 }
//                 catch (Exception ex)
//                 {
//                     _logger.LogError(ex, "Failed to renew {subscription} for {tenantId}", s.Id, s.TenantId);
//                 }
//             }
//         }

//         internal class TenantSubscription
//         {
//             public string Id { get; set; }
//             public DateTimeOffset ExpiresOn { get; set; }
//             public Guid TenantId { get; set; }
//         }

//         internal class ConfigThreshold
//         {
//             public int Days { get; set; } = 0;
//             public int Hours { get; set; } = 0;
//             public int Minutes { get; set; } = 0;
//         }

//         internal class Config
//         {
//             public int SleepMinutes { get; set; } = 5;
//             public ConfigThreshold Threshold { get; set; }
//         }
//     }
// }