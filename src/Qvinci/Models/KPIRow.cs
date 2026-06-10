using System;
using System.Collections.Generic;

namespace Qvinci.Models
{
    public class KPIRow
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
        public Guid? ParentId { get; set; }
        public string Path { get; set; }

        public Dictionary<string, KPIValue> Values { get; set; }
    }
}
