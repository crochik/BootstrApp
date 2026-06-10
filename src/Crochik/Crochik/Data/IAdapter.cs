using System.Collections.Generic;
using System.Threading.Tasks;

namespace Crochik.Data
{
    public interface IAdapter<T> where T : class
    {
        Task<IEnumerable<T>> GetAsync();
        Task<bool> UpdateAsync(T obj);
        Task<bool> DeleteAsync(T key);
        Task<T> CreateAsync(T obj);
        Task<T> GetAsync(object criteria);
    }
}