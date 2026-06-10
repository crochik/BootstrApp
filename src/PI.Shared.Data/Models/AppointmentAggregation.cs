using System;
using System.Collections.Generic;

namespace PI.Shared.Data.Models
{
    public class AppointmentAggregation
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public Guid EntityId { get; set; }
        public IEnumerable<Row> Data { get; set; }

        public class Row
        {
            public string Date { get; set; }
            public string Tool { get; set; }
            public int Cancelled { get; set; }
            public int Active { get; set; }
        }
    }
}
