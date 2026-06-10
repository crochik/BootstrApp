using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using PI.Shared.Models;

namespace Messages.Flow
{
    // TODO: use generic instead?
    public class BalanceUpdatedEvent : EntityEvent
    {
        public Guid TransactionId { get; set; }
        public int TransactionNumber { get; set; }
        public string TransactionType { get; set; }
        public decimal PreviousBalance { get; set; }
        public decimal Balance { get; set; }
        public override Guid AccountId { get; set; }

        public override Guid TargetId
        {
            get;
            set;
        }

        [JsonIgnore]
        public override IEnumerable<KeyValuePair<string, object>> Refs
        {
            get
            {
                yield return new KeyValuePair<string, object>("EntityId", TargetId.ToString());
                yield return new KeyValuePair<string, object>(nameof(TransactionId), TransactionId.ToString());
            }
        }

        [JsonIgnore]
        public override IEnumerable<KeyValuePair<string, object>> Meta
        {
            get
            {
                yield return new KeyValuePair<string, object>(nameof(TransactionNumber), TransactionNumber);
                yield return new KeyValuePair<string, object>(nameof(TransactionType), TransactionType);
                yield return new KeyValuePair<string, object>(nameof(PreviousBalance), PreviousBalance);
                yield return new KeyValuePair<string, object>(nameof(Balance), Balance);
                yield return new KeyValuePair<string, object>(nameof(Entity), Entity);
            }
        }

        public BalanceUpdatedEvent() {}

        public BalanceUpdatedEvent(IEntity entity) : base(entity) {}
    }
}