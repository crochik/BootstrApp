using System.Linq;
using PI.Shared.Models.Expressions;

namespace PI.Shared.Models;

public class Criteria
{
    public Condition[] Conditions { get; set; }
}

public static class CriteriaExtensions
{
    public static bool IsSearch(this Criteria criteria) => criteria?.Conditions?.Any(x => x.FieldName == Condition.FullTextSearch) ?? false;
}