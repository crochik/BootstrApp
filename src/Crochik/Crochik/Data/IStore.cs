using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Crochik.Data
{
    [Obsolete]
    public interface IStore<T, KEY> where T : class
    {
        Task<IEnumerable<T>> GetAsync();
        Task<T> GetAsync(KEY key);
        Task<bool> UpdateAsync(T obj);
        Task<bool> DeleteAsync(KEY key);
        Task<T> CreateAsync(T obj);
    }

    [Obsolete]
    public interface ISqlStore<T, KEY> :
        IStore<T, KEY>
        where T : class
    {
        Task<IEnumerable<T>> Select(string condition = null, object args = null);
    }


}