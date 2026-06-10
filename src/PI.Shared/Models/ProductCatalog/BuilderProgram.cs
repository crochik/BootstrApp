using System;

namespace PI.ProductCatalog.Models
{
    public class BuilderProgram
    {
        public string Name { get; set; }

        /// <summary>
        /// What does this mean? should it be included in the tostring?
        /// </summary>
        public bool Condition { get; set; }

        public override bool Equals(object obj)
            => (obj is BuilderProgram other) &&
            string.Equals(Name, other.Name) &&
            Condition == other.Condition;

        public override int GetHashCode() => HashCode.Combine(Name, Condition);

        public override string ToString() => Name;
    }
}