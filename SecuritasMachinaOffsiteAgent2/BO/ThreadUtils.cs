using Common.Statics;
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
        static ConcurrentDictionary<string, BackupWorker> dt = new ConcurrentDictionary<string, BackupWorker>();

        internal static void deleteFromQueue(string key)
        {
            BackupWorker tmp = null;
            // BackupWorker tmp = dt.TryGetValue(key);
            bool v = dt.TryRemove(key, out tmp);
        }
        internal static bool isInQueue(string pBackupWorkerName)
        {
            bool ret = true;
            if (!dt.ContainsKey(pBackupWorkerName))
            {
                ret = false;
            }

            return ret;
        }
        internal static void addToQueue(BackupWorker backupWorker)
        {
            if (dt.ContainsKey(backupWorker.ToString()))
            {
                Console.WriteLine("Already have " + backupWorker.ToString());
            }
            else
            {
                while (getActiveThreads() >= RunTimeSettings.MaxThreads)
                {
                    Thread.Sleep(5 * 1000);
                }
                string backWorkerName = backupWorker.ToString();
                if (!dt.ContainsKey(backWorkerName))
                {
                    dt.TryAdd(backWorkerName, backupWorker);
                    ThreadPool.QueueUserWorkItem(async x =>
                    {
                        await backupWorker.StartAsync();
                        deleteFromQueue(backWorkerName);
                        Thread.Sleep(100);
                        // countdownEvent.Signal();
                    });
                }

            }
        }

        internal static long getActiveThreads()
        {
            return dt.Count;
        }
    }
}
