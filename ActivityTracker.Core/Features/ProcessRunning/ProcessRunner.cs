using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace ActivityTracker.Core.Features.ProcessRunning
{
    public interface IProcessRunner
    {
        Task<string> RunBashScriptProcessAsync(string script);
    }

    public class ProcessRunner : IProcessRunner
    {
        public async Task<string> RunBashScriptProcessAsync(string script)
        {
            var escapedArgs = script.Replace("\"", "\\\"");

            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{escapedArgs}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)
                }
            };
            
            process.Start();

            string result = await process.StandardOutput.ReadToEndAsync();
            
            process.WaitForExit();

            return result;
        }
    }
}