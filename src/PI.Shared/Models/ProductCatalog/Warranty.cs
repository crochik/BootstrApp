using System;

namespace PI.ProductCatalog.Models
{
    public class Warranty
    {
        public string Description { get; set; }
        public string Duration { get; set; }
        public bool Condition { get; set; }

        public override bool Equals(object obj) 
            => (obj is Warranty other) && 
            string.Equals(Description, other.Description) &&
            string.Equals(Duration, other.Duration) &&
            Condition==other.Condition;

        public override int GetHashCode() => HashCode.Combine(Description, Duration, Condition);
    }
}