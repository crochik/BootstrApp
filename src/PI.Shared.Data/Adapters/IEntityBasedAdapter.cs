// using System.Threading.Tasks;
// using Dapper;

// namespace PI.Shared.Data.Adapters
// {
//     public interface IEntityBasedAdapter<T>
//         where T : class
//     {
//         Task<T> FindAsync(string loginProvider, string providerKey);
//         Task<T> GetAsync(string entityId);
//         Task<T> CreateAsync(T obj, string name);
//     }
// }