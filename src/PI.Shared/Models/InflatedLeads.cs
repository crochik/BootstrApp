using System;
using System.Collections.Generic;

namespace PI.Shared.Models
{
    public class InflatedLeads
    {
        public Guid LeadTypeId { get; set; }
        public List<Lead> List { get; set; }
        public IEnumerable<FieldMapperConfig> Fields { get; set; }
    }
}