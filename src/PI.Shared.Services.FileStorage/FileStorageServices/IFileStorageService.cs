using System.IO;
using System.Threading.Tasks;

namespace PI.Shared.Services
{
    public interface IFileStorageService
    {
        string Provider { get; }
        Task DownloadAsync(string bucket, string path, string outputPath);
        Task<string> UploadAsync(string inputPath, string contentType, string bucket, string path);
        Task<string> UploadAsync(Stream stream, string contentType, string bucket, string path, bool? allowAnonymousRead = null);
    }
}