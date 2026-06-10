using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Crochik.FTP
{
    public class FtpSettings
    {
        public string Host { get; set; }
        public string User { get; set; }
        public string Password { get; set; }

        public bool UsePassive { get; set; } = true;
        public bool KeepAlive { get; set; } = true;

        private NetworkCredential _credentials = null;
        public NetworkCredential Credentials => _credentials ?? new NetworkCredential(User, Password);
    }

    public class FtpClient
    {
        public FtpSettings Settings { get; }

        public FtpClient(FtpSettings settings)
        {
            Settings = settings;
        }

        public string BuildUrl(params string[] path)
            => $"ftp://{Settings.Host}/{string.Join('/', path)}";

        public string BuildUrl(IEnumerable<string> path)
            => $"ftp://{Settings.Host}/{string.Join('/', path)}";

        public async Task<IEnumerable<string>> ListAsync(params string[] path)
        {
            var text = await GetTextAsync(WebRequestMethods.Ftp.ListDirectory, path);
            return text.Split("\r\n", System.StringSplitOptions.RemoveEmptyEntries);
        }

        public async Task DownloadAsync(string localPath, params string[] path)
        {
            System.Console.WriteLine(localPath);

            using var stream = await RequestAsync(WebRequestMethods.Ftp.DownloadFile, path);
            if (stream == Stream.Null) throw new System.Exception("Received empty response");
            FileStream localFileStream = null;

            try
            {
                localFileStream = new FileStream(localPath, FileMode.Create);
                await stream.CopyToAsync(localFileStream);
            }
            finally
            {
                if (localFileStream != null) localFileStream.Close();
                stream.Close();
            }

            System.Console.WriteLine("done");
        }

        private FtpWebRequest BuildRequest(string method, params string[] path)
        {
            var request = (FtpWebRequest)WebRequest.Create(BuildUrl(path));
            request.Method = method;
            request.UsePassive = Settings.UsePassive;
            request.KeepAlive = Settings.KeepAlive;
            request.Credentials = Settings.Credentials;

            return request;
        }

        public async Task<FtpWebResponse> GetResponseAsync(string method, params string[] path)
            => (FtpWebResponse)await BuildRequest(method, path).GetResponseAsync();

        public async Task<Stream> RequestAsync(string method, params string[] path)
            => (await GetResponseAsync(method, path)).GetResponseStream();

        public async Task<string> GetTextAsync(string method, params string[] path)
        {
            using var stream = await RequestAsync(method, path);
            if (stream == Stream.Null) return null;

            StreamReader reader = null;
            try
            {
                reader = new StreamReader(stream);
                return await reader.ReadToEndAsync();
            }
            finally
            {
                if (reader != null) reader.Close();
                stream.Close();
            }
        }
    }

    /*
            var user = args[0];
            var pwd = args[1];
            var credentials = new NetworkCredential(user, pwd);

            // Get the object used to communicate with the server.
            var request = (FtpWebRequest)WebRequest.Create("ftp://b2b.nourison.net/OUTBOX");
            request.Method = WebRequestMethods.Ftp.ListDirectory;
            // request.UsePassive = true;
            request.KeepAlive = true;
            request.Credentials = credentials;

            var response = (FtpWebResponse)request.GetResponse();
            using var responseStream = response.GetResponseStream();
            using var reader = new StreamReader(responseStream);
            var list = reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            reader.Close();
            response.Close();

            foreach (var filename in list)
            {
                Console.WriteLine($"Donwload {filename}");
                request = (FtpWebRequest)WebRequest.Create($"ftp://b2b.nourison.net/OUTBOX/{filename}");
                request.Method = WebRequestMethods.Ftp.DownloadFile;
                request.KeepAlive = true;
                request.Credentials = credentials;
                response = (FtpWebResponse)request.GetResponse();
                var inStream = response.GetResponseStream();
                var localFileStream = new FileStream(filename, FileMode.Create);
                byte[] byteBuffer = new byte[1024];
                int bytesRead = inStream.Read(byteBuffer);
                while (bytesRead > 0)
                {
                    localFileStream.Write(byteBuffer, 0, bytesRead);
                    bytesRead = inStream.Read(byteBuffer);
                }
                localFileStream.Close();
                inStream.Close();

            }
    */
}