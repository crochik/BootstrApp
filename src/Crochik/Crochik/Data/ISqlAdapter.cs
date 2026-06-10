using System.Collections.Generic;
using System.Threading.Tasks;

namespace Crochik.Data
{
    public interface ISqlAdapter<T> :
        IAdapter<T>
        where T : class
    {
        Task<IEnumerable<T>> SelectAsync(string condition = null, object args = null, string orderBy = null, int? maxRecords = null);
    }
}