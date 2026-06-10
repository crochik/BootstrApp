using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Services
{
    public class ExternalProcessExec
    {
        private ILogger _logger;

        public string FilePath { get; set; }
        public string Arguments { get; set; }
        public int TimeOutMs { get; set; } = 30 * 60 * 1000; // 30 minutes
        public string WorkingDirectory { get; set; }
        public string OutputFile { get; set; }
        public string ErrorFile { get; set; }
        public Action<string> OnOutput { get; set; }
        public Action<string> OnError { get; set; }

        public ExternalProcessExec(ILogger logger)
        {
            _logger = logger;
        }

        public bool Run()
        {
            using var process = new Process();

            using var outputWriter = string.IsNullOrEmpty(OutputFile) ? null :
                File.CreateText(Path.Combine(WorkingDirectory, OutputFile));

            using var errorWriter = string.IsNullOrEmpty(ErrorFile) ? null :
                File.CreateText(Path.Combine(WorkingDirectory, ErrorFile));

            process.StartInfo.FileName = FilePath;
            process.StartInfo.Arguments = Arguments;
            process.StartInfo.WorkingDirectory = WorkingDirectory;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            if (outputWriter != null)
            {
                process.OutputDataReceived += (sender, data) => outputWriter.WriteLine(data.Data);
            }

            if (errorWriter != null)
            {
                process.ErrorDataReceived += (sender, data) => errorWriter.WriteLine(data.Data);
            }

            if (OnOutput != null) process.OutputDataReceived += (sender, data) => OnOutput(data.Data);
            if (OnError != null) process.ErrorDataReceived += (sender, data) => OnError(data.Data);

            _logger.LogInformation("Starting Process");
            _logger.LogInformation($"$ {process.StartInfo.FileName} {process.StartInfo.Arguments}");

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            var exited = process.WaitForExit(TimeOutMs);
            if (exited)
            {
                _logger.LogInformation("Process completed successfully");
            }
            else
            {
                _logger.LogError("Process exited with {exitCode}", process.ExitCode);
            }

            return exited;
        }
    }
}