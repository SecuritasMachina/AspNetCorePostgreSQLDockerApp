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
        
        public ScanWorkerCrons()
        {
            jobCronsListTime = DateTime.MinValue;
            _WorkerDTOs = new List<JobDTO>();

        }

        public async Task StartAsync()
        {
            ScanGitHubWorker scanGitHubWorker = new ScanGitHubWorker(RunTimeSettings.GITHUB_PAT_Token, RunTimeSettings.GITHUB_OrgName, RunTimeSettings.customerAgentAuthKey, RunTimeSettings.GoogleStorageBucketName);
            ArchiveWorker archiveWorker = new ArchiveWorker(RunTimeSettings.customerAgentAuthKey, RunTimeSettings.GoogleStorageBucketName, RunTimeSettings.RetentionDays);
            StatusWorker statusWorker = new StatusWorker();
            UpdateOffSiteBytesWorker updateOffSiteBytesWorker = new UpdateOffSiteBytesWorker(RunTimeSettings.customerAgentAuthKey, RunTimeSettings.GoogleStorageBucketName, RunTimeSettings.RetentionDays);
            ScanStageDirWorker scanStageDirWorker = new ScanStageDirWorker();

            GenericMessage genericMessage = new GenericMessage();
            try
            {
                
                if (jobCronsListTime.AddMinutes(6) < DateTime.Now)
                {
                    jobCronsListTime = DateTime.Now;
                    //repoListMsg = HTTPUtils.Instance.getRepoList(RunTimeSettings.customerAgentAuthKey);
                    _WorkerDTOs = JsonConvert.DeserializeObject<List<JobDTO>>(HTTPUtils.Instance.getWorkerList(RunTimeSettings.customerAgentAuthKey));
                    HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Received {_WorkerDTOs.Count} Repositories");
                }
                int qSize = ThreadUtilsV2.Instance.getGitQueueSize();
                if (qSize > 0)
                {
                    HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Waiting for {qSize} threads to finish");
                    return;
                }
                if (_WorkerDTOs.Count > 0)
                {
                    //Loop through and run any crons

                    DateTime nextRunSoonest = DateTime.MaxValue;
                    foreach (JobDTO repo in _WorkerDTOs)
                    {
                        if (!String.IsNullOrEmpty(repo.cronSpec))
                        {
                            CrontabSchedule crontabSchedule = CrontabSchedule.Parse(repo.cronSpec);

                            DateTime dt = crontabSchedule.GetNextOccurrence(DateTime.Now);
                            if (dt < nextRunSoonest)
                                nextRunSoonest = dt;
                        }
                    }
                    bool runJobs = false;
                    if (nextRunSoonest < DateTime.MaxValue)
                    {
                        TimeSpan nextRunJobspan = nextRunSoonest.Subtract(DateTime.Now);
                        TimeZoneInfo easternTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                        var utc = nextRunSoonest.ToUniversalTime();
                        DateTime convertedDate = TimeZoneInfo.ConvertTimeFromUtc(utc, easternTimeZone);

                        int totalMinLeft = ((int)nextRunJobspan.TotalMinutes);
                        if (nextRunJobspan.TotalMinutes < 1)
                            HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Scan for jobs in less than a minute @ {String.Format("{0:g}", convertedDate)}");
                        else
                            HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Scan for jobs in {totalMinLeft + 1} minutes @ {String.Format("{0:g}", convertedDate)}");
                        if (totalMinLeft >= 0 && nextRunJobspan.TotalMinutes < .5)
                            runJobs = true;
                    }

                    if (runJobs)
                    {
                        bool queuedSuccess = false;
                        HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Checking for jobs to run");
                        foreach (JobDTO jobDTO in _WorkerDTOs)
                        {
                            if (!String.IsNullOrEmpty(jobDTO.cronSpec))
                            {
                                CrontabSchedule crontabSchedule = CrontabSchedule.Parse(jobDTO.cronSpec);

                                DateTime now = Utils.getDBDateNow();
                                DateTime nextDate = crontabSchedule.GetNextOccurrence(DateTime.Now);
                                TimeSpan nextRunJobspan = nextDate.Subtract(DateTime.Now);
                                int totalMinLeft = ((int)nextRunJobspan.TotalMinutes);
                                if (nextRunJobspan.TotalMinutes < .5)
                                {
                                    if (jobDTO.workerName.Contains("ScanGitHubWorker"))
                                        scanGitHubWorker.StartAsync();
                                    if (jobDTO.workerName.Contains("ArchiveWorker"))
                                        archiveWorker.StartAsync();
                                    if (jobDTO.workerName.Contains("StatusWorker"))
                                        statusWorker.StartAsync();
                                    if (jobDTO.workerName.Contains("UpdateOffSiteBytesWorker"))
                                        updateOffSiteBytesWorker.StartAsync();
                                    if (jobDTO.workerName.Contains("ScanStageDirWorker"))
                                        scanStageDirWorker.StartAsync();
                                }
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
