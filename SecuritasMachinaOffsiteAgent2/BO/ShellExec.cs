using Common.Statics;
using Common.Utils.Comm;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SecuritasMachinaOffsiteAgent.BO
{
    internal class ShellExec
    {
        private static ShellExec? instance;
        private static ConcurrentDictionary<string, string> execWorker;
        public static ShellExec Instance
        {
            get
            {
                if (instance == null)
                {
                    //Console.WriteLine("instance = new ThreadUtilsV2();");
                    instance = new ShellExec();
                }
                return instance;
            }
        }
        private ShellExec()
        {
            lock(execWorker)
                execWorker = new ConcurrentDictionary<string, string>();
        }
        public int ShellRun(String workingDir, String command)
        {
            //Console.WriteLine(command);
            ProcessStartInfo info;
            //cmdOutput = new StringBuilder();
            // Style = ProgressBarStyle.Marquee;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                info = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    ErrorDialog = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    RedirectStandardError = true,
                    StandardErrorEncoding = Encoding.UTF8,
                    WorkingDirectory = workingDir,
                    FileName = command.Substring(0, command.IndexOf(" ")),
                    Arguments = " " + command.Substring(command.IndexOf(" "))
                };
                //,                
            }
            else
            {
                info = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    LoadUserProfile = true,
                    ErrorDialog = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    RedirectStandardError = true,
                    StandardErrorEncoding = Encoding.UTF8,
                    WorkingDirectory = workingDir,
                    FileName = "cmd.exe",
                    Arguments = "/c " + command
                };

            }

            Process shell = new Process();
            shell.StartInfo = info;
            shell.EnableRaisingEvents = true;
            shell.ErrorDataReceived += new DataReceivedEventHandler((sender, e) =>
            {
                // Prepend line numbers to each line of the output.
                if (!String.IsNullOrEmpty(e.Data))
                {
                    HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"[{numOutputLines}] - {e.Data}");
                    
                }
            });
            shell.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
            {
                // Prepend line numbers to each line of the output.
                if (!String.IsNullOrEmpty(e.Data))
                {
                    HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"[{numOutputLines}] - {e.Data}");

                }
            });

            shell.Start();
            shell.BeginErrorReadLine();
            shell.BeginOutputReadLine();
            shell.WaitForExit();

            return shell.ExitCode;
        }
        static int numOutputLines = 0;
        private  void ShellErrorDataReceived(object sendingProcess,
           DataReceivedEventArgs outLine)
        {
            // Collect the sort command output.
            if (!String.IsNullOrEmpty(outLine.Data))
            {
                numOutputLines++;

                // Add the text to the collected output.
                //cmdOutput.Append(Environment.NewLine +
                //   $"[{numOutputLines}] - {outLine.Data}");
                HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"[{numOutputLines}] - {outLine.Data}");
            }
        }
        private  void ShellOutputDataReceived(object sendingProcess,
           DataReceivedEventArgs outLine)
        {
            // Collect the sort command output.
            if (!String.IsNullOrEmpty(outLine.Data))
            {
                numOutputLines++;

                // Add the text to the collected output.
                //cmdOutput.Append(Environment.NewLine +
                //     $"[{numOutputLines}] - {outLine.Data}");
                HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"[{numOutputLines}] - {outLine.Data}");
            }
        }
    }
}
