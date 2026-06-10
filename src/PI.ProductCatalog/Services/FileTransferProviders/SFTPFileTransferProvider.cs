using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace PI.Shared.FileTransferProviders
{
    public class SFTPFileTransferProvider : IFileTransferProvider
    {
        public SFTPFileTransferProvider()
        {
        }

        public bool CanHandle(Uri url) => url?.Scheme switch
        {
            "sftp" => true,
            _ => false,
        };

        public async ValueTask<IFileTransferConnection> ConnectAsync(Uri uri, string userName, string password, Func<string, Task> log = null)
        {
            var connection = new SFTPFileTransferConnection(uri, userName, password, log);
            await connection.ConnectAsync();

            return connection;
        }
    }

    public class SFTPFileTransferConnection : IFileTransferConnection
    {
        private readonly Func<string, Task> _log;
        private readonly SftpClient _client;

        public SFTPFileTransferConnection(Uri uri, string userName, string password, Func<string, Task> log)
        {
            _log = log;
            _client = new SftpClient(uri.Host, uri.Port, userName, password);
        }

        public ValueTask DisconnectAsync()
        {
            _client.Disconnect();
            return new ValueTask();
        }

        public async ValueTask DownloadAsync(FileStream localFileStream, IRemoteDirectoryEntry nextFile)
        {
            await _client.DownloadAsync(nextFile.Path, localFileStream);
        }

        public async ValueTask<IRemoteDirectoryEntry[]> GetListingAsync(string path)
        {
            if (!path.StartsWith("\\")) path = "\\" + path;
            var files = await _client.GetListingAsync(path);

            return files.Select(x => new RemoteDirectoryEntry
            {
                Name = x.Name,
                Path = x.FullName,
                IsFile = x.IsRegularFile,
                Date = x.LastWriteTimeUtc,
            }).ToArray();
        }

        internal async ValueTask ConnectAsync()
        {
            await _client.ConnectAsync(CancellationToken.None);
        }
    }

    public static class SftpClientExtensions
    {
        public static Task<IEnumerable<ISftpFile>> GetListingAsync(this SftpClient client, string path)
        {
            return client.ListDirectoryAsync(path, CancellationToken.None);
        }

        public static Task DownloadAsync(this SftpClient client, string path, Stream output)
        {
            var tcs = new TaskCompletionSource<bool>();

            client.BeginDownloadFile(path, output, iar =>
            {
                client.EndDownloadFile(iar);
                tcs.TrySetResult(true);
            }, null);

            return tcs.Task;
        }
    }
}