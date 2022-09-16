using Common.Statics;
using Common.Utils.Comm;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecuritasMachinaOffsiteAgent.BO
{
    internal class ThreadUtils
    {

        //private static int maxThreads = 1;
        static ConcurrentDictionary<string, BackupWorker> dtBackupWorker = new ConcurrentDictionary<string, BackupWorker>();
        static ConcurrentDictionary<string, GitHubArchiveWorker> dtGitHubWorker = new ConcurrentDictionary<string, GitHubArchiveWorker>();

        internal static void deleteFromQueue(string key)
        {
            BackupWorker tmp = null;

            bool v = dtBackupWorker.TryRemove(key, out tmp);
        }
        internal static bool isInQueue(string pBackupWorkerName)
        {
            bool ret = true;
            if (!dtBackupWorker.ContainsKey(pBackupWorkerName))
            {
                ret = false;
            }

            return ret;
        }
        internal static void addToBackupWorkerQueue(BackupWorker backupWorker)
        {
            if (dtBackupWorker.ContainsKey(backupWorker.ToString()))
            {
                Console.WriteLine("Already have " + backupWorker.ToString());
            }
            else
            {
                DateTime start = DateTime.Now;
                TimeSpan timeDiff = DateTime.Now - start;
                while (dtBackupWorker.Count >= RunTimeSettings.MaxThreads && timeDiff.Hours < 1)
                {
                    Thread.Sleep(5 * 1000);
                    timeDiff = DateTime.Now - start;

                }
                string backWorkerName = backupWorker.ToString();
                if (!dtBackupWorker.ContainsKey(backWorkerName))
                {
                    dtBackupWorker.TryAdd(backWorkerName, backupWorker);
                    ThreadPool.QueueUserWorkItem(async x =>
                    {
                        await backupWorker.StartAsync();
                        BackupWorker tmp = null;
                        bool v = dtBackupWorker.TryRemove(backWorkerName, out tmp);
                        Thread.Sleep(100);
                        // countdownEvent.Signal();
                    });
                }

            }
        }
        internal static void addToGitHubWorkerQueue(GitHubArchiveWorker backupWorker)
        {

            DateTime start = DateTime.Now;
            TimeSpan timeDiff = DateTime.Now - start;
            while (dtGitHubWorker.Values.ToList().Count >= RunTimeSettings.MaxThreads && timeDiff.Hours < 1)
            {
                HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Throttling Threads {RunTimeSettings.MaxThreads}");
                foreach (var worker in dtGitHubWorker.Values)
                {
                    HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Active Thread: {worker.ToString()}");
                }
                Thread.Sleep(5 * 1000);
                timeDiff = DateTime.Now - start;
                
            }
            string backWorkerName = backupWorker.ToString();
            if (!dtGitHubWorker.ContainsKey(backWorkerName))
            {
                bool addSuccess=dtGitHubWorker.TryAdd(backWorkerName, backupWorker);
                if (addSuccess)
                {
                    ThreadPool.QueueUserWorkItem(async x =>
                    {
                        await backupWorker.StartAsync();
                        GitHubArchiveWorker tmp = null;
                        bool v = dtGitHubWorker.TryRemove(backWorkerName, out tmp);
                        if (!v)
                        {
                            HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAgentAuthKey, "ERROR", $"Remove failed for Thread : {backWorkerName}");
                        }

                        Thread.Sleep(100);
                    });
                }
                else
                {
                    HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAgentAuthKey, "ERROR", $"Add to dictionary failed for Thread : {backWorkerName}");
                }
            }
            else
            {
                HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Already have {backWorkerName}");
            }


        }

    }
}
