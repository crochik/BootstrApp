using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Data;
using Crochik.Dipper;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Data.Models;
using PI.Shared.Models;

namespace PI.Shared.Data.Adapters;

public class AppointmentAdapter
{
    private readonly ILogger<AppointmentAdapter> _logger;
    private readonly MongoConnection _connection;

    public AppointmentAdapter(ILogger<AppointmentAdapter> logger, MongoConnection connection)
    {
        _logger = logger;
        _connection = connection;
    }

    public async Task<IEnumerable<Appointment>> GetByTypeAsync(Guid appointmentTypeId, DateTime? start, IQueryParams parms = null)
    {
        // TODO: hanlde sort?
        // ...

        var query = _connection.Filter<Appointment>()
            .Limit(parms?.Top)
            .Skip(parms?.Skip)
            .Eq(x => x.AppointmentTypeId, appointmentTypeId);

        if (start.HasValue)
        {
            query.Gte(x => x.Start, start.Value);
        }

        return await query.FindAsync();
    }
        
    public async Task<IEnumerable<Appointment>> GetForLeadAsync(Guid leadId)
        => await _connection.Filter<Appointment>()
            .Eq(x => x.LeadId, leadId)
            .FindAsync();

    
        
    public async Task<AppointmentAggregation> AggregateByToolAsync(IEntityContext context, DateTime start, DateTime end)
    {
        var rows = await _connection.DipperAggregateAsync<AppointmentByToolRow>(
            "Report.AppointmentsByTool",
            "fci",
            new
            {
                Start = start,
                End = end
            });

        var list = new List<AppointmentAggregation.Row>();
        foreach (var row in rows)
        {
            list.Add(new AppointmentAggregation.Row
            {
                Tool = "Callcenter",
                Active = row.Callcenter
            });
            list.Add(new AppointmentAggregation.Row
            {
                Tool = "Lumin",
                Active = row.Lumin
            });
            list.Add(new AppointmentAggregation.Row
            {
                Tool = "WebScheduler",
                Active = row.WebScheduler
            });
            list.Add(new AppointmentAggregation.Row
            {
                Tool = "Other",
                Active = row.Other
            });
        }

        return new AppointmentAggregation
        {
            Start = start,
            End = end,
            EntityId = context.EntityId.Value,
            Data = list
        };
    }
        
    [BsonIgnoreExtraElements]
    public class AppointmentByToolRow
    {
        [BsonId]
        public Guid Id { get; set; }
        public string Name { get; set; }
        public int Callcenter { get; set; }
        public int Lumin { get; set; }
        public int WebScheduler { get; set; }
        public int Other { get; set; }
        public int Total { get; set; }
    }
}