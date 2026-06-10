using System.Threading.Tasks;
using Models;
using PI.Shared.Models;

namespace Services
{
    public interface ITransferService
    {
        Task<Result<string>> LoadAsync(string tmpFolder, SingerJob job);
        Task ProcessFileAsync(SingerJob job, string localPath);
    }
}