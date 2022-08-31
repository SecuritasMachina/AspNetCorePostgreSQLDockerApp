﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecuritasMachinaOffsiteAgent.BO
{
    internal class ThreadUtils
    {

        
        static Dictionary<string, BackupWorker> dt = new Dictionary<string, BackupWorker>();
        internal static void deleteFromQueue(string key)
        {
            dt.Remove(key);
        }
        internal static void addToQueue(BackupWorker backupWorker)
        {
            if (dt.ContainsKey(backupWorker.ToString()))
            {
                Console.WriteLine("Already have " + backupWorker.ToString());
            }
            else
            {
                dt.Add(backupWorker.ToString(), backupWorker);
                ThreadPool.QueueUserWorkItem(async x =>
                {
                    await backupWorker.StartAsync();
                    deleteFromQueue(backupWorker.ToString());

                // countdownEvent.Signal();
                });
            }
        }

        internal static long getActiveThreads()
        {
            return dt.Count;
        }
    }
}
