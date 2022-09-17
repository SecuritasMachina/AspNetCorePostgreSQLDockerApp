using Common.Statics;
using Common.Utils.Comm;
using Octokit;
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
            int tCount = 0;
            DateTime start = DateTime.Now;
            TimeSpan timeDiff = DateTime.Now - start;
            foreach (var worker in dtBackupWorker.Values)
            {
                tCount++;
            }
            while (tCount >= RunTimeSettings.MaxThreads && timeDiff.Hours < 1)
            {
                HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Throttling BackupWorkerQueue Threads {RunTimeSettings.MaxThreads}");
                tCount = 0;
                string tmp = "";
                foreach (var worker in dtBackupWorker.Values)
                {
                    tCount++;
                    tmp += $" | Active Thread {tCount}: {worker.ToString()}";
                }
                if (!String.IsNullOrEmpty(tmp))
                    HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", tmp);
                Thread.Sleep(5 * 1000);
                tmp = "";
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
        //public static readonly object dtGitHubWorker = new object();
        internal static void addToGitHubWorkerQueue(GitHubArchiveWorker backupWorker)
        {

            DateTime start = DateTime.Now;
            TimeSpan timeDiff = DateTime.Now - start;
            int tCount = 0;
            string tmp = "";
            lock (dtGitHubWorker)
            {
                foreach (var worker in dtGitHubWorker.Values)
                {
                    tCount++;
                    tmp += $" | Active Thread {tCount}: {worker.ToString()}";
                }
            }
            while (tCount >= RunTimeSettings.MaxThreads && timeDiff.TotalHours < 1)
            {
                HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Throttling GitHubWorkerQueue {tCount} of Max Threads {RunTimeSettings.MaxThreads} Active Threads: {tmp}");
                Thread.Sleep(5 * 1000);
                tCount = 0;
                tmp = "";
                lock (dtGitHubWorker)
                {
                    foreach (var worker in dtGitHubWorker.Values)
                    {
                        tCount++;
                        tmp += $" | Active Thread {tCount}: {worker.ToString()}";
                    }
                }
                if (tCount > 0)
                    HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Throttling GitHubWorkerQueue {tCount} of Max Threads {RunTimeSettings.MaxThreads} Active Threads: {tmp}");




                timeDiff = DateTime.Now - start;

            }
            string backWorkerName = backupWorker.ToString();
            bool tmpBool = false;
            lock (dtGitHubWorker)
                tmpBool = dtGitHubWorker.ContainsKey(backWorkerName);

            if (!tmpBool)
            {
                bool addSuccess = false;
                lock (dtGitHubWorker)
                    addSuccess = dtGitHubWorker.TryAdd(backWorkerName, backupWorker);
                if (addSuccess)
                {
                    //HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAgentAuthKey, "INFO", $"Added Thread : {backWorkerName}");
                    //ThreadPool.SetMaxThreads(RunTimeSettings.MaxThreads, RunTimeSettings.MaxThreads);


                    ThreadPool.QueueUserWorkItem(async x =>
                    {
                        await backupWorker.StartAsync();
                        GitHubArchiveWorker tmp = null;
                        bool v = false;
                        lock (dtGitHubWorker)
                            v = dtGitHubWorker.TryRemove(backWorkerName, out tmp);
                        if (!v)
                        {
                            HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "ERROR", $"Remove failed for Thread : {backWorkerName}");
                        }


                        Thread.Sleep(100);
                    });
                }
                else
                {
                    HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "ERROR", $"Add to dictionary failed for Thread : {backWorkerName}");
                }
            }
            else
            {
                HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Already have {backWorkerName}");
            }



        }

    }
}
