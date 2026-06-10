using System.Threading.Tasks;
using PI.Shared.Data.Adapters;
using PI.Shared.Models;

namespace PI.Shared.Data.Mongo.Adapters
{
    public class PostalCodeAdapter : IPostalCodeAdapter
    {
        public Task<PostalCodeLookup> FindAsync(string code)
            => Task.FromResult<PostalCodeLookup>(null);
    }
}