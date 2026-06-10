// using System;
// using System.Collections.Generic;
// using System.Threading.Tasks;
// using AutoMapper;
// using Crochik.Messaging;
// using Crochik.NET.APM;
// using Microsoft.Extensions.Configuration;
// using Microsoft.Extensions.Logging;
// using Newtonsoft.Json;
// using PI.Shared.Data.Adapters;
// using PI.Shared.Models.Singer;

// namespace Services
// {
//     public class PySingerService : AbstractMessageQueueService
//     {
//         private readonly IMapper _mapper;
//         private readonly LoaderService _loader;
//         private readonly ILeadTypeAdapter _leadTypeAdapter;
//         private readonly IEntityAdapter _entityAdapter;
//         private readonly ISingerConfigAdapter _configAdapter;

//         // TODO: make it into a real cache (evict old, ...)
//         private Dictionary<Guid, SingerImportConfig> ConfigCache { get; set; } = new Dictionary<Guid, SingerImportConfig>();

//         public PySingerService(
//             ILogger<SingerService> logger,
//             IMapper mapper,
//             IConfiguration configuration,
//             IMessageBroker messageBroker,
//             IAPMService apmService,
//             LoaderService loader,
//             IEntityAdapter entityAdapter,
//             ILeadTypeAdapter leadTypeAdapter,
//             ISingerConfigAdapter configAdapter
//             ) : base(logger, configuration, messageBroker, apmService)
//         {
//             this._configAdapter = configAdapter;
//             this._entityAdapter = entityAdapter;
//             this._leadTypeAdapter = leadTypeAdapter;
//             this._mapper = mapper;
//             this._loader = loader;
//         }

//         protected override void Init(IMessageQueue messageQueue, TypeMapper mapper)
//         {
//             MessageBroker.Bind(messageQueue, "singer.#");
//         }

//         protected override async Task OnMessageAsync(IMessage evt)
//         {
//             var timer = DateTime.UtcNow;
//             try
//             {
//                 if (await ProcessMessageAsync(evt))
//                 {
//                     evt.Acknowledge();
//                 }
//             }
//             finally
//             {
//                 // evt.Acknowledge();
//                 Logger.LogDebug(evt.RoutingKey + ": " + (DateTime.UtcNow - timer).TotalMilliseconds);
//             }
//         }

//         private async Task<bool> ProcessMessageAsync(IMessage evt)
//         {
//             var parts = evt.RoutingKey.Split('.');
//             if (!Guid.TryParse(parts[1], out var configId))
//             {
//                 Logger.LogError("Invalid {route}", evt.RoutingKey);
//                 return false;
//             }

//             var body = evt.Body;

//             // singer.{configId}.{record|stream|activate}.{stream}
//             // singer.{configId}.{target}.{state}
//             if (parts.Length != 4)
//             {
//                 Logger.LogError("Invalid {route}", string.Join('.', parts));
//                 return false;
//             }

//             switch (parts[2])
//             {
//                 case "record":
//                     break;

//                 case "target":
//                     // return false;
//                     break;

//                 case "schema":
//                 case "activate":
//                 default:
//                     return false;
//             }

//             if (!(body is string json))
//             {
//                 Logger.LogError("Unexpected {bodyType}", body.GetType().Name);
//                 return false;
//             }

//             var config = await GetConfigAsync(configId);
//             if (config == null)
//             {
//                 Logger.LogError("Import Configuration not found for {importConfigId}", configId);
//                 return false;
//             }

//             if (parts[2] == "target")
//             {
//                 if (parts[3] == "state")
//                 {
//                     config.State = JsonConvert.DeserializeObject<SingerState>(json);
//                     return true;
//                 }

//                 return true;
//             }

//             if (parts[2] == "record")
//             {
//                 if (!config.Streams.TryGetValue(parts[3], out var streamConfig))
//                 {
//                     Logger.LogWarning("No configuration for {stream}", parts[3]);
//                     return false;
//                 }

//                 // TODO: this will fail :)
//                 var job = default(SingerJob);

//                 return streamConfig switch
//                 {
//                     CachedLeadStreamConfig lead => await _loader.LoadLeadAsync(LoaderService.Context(job, lead), json),
//                     AppointmentStreamConfig appt => await _loader.LoadAppoitmentAsync(LoaderService.Context(job, appt), json),
//                     OrganizationStreamConfig org => await _loader.LoadOrganizationAsync(LoaderService.Context(job, org), json),
//                     UserStreamConfig user => await _loader.LoadUserAsync(LoaderService.Context(job, user), json),
//                     OrganizationMembershipStreamConfig membership => await _loader.LoadOrgMembershipAsync(LoaderService.Context(job, membership), json),
//                     _ => false
//                 };
//             }

//             return true;
//         }

//         private async Task<SingerImportConfig> GetConfigAsync(Guid id)
//         {
//             if (ConfigCache.TryGetValue(id, out var config)) return config;

//             var loadedConfig = await _configAdapter.GetByIdAsync(id);
//             if (loadedConfig == null) return null;

//             var map = new Dictionary<string, SingerStreamConfig>();
//             foreach (var stream in loadedConfig.Streams)
//             {
//                 switch (stream.Value)
//                 {
//                     case LeadStreamConfig lead:
//                         map.Add(stream.Key, await ResolveAsync(lead));
//                         break;

//                     default:
//                         map.Add(stream.Key, stream.Value);
//                         break;
//                 }
//             }

//             loadedConfig.Streams = map;

//             ConfigCache.Add(id, loadedConfig);

//             return loadedConfig;
//         }

//         private async Task<CachedLeadStreamConfig> ResolveAsync(LeadStreamConfig config)
//         {
//             var map = _mapper.Map<CachedLeadStreamConfig>(config);
//             var leadType = await _leadTypeAdapter.GetByIdAsync(config.LeadTypeId);
//             var entity = await _entityAdapter.GetByIdAsync(leadType.EntityId);
//             map.LeadType = leadType;
//             map.Context = entity.Context;

//             return map;
//         }
//     }
// }