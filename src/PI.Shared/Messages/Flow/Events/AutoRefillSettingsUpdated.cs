using System.Collections.Generic;
using PI.Shared.Models;
using PI.Shared.Models.Billing;
using Crochik.Extensions;

namespace Messages.Flow
{
    public class AutoRefillSettingsUpdated : GenericFlowEvent
    {
        public AutoRefillSettingsUpdated()
        {
        }

        public AutoRefillSettingsUpdated(IEntity entity, BillEntity billEntity, Actor actor) :
            base(entity)
        {
            // FlowId = FlowIds.Billing;
            // StatusId = null;
            // TargetId = billEntity.Id;
            // AccountId = billEntity.AccountId;
            Actor = actor;

            Description = billEntity.AutoRefill ?
                $"Auto Refill settings changed for {billEntity.Name}: Enabled ({billEntity.MinBalance.FormatCurrency()} => {billEntity.MaxBalance.FormatCurrency()})" :
                $"Auto Refill settings changed for {billEntity.Name}: Disabled";

            RefValues = new List<KeyValuePair<string, object>> {
                new KeyValuePair<string,object>("EntityId", billEntity.Id),
                new KeyValuePair<string,object>("EntityId", entity.Context.UserId.GetOptionalValue()),
                new KeyValuePair<string,object>("EntityId", entity.Context.OrganizationId.GetOptionalValue()),
            };

            MetaValues = new Dictionary<string, object> {
                {nameof(BillEntity.AutoRefill), billEntity.AutoRefill},
                {nameof(BillEntity.MinBalance), billEntity.MinBalance.HasValue ? (object)billEntity.MinBalance.Value : null},
                {nameof(BillEntity.MaxBalance), billEntity.MaxBalance.HasValue ? (object)billEntity.MaxBalance.Value : null},
            };
        }
    }
}