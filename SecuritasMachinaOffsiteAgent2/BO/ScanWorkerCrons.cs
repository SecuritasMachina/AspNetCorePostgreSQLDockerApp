using Common.DTO.V2;
using Common.Statics;
using Common.Utils.Comm;
using NCrontab;
using Newtonsoft.Json;
using Octokit;
using SecuritasMachinaOffsiteAgent.DTO.V2;
using System;

namespace SecuritasMachinaOffsiteAgent.BO
{
    public class ScanWorkerCrons
    {
        private static DateTime jobCronsListTime;
        private static List<JobDTO> _WorkerDTOs;
        private ScanGitHubWorker scanGitHubWorker = new ScanGitHubWorker(RunTimeSettings.customerAgentAuthKey, RunTimeSettings.GoogleArchiveBucketName);
        private ArchiveWorker archiveWorker = new ArchiveWorker(RunTimeSettings.customerAgentAuthKey, RunTimeSettings.GoogleArchiveBucketName, RunTimeSettings.RetentionDays);
        private StatusWorker statusWorker = new StatusWorker();
        private UpdateOffSiteBytesWorker updateOffSiteBytesWorker = new UpdateOffSiteBytesWorker(RunTimeSettings.customerAgentAuthKey, RunTimeSettings.GoogleArchiveBucketName, RunTimeSettings.RetentionDays);
        private ScanStageDirWorker scanStageDirWorker = new ScanStageDirWorker();

        public ScanWorkerCrons()
        {
            jobCronsListTime = DateTime.MinValue;
            _WorkerDTOs = new List<JobDTO>();

        }

        public async Task StartAsync()
        {


            GenericMessage genericMessage = new GenericMessage();
            try
            {

                if (jobCronsListTime.AddMinutes(5) < DateTime.Now)
                {
                    jobCronsListTime = DateTime.Now;
                    _WorkerDTOs = HTTPUtils.Instance.getWorkerList(RunTimeSettings.customerAgentAuthKey);
                    HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Received {_WorkerDTOs.Count} workers");
                }

                if (_WorkerDTOs.Count > 0)
                {
                    //Loop through and run any crons

                    DateTime nextRunSoonest = DateTime.MaxValue;
                    foreach (JobDTO repo in _WorkerDTOs.Where(i => !String.IsNullOrEmpty(i.cronSpec)))
                    {
                        CrontabSchedule crontabSchedule = CrontabSchedule.Parse(repo.cronSpec);

                        DateTime dt = crontabSchedule.GetNextOccurrence(DateTime.Now);
                        if (dt < nextRunSoonest)
                            nextRunSoonest = dt;
                    }
                    bool runJobs = false;
                    if (nextRunSoonest < DateTime.MaxValue)
                    {
                        TimeSpan nextRunJobspan = nextRunSoonest.Subtract(DateTime.Now);
                        TimeZoneInfo easternTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                        var utc = nextRunSoonest.ToUniversalTime();
                        DateTime convertedDate = TimeZoneInfo.ConvertTimeFromUtc(utc, easternTimeZone);

                        int totalMinLeft = ((int)nextRunJobspan.TotalMinutes);
                        int totalSecLeft = ((int)nextRunJobspan.TotalSeconds);
                        if (nextRunJobspan.TotalMinutes > 1)
                        {
                            HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Scan for jobs in {totalMinLeft + 1} minutes @ {String.Format("{0:g}", convertedDate)}");
                            return;
                        }
                        if (totalMinLeft >= 0 && nextRunJobspan.TotalSeconds < 10)
                            runJobs = true;
                        else
                            HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Scan for jobs in less than a minute @ {String.Format("{0:g}", convertedDate)}");
                    }

                    if (runJobs)
                    {
                        bool queuedSuccess = false;
                        HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Checking for jobs to run");
                        foreach (JobDTO jobDTO in _WorkerDTOs.Where(i => !String.IsNullOrEmpty(i.cronSpec)))
                        {
                            CrontabSchedule crontabSchedule = CrontabSchedule.Parse(jobDTO.cronSpec);

                            DateTime now = Utils.getDBDateNow();
                            DateTime nextDate = crontabSchedule.GetNextOccurrence(DateTime.Now);
                            TimeSpan nextRunJobspan = nextDate.Subtract(DateTime.Now);
                            int totalMinLeft = ((int)nextRunJobspan.TotalMinutes);
                            if (nextRunJobspan.TotalSeconds < 10)
                            {
                                HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Checking {jobDTO.workerName} @ {jobDTO.cronSpec}");
                                if (String.Equals(jobDTO.workerName, "ScanGitHubWorker", StringComparison.OrdinalIgnoreCase) && !scanGitHubWorker.isBusy())
                                    scanGitHubWorker.StartAsync();
                                if (String.Equals(jobDTO.workerName, "ArchiveWorker", StringComparison.OrdinalIgnoreCase) && !archiveWorker.isBusy())
                                    archiveWorker.StartAsync();
                                if (String.Equals(jobDTO.workerName, "StatusWorker", StringComparison.OrdinalIgnoreCase) && !statusWorker.isBusy())
                                    statusWorker.StartAsync();
                                if (String.Equals(jobDTO.workerName, "UpdateOffSiteBytesWorker", StringComparison.OrdinalIgnoreCase) && !updateOffSiteBytesWorker.isBusy())
                                    updateOffSiteBytesWorker.StartAsync();
                                if (String.Equals(jobDTO.workerName, "ScanStageDirWorker", StringComparison.OrdinalIgnoreCase) && !scanStageDirWorker.isBusy())
                                    scanStageDirWorker.StartAsync();
                            }
                        }
                        //if (queuedSuccess)
                        //repoListRefreshTime = DateTime.Now;
                    }
                }

            }
            catch (Exception ex)
            {
                HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "ERROR", $"ScanGitHubWorker {ex.ToString()}");
            }

        }


    }
}
