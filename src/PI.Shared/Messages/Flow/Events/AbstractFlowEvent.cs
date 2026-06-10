using System;
using System.Collections.Generic;
using Crochik.Messaging;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using PI.Shared.Models;

namespace Messages.Flow;

[DiscriminatorWithFallback]
[BsonDiscriminator(Required = true)]
[BsonKnownTypes(
    typeof(GenericFlowEvent),
    typeof(EntityEvent),
    typeof(AutoRefillSettingsUpdated),
    typeof(BalanceUpdatedEvent),
    typeof(LeadEvent),
    typeof(LeadWithAppointmentEvent),
    typeof(TransactionEvent),
    typeof(CreateInvoiceEvent),
    typeof(PaymentStatusUpdateEvent),
    typeof(AdjustmentEvent),
    typeof(DisputeEvent)
)]
public class FlowEvent : IMessageBody
{
    public string Action { get; set; }
    public string Description { get; set; }
    public Actor Actor { get; set; }

    private Guid _runId = Guid.NewGuid();

    public Guid RunId
    {
        get => _runId;
        set
        {
            if (value != Guid.Empty)
            {
                _runId = value;
            }
        }
    }

    /// <summary>
    /// Event Type Id
    /// it didn't exist before 01/2024 so it may be null
    /// </summary>
    public Guid? EventTypeId { get; init; }

    public virtual Guid FlowId { get; set; }
    public virtual Guid? StatusId { get; set; }
    public virtual Guid TargetId { get; set; }
    public virtual Guid AccountId { get; set; }
    public virtual string ObjectType { get; set; }

    [JsonIgnore] public virtual IEnumerable<KeyValuePair<string, object>> Refs { get; }

    [JsonIgnore] public virtual IEnumerable<KeyValuePair<string, object>> Meta { get; }
    
    public static IEnumerable<Placeholder> GetDefaultEventPlaceHolders(string prefix = default)
    {
        prefix ??= "Event";
        
        yield return new Placeholder
        {
            Name = "{{" + $"{prefix}.{nameof(Description)}" + "}}",
            ObjectType = "system.String",
            Type = Placeholder.PlaceholderType.Value,
            Description = "Event Description",
        };

        yield return new Placeholder
        {
            Name = "{{" + $"{prefix}.{nameof(Action)}" + "}}",
            ObjectType = "system.String",
            Type = Placeholder.PlaceholderType.Value,
            Description = "Action Name",
        };

        yield return new Placeholder
        {
            Name = "{{" + $"{prefix}.{nameof(RunId)}" + "}}",
            ObjectType = "system.String",
            Type = Placeholder.PlaceholderType.Value,
            Description = "Run Id",
        };

        yield return new Placeholder
        {
            Name = "{{" + $"{prefix}.{nameof(EventTypeId)}" + "}}",
            ObjectType = "system.String",
            Type = Placeholder.PlaceholderType.Value,
            Description = "Event Type Id",
        };

        yield return new Placeholder
        {
            Name = "{{" + $"{prefix}.{nameof(FlowId)}" + "}}",
            ObjectType = "system.String",
            Type = Placeholder.PlaceholderType.Value,
            Description = "Flow Id",
        };

        yield return new Placeholder
        {
            Name = "{{" + $"{prefix}.{nameof(ObjectType)}" + "}}",
            ObjectType = "system.String",
            Type = Placeholder.PlaceholderType.Value,
            Description = "Object Type that triggered the event",
        };

        yield return new Placeholder
        {
            Name = "{{" + $"{prefix}.{nameof(StatusId)}" + "}}",
            ObjectType = "system.String",
            Type = Placeholder.PlaceholderType.Value,
            Description = "Object Status when the event was triggered",
        };

        yield return new Placeholder
        {
            Name = "{{" + $"{prefix}.{nameof(TargetId)}" + "}}",
            ObjectType = "system.String",
            Type = Placeholder.PlaceholderType.Value,
            Description = "Id of the object that triggered the event",
        };
    }
}

public static class FlowEventExtensions
{
    public static Guid? GetUserId(this FlowEvent flowEvent)
    {
        return flowEvent.Actor switch
        {
            AbstractAPIActor api => api.UserId,
            _ => null,
        };
    }
}