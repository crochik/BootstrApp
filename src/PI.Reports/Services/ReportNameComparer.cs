using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using PI.Shared.Models;

namespace Reports.Services
{
    public class ReportNameComparer : IEqualityComparer<AppReport>
    {
        public static ReportNameComparer Default = new();
        public bool Equals([AllowNull] AppReport x, [AllowNull] AppReport y) => string.Equals(x.Description ?? x.Name, y.Description ?? y.Name);
        public int GetHashCode([DisallowNull] AppReport obj) => (obj.Description ?? obj.Name).GetHashCode();
    }
}