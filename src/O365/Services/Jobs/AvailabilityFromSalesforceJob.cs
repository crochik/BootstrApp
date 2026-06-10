using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Crochik.Dipper;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Services;

public class AvailabilityFromSalesforceJob : IRunJob
{
    private readonly MongoConnection _connection;

    public string Name => "AvailabilityFromSalesforce";

    public AvailabilityFromSalesforceJob(MongoConnection connection)
    {
        _connection = connection;
    }

    public async Task<JobResult> ExecuteAsync(IEntityContext context, CancellationToken stoppingToken)
    {
        var users = await _connection.DipperAggregateAsync<Record>("OperatingHours", "sf", new { context.AccountId });

        var organizations = new Dictionary<Guid, SchedulerSettings>();
        var list = new List<WriteModel<User>>();
        foreach (var user in users)
        {
            var apptType = await GetOrCreateAsync(context, user.OrganizationId, user.Duration);

            if (user.OrganizationId.HasValue)
            {
                if (!organizations.TryGetValue(user.OrganizationId.Value, out var organization))
                {
                    // first time seeing org 
                    organization = new SchedulerSettings
                    {
                        Id = Model.NewGuid(),
                        AccountId = context.AccountId.Value,
                        EntityId = user.OrganizationId.Value,
                        Name = "Scheduler",
                        Description = $"Auto generated Scheduler configuration",
                        CreatedOn = DateTime.UtcNow,
                        LastActor = context.Actor(),
                        // LeadTypeId = 
                        // ExternalId = ...
                    };

                    organizations.Add(user.OrganizationId.Value, organization);
                }

                // add user to settings 
                organization.Settings = (organization.Settings ?? Enumerable.Empty<UserSchedulingSettings>())
                    .Append(new UserSchedulingSettings
                    {
                        UserId = user.Id,
                        AppointmentTypeId = apptType.Id,
                        Priority = (int)user.Priority,
                        // Constraints = 
                    })
                    .ToArray();
            }

            var availability = user.Slots.Select(x =>
            {
                var startTime = x.StartTime.Split(":");
                var endTime = x.EndTime.Split(":");
                var startMinutes = int.Parse(startTime[0]) * 60 + int.Parse(startTime[1]);
                var endMinutes = int.Parse(endTime[0]) * 60 + int.Parse(endTime[1]);

                return new Availability
                {
                    Id = Guid.NewGuid(),
                    StartMinutes = startMinutes,
                    DurationMinutes = endMinutes - startMinutes,
                    DayId = x.DayOfWeek,
                    AppointmentTypeIds = new[]
                    {
                        apptType.Id
                    }
                };
            });

            var model = _connection.Filter<Entity, User>()
                .Eq(x => x.Id, user.Id)
                .Update
                .Set(x => x.TimeZoneId, user.TimeZone)
                .Set(x => x.Availability, availability)
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .Set(x => x.LastActor, context.Actor())
                .UpdateOneModel();

            list.Add(model);
            if (list.Count > 100)
            {
                await _connection.BulkWriteAsync(list);
                list.Clear();
            }
        }

        if (list.Count > 0)
        {
            await _connection.BulkWriteAsync(list);
            list.Clear();
        }

        // save organization scheduler settings
        if (organizations.Count > 0)
        {
            var existing = await _connection.Filter<SchedulerSettings>()
                .Eq(x => x.AccountId, context.AccountId)
                .In(x => x.EntityId, organizations.Keys)
                .IncludeField(x => x.EntityId)
                .IncludeField(x => x.Id)
                .FindAsync();

            var existingOrgConfigs = existing.Select(x => x.EntityId).Distinct().ToHashSet();

            foreach (var organization in organizations)
            {
                if (organization.Value.Settings != null)
                {
                    organization.Value.Settings = organization.Value.Settings
                        .OrderBy(x => x.Priority)
                        .ToArray();
                }

                if (existingOrgConfigs.Contains(organization.Key))
                {
                    // update 
                    await _connection.Filter<SchedulerSettings>()
                        .Eq(x => x.AccountId, context.AccountId)
                        .Eq(x => x.EntityId, organization.Key)
                        .Update
                        .Set(x => x.Settings, organization.Value.Settings)
                        .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                        .Set(x => x.LastActor, context.Actor())
                        .UpdateManyAsync();
                }
                else
                {
                    await _connection.InsertAsync(organization.Value);
                }
            }
        }

        return new JobResult
        {
            Message = $"Imported availability for {users.Count}",
        };
    }

    private async ValueTask<AppointmentType> GetOrCreateAsync(IEntityContext context, Guid? organizationId, int duration)
    {
        var apptType = await _connection.Filter<AppointmentType>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.EntityId, organizationId ?? context.AccountId)
            .Eq(x => x.Settings.Duration, duration)
            .FirstOrDefaultAsync();

        if (apptType == null)
        {
            // TODO: use objecttypeservice?
            // ...

            apptType = new AppointmentType
            {
                Id = Guid.NewGuid(),
                AccountId = context.AccountId.Value,
                EntityId = organizationId ?? context.AccountId.Value,
                // LeadTypeId = 
                CreatedOn = DateTime.UtcNow,
                LastActor = context.Actor(),
                LastModifiedOn = DateTime.UtcNow,
                Settings = new SchedulingSettings
                {
                    Duration = duration,
                    MinMinutesFromNow = 120,
                },
                    
                // get from some account settings... 
                Name = $"{duration}min in Home Consultation",
                // ICalTitle = "",
                // ICalSummary = "",
            };

            await _connection.InsertAsync(apptType);

            // TODO: fire event
            // ...
        }

        return apptType;
    }

    public class TimeSlot
    {
        public string Type { get; set; }
        public bool IsDeleted { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public DayOfWeek DayOfWeek { get; set; }
    }

    public class Record
    {
        [BsonId] public Guid Id { get; set; }
        public string Name { get; set; }
        public Guid? OrganizationId { get; set; }
        public string ServiceResourceId { get; set; }
        public string OperationHoursId { get; set; }
        public string TimeZone { get; set; }
        public TimeSlot[] Slots { get; set; }
        public decimal Efficiency { get; set; }
        public int Duration { get; set; }
        public decimal Priority { get; set; }
    }
}