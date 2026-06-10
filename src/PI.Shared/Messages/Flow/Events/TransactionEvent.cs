using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using PI.Shared.Constants;
using PI.Shared.Models;
using PI.Shared.Models.Billing;

namespace Messages.Flow;

public abstract class AbstractEntityEvent : FlowEvent
{
    public string Entity { get; set; }
}

public abstract class TransactionEvent : AbstractEntityEvent
{
    public abstract BillTransaction Transaction { get; }

    [JsonIgnore]
    public override Guid TargetId
    {
        get => Transaction.EntityId.Value;
        set { }
    }

    [JsonIgnore]
    public override Guid AccountId
    {
        get => Transaction.AccountId;
        set { }
    }

    [JsonIgnore]
    public override IEnumerable<KeyValuePair<string, object>> Refs => Transaction.GetRefs();

    [JsonIgnore]
    public override IEnumerable<KeyValuePair<string, object>> Meta => GetMeta();
    public override string ObjectType { get; set; }

    protected virtual IEnumerable<KeyValuePair<string, object>> GetMeta()
    {
        if (!string.IsNullOrWhiteSpace(Entity)) yield return new KeyValuePair<string, object>(nameof(Entity), Entity);
        // if (!string.IsNullOrWhiteSpace(Actor)) yield return new KeyValuePair<string, object>(nameof(Actor), Actor);
    }

    protected TransactionEvent()
    {
    }

    protected TransactionEvent(IEntity entity)
    {
        // using accountid and targetid directly from transaction 
        ObjectType = entity.ObjectType;
        Entity = entity.Name;
        StatusId = entity.ObjectStatusId;
        FlowId = entity.FlowId.GetValueOrDefault(FlowIds.Billing);
    }
}

/// <summary>
/// Event triggered when a new charge to an entity has to be created
/// </summary>
public class CreateInvoiceEvent : TransactionEvent
{
    [JsonIgnore]
    public override BillTransaction Transaction => Invoice;

    public Invoice Invoice { get; set; }

    public CreateInvoiceEvent() { }

    public CreateInvoiceEvent(IEntity entity) : base(entity) { }

    protected override IEnumerable<KeyValuePair<string, object>> GetMeta()
    {
        foreach (var m in base.GetMeta()) yield return m;
        foreach (var m in Invoice.GetMeta()) yield return m;
    }
}

/// <summary>
/// Event triggered when a payment status update is received from a third party
/// </summary>
public class PaymentStatusUpdateEvent : TransactionEvent
{
    [JsonIgnore]
    public override BillTransaction Transaction => Payment;

    public Payment Payment { get; set; }
    public PaymentStatus Status { get; set; }

    public PaymentStatusUpdateEvent() { }

    public PaymentStatusUpdateEvent(IEntity entity) : base(entity) { }

    protected override IEnumerable<KeyValuePair<string, object>> GetMeta()
    {
        foreach (var m in base.GetMeta()) yield return m;
        foreach (var m in Payment.GetMeta()) yield return m;
    }
}

public class AdjustmentEvent : TransactionEvent
{
    [JsonIgnore]
    public override BillTransaction Transaction => Adjustment;

    [JsonIgnore]
    public override IEnumerable<KeyValuePair<string, object>> Refs => Adjustment.GetRefs();

    public Adjustment Adjustment { get; set; }

    public AdjustmentEvent() { }

    public AdjustmentEvent(IEntity entity) : base(entity) { }

    protected override IEnumerable<KeyValuePair<string, object>> GetMeta()
    {
        foreach (var m in base.GetMeta()) yield return m;
        foreach (var m in Adjustment.GetMeta()) yield return m;
    }
}

public class DisputeEvent : TransactionEvent
{
    [JsonIgnore]
    public override BillTransaction Transaction => Dispute;

    [JsonIgnore]
    public override IEnumerable<KeyValuePair<string, object>> Refs => Dispute.GetRefs();

    public Dispute Dispute { get; set; }

    public DisputeEvent() { }

    public DisputeEvent(IEntity entity) : base(entity) { }

    protected override IEnumerable<KeyValuePair<string, object>> GetMeta()
    {
        foreach (var m in base.GetMeta()) yield return m;
        foreach (var m in Dispute.GetMeta()) yield return m;
    }
}