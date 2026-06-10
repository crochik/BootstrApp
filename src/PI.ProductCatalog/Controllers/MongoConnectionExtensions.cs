using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using PI.ProductCatalog.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;

namespace Controllers
{
    public static class MongoConnectionExtensions
    {
        public static async Task<IEnumerable<ReferenceValue>> CatalogFeedLookupAsync(this MongoConnection connection, IEntityContext context, DataViewRequest request)
        {
            var query = connection.Filter<CatalogFeed>()
                .Eq(x => x.AccountId, context.AccountId)
                .Eq(x => x.EntityId, context.GetOwnerEntityId());

            if (request.Criteria.TryGetEqCondition(Condition.LookupId, out var condition) && condition.TryGetUidValue(out var id))
            {
                query.Eq(x => x.Id, id);
            }

            var records = await query.FindAsync();
            return records
                .Select(x => new ReferenceValue
                {
                    Id = x.Id.ToString(),
                    Value = x.Name
                })
                .OrderBy(x => x.Value);
        }
    }
}
