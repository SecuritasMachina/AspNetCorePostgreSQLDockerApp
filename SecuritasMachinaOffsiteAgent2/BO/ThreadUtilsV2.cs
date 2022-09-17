﻿using Common.Statics;
using Common.Utils.Comm;
using Octokit;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SecuritasMachinaOffsiteAgent.BO
{
    internal class ThreadUtilsV2
    {


        private static ConcurrentDictionary<string, BackupWorker> dtBackupWorker;
        private static ConcurrentDictionary<string, GitHubArchiveWorker> dtGitHubWorker;
        private static ThreadUtilsV2? instance;

        public static ThreadUtilsV2 Instance
        {
            get
            {
                if (instance == null)
                {
                    //Console.WriteLine("instance = new ThreadUtilsV2();");
                    instance = new ThreadUtilsV2();
                }
                return instance;
            }
        }
        private ThreadUtilsV2()
        {

            dtBackupWorker = new ConcurrentDictionary<string, BackupWorker>();
            dtGitHubWorker = new ConcurrentDictionary<string, GitHubArchiveWorker>();
        }
        internal void deleteFromQueue(string key)
        {
            BackupWorker tmp = null;

            bool v = dtBackupWorker.TryRemove(key, out tmp);
        }
        internal bool isInQueue(string pBackupWorkerName)
        {
            bool ret = true;
            if (!dtBackupWorker.ContainsKey(pBackupWorkerName))
            {
                ret = false;
            }

            return ret;
        }
        internal bool isGitWorkerInQueue(string pBackupWorkerName)
        {
            bool ret = false;
            if (dtGitHubWorker.ContainsKey(pBackupWorkerName))
            {
                ret = true;
            }

            return ret;
        }
        internal bool deleteGitWorkerFromQueue(string key)
        {
            bool ret = false;
            GitHubArchiveWorker tmp = null;

            ret = dtGitHubWorker.TryRemove(key, out tmp);
            return ret;
        }
        internal void addToBackupWorkerQueue(BackupWorker backupWorker)
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

                tCount = 0;
                string tmp = "";
                foreach (var worker in dtBackupWorker.Values)
                {
                    tCount++;
                    tmp += $" | Active Thread {tCount}: {worker.ToString()}";
                }

                if (!String.IsNullOrEmpty(tmp))
                    HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Throttling BackupWorkerQueue Threads {RunTimeSettings.MaxThreads} Threads:{tmp}");
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
                    Thread.Sleep(25);
                    // countdownEvent.Signal();
                });
            }


        }

        internal bool addToGitHubWorkerQueue(GitHubArchiveWorker backupWorker)
        {

            DateTime start = DateTime.Now;
            bool QueuedSuccess = false;
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
                start = DateTime.Now;
                HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Throttling {backupWorker.ToString()} {tCount} of Max Threads {RunTimeSettings.MaxThreads} Active Threads: {tmp}");
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
            start = DateTime.Now;
            string backWorkerName = backupWorker.ToString();
            bool tmpBool = false;
            //lock (dtGitHubWorker)
            tmpBool = isGitWorkerInQueue(backWorkerName);

            if (!tmpBool)
            {
                bool addSuccess = false;
                lock (dtGitHubWorker)
                    addSuccess = dtGitHubWorker.TryAdd(backWorkerName, backupWorker);
                if (addSuccess)
                {
                    ThreadPool.QueueUserWorkItem(async x =>
                    {
                        QueuedSuccess = await backupWorker.StartAsync();

                        bool v = deleteGitWorkerFromQueue(backWorkerName);
                        if (!v)
                        {
                            HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "ERROR", $"Remove failed for Thread : {backWorkerName}");
                        }



                        Thread.Sleep(25);
                    });
                    return QueuedSuccess;
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
            return QueuedSuccess;


        }

    }
}
