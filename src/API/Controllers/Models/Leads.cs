using System;
using System.Collections.Generic;
using PI.Shared.Form.Models;

namespace Controllers.Models
{
    public class Leads
    {
        public Guid LeadTypeId { get; set; }
        public IEnumerable<Lead> List { get; set; }
        public IEnumerable<FormField> Fields { get; set; }
    }
}