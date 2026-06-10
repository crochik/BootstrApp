using System;
using System.IO;
using System.Threading.Tasks;

namespace PI.Shared.FileTransferProviders
{
    public interface IRemoteDirectoryEntry 
    {
        bool IsFile { get; }
        string Path { get; }
        string Name { get; }
        DateTime Date { get; }
    }

    public class RemoteDirectoryEntry : IRemoteDirectoryEntry
    {
        public bool IsFile { get; set; }

        public string Path { get; set; }

        public string Name { get; set; }

        public DateTime Date { get; set; }
    }

    public interface IFileTransferConnection
    {
        ValueTask<IRemoteDirectoryEntry[]> GetListingAsync(string path);
        ValueTask DownloadAsync(FileStream localFileStream, IRemoteDirectoryEntry nextFile);
        ValueTask DisconnectAsync();
    }

    public interface IFileTransferProvider
    {
        bool CanHandle(Uri url);
        ValueTask<IFileTransferConnection> ConnectAsync(Uri uri, string userName, string password, Func<string, Task> log = null);
    }
}