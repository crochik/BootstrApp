using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentFTP;

namespace PI.Shared.FileTransferProviders
{
    public class FTPFileTransferProvider : IFileTransferProvider
    {
        public FTPFileTransferProvider()
        {
        }

        public bool CanHandle(Uri url) => url?.Scheme switch
        {
            "ftp" => true,
            "ftps" => true,
            _ => false,
        };

        public async ValueTask<IFileTransferConnection> ConnectAsync(Uri serverUrl, string userName, string password, Func<string, Task> logAsync = null)
        {
            var connection = new FTPFileTransferConnection(serverUrl, userName, password, logAsync);
            await connection.ConnectAsync();

            return connection;
        }
    }

    public class FTPFileTransferConnection : IFileTransferConnection
    {
        private const int timeout = 5000;
        private readonly FtpClient _client;

        public FTPFileTransferConnection(Uri serverUrl, string userName, string password, Func<string, Task> logAsync)
        {
            _client = new FtpClient(serverUrl)
            {
                ConnectTimeout = timeout,
                ReadTimeout = timeout,
                DataConnectionConnectTimeout = timeout,
                DataConnectionReadTimeout = timeout
            };

            _client.OnLogEvent += (level, message) =>
            {
                switch (level)
                {
                    case FtpTraceLevel.Warn:
                        logAsync($"FTP Warning: {message}").Wait();
                        break;

                    case FtpTraceLevel.Error:
                        logAsync($"FTP Error: {message}").Wait();
                        break;
                }
            };

            _client.Credentials = new System.Net.NetworkCredential(userName, password);
        }

        public async ValueTask ConnectAsync() => await _client.ConnectAsync();

        public async ValueTask DisconnectAsync() => await _client.DisconnectAsync();

        public async ValueTask<IRemoteDirectoryEntry[]> GetListingAsync(string path)
        {
            var files = await _client.GetListingAsync(path);

            return files.Select(x => new RemoteDirectoryEntry
            {
                Name = x.Name,
                Path = x.FullName,
                IsFile = x.Type == FtpFileSystemObjectType.File,
                Date = x.Modified,
            }).ToArray();
        }

        public async ValueTask DownloadAsync(FileStream localFileStream, IRemoteDirectoryEntry nextFile)
        {
            var result = await _client.DownloadAsync(localFileStream, nextFile.Path);
            if (!result)
            {
                throw new Exception($"Error downloading file: {nextFile.Path}");
            }
        }
    }
}