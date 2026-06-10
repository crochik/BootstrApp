using System;
using System.Collections.Generic;

namespace PI.Shared.Data.Models
{
    public class LeadAggregation
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public Guid EntityId { get; set; }
        public IEnumerable<Row> Data { get; set; }

        public class Row
        {
            public DateTime CreatedOn { get; set; }
            public string Name { get; set; }
            public int Count { get; set; }
        }
    }
}
