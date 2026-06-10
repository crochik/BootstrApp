using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using PI.Shared.Models;

namespace Messages.Flow
{
    public class Context
    {
        public Guid? IntegrationId { get; set; }
        public Guid? EntityId { get; set; }
        public IEnumerable<TrunkIdentity> ExternalIdentities { get; set; }
    }

    public class LeadInfo
    {
        public PI.Shared.Models.Lead Lead { get; set; }
        public IEnumerable<IntegrationMapping> IntegrationMapping { get; set; }
        public string SchedulerUrl { get; set; }
    }

    public class AppointmentInfo
    {
        public Appointment Appointment { get; set; }
        public IEnumerable<IntegrationMapping> IntegrationMapping { get; set; }
        public IEnumerable<TrunkIdentity> ExternalIdentities { get; set; }
    }

    public class LeadEvent : FlowEvent
    {
        public LeadInfo Lead { get; set; }
        public Context Context { get; set; }

        [JsonIgnore]
        public override Guid TargetId
        {
            get => Lead.Lead.Id;
            set { }
        }

        [JsonIgnore]
        public override Guid FlowId
        {

            get => Lead.Lead.FlowId.Value;
            set { }
        }

        [JsonIgnore]
        public override Guid? StatusId
        {
            get => Lead.Lead.ObjectStatusId;
            set { }
        }

        [JsonIgnore]
        public override Guid AccountId
        {
            get => Lead.Lead.AccountId;
            set { }
        }

        [JsonIgnore]
        public override string ObjectType
        {
            get => SystemObjectType.Lead;
            set { }
        }

        [JsonIgnore]
        public override IEnumerable<KeyValuePair<string, object>> Refs => GetRefs();

        [JsonIgnore]
        public override IEnumerable<KeyValuePair<string, object>> Meta => GetMeta();

        public LeadEvent() { }

        protected virtual IEnumerable<KeyValuePair<string, object>> GetRefs()
        {
            yield return new KeyValuePair<string, object>("LeadId", Lead.Lead.Id.ToString());
            yield return new KeyValuePair<string, object>("EntityId", Lead.Lead.EntityId.ToString());
            if (Lead.Lead.AssignedEntityId.HasValue) yield return new KeyValuePair<string, object>("EntityId", Lead.Lead.AssignedEntityId.Value.ToString());

            if (Context != null)
            {
                if (Context.IntegrationId.HasValue) yield return new KeyValuePair<string, object>("IntegrationId", Context.IntegrationId.Value.ToString());
                if (Context.EntityId.HasValue) yield return new KeyValuePair<string, object>("EntityId", Context.EntityId.Value.ToString());
            }
        }

        protected virtual IEnumerable<KeyValuePair<string, object>> GetMeta()
        {
            yield return new KeyValuePair<string, object>("Lead", Lead.Lead.Name);
            // ...
        }
    }

    public class LeadWithAppointmentEvent : LeadEvent
    {
        public AppointmentInfo Appointment { get; set; }

        public LeadWithAppointmentEvent() { }

        protected override IEnumerable<KeyValuePair<string, object>> GetRefs()
        {
            foreach (var r in base.GetRefs()) yield return r;
            yield return new KeyValuePair<string, object>("AppointmentId", Appointment.Appointment.Id.ToString());
        }
    }
}