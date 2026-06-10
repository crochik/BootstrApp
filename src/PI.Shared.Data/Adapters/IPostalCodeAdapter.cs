using System.Threading.Tasks;
using PI.Shared.Models;

namespace PI.Shared.Data.Adapters
{
    public interface IPostalCodeAdapter
    {
        Task<PostalCodeLookup> FindAsync(string code);
    }
}