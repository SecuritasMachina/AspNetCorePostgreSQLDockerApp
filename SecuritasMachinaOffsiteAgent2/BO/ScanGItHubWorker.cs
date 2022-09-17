using Common.DTO.V2;
using Common.Statics;
using Common.Utils.Comm;
using NCrontab;
using Newtonsoft.Json;
using Octokit;
using System;

namespace SecuritasMachinaOffsiteAgent.BO
{
    public class ScanGitHubWorker
    {
        private string customerGuid;
        private string googleBucketName;

        private string GITHUB_PAT_Token;
        private string GITHUB_OrgName;
        private static DateTime cacheRefreshTime;
        private static DateTime repoListRefreshTime;
        private static string repoListMsg;
        private static List<RepoDTO> _RepoDTOs;

        public ScanGitHubWorker(string GITHUB_PAT_Token, string GITHUB_OrgName, string customerGuid, string googleBucketName)
        {
            this.GITHUB_PAT_Token = GITHUB_PAT_Token;
            this.GITHUB_OrgName = GITHUB_OrgName;
            this.customerGuid = customerGuid;
            this.googleBucketName = googleBucketName;
            cacheRefreshTime = DateTime.MinValue;
            repoListRefreshTime = DateTime.MinValue;
            _RepoDTOs = new List<RepoDTO>();
        }

        public async Task StartAsync()
        {
            //List<RepoDTO> repoDTOs = new List<RepoDTO>();
            GenericMessage genericMessage = new GenericMessage();
            try
            {
                if (cacheRefreshTime.AddMinutes(5) < DateTime.Now)
                {
                    cacheRefreshTime = DateTime.Now;
                    var github = new GitHubClient(new ProductHeaderValue($"SecuritasMachina_Agent_{VersionUtil.getAppName()}")); // TODO: other setup
                    var tokenAuth = new Credentials(GITHUB_PAT_Token); // NOTE: not real token
                    github.Credentials = tokenAuth;
                    IReadOnlyList<Repository> contents = await github
                                                    .Repository.GetAllForUser(GITHUB_OrgName);
                    List<RepoDTO> repoDTOs = new List<RepoDTO>();
                    foreach (Repository repo in contents)
                    {
                        RepoDTO repoDTO = new RepoDTO();
                        repoDTO.Description = repo.Description;
                        repoDTO.FullName = repo.FullName;
                        repoDTO.Size = repo.Size;
                        repoDTO.UpdatedAt = repo.UpdatedAt.ToUnixTimeMilliseconds();
                        repoDTO.Url = repo.CloneUrl;
                        repoDTOs.Add(repoDTO);
                    }

                    GenericMessage.msgTypes msgType = GenericMessage.msgTypes.REPOLIST;
                    genericMessage.msgType = msgType.ToString();
                    genericMessage.msg = JsonConvert.SerializeObject(repoDTOs);
                    genericMessage.guid = RunTimeSettings.customerAgentAuthKey;
                    HTTPUtils.Instance.putCache(RunTimeSettings.customerAgentAuthKey, "REPOLIST", JsonConvert.SerializeObject(genericMessage));
                    HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Found {repoDTOs.Count} Repositories");


                }
                if (repoListRefreshTime.AddMinutes(6) < DateTime.Now)
                {
                    repoListRefreshTime = DateTime.Now;
                    repoListMsg = HTTPUtils.Instance.getRepoList(RunTimeSettings.customerAgentAuthKey);
                    _RepoDTOs = JsonConvert.DeserializeObject<List<RepoDTO>>(repoListMsg);
                    HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Received {_RepoDTOs.Count} Repositories");
                }
                int qSize = ThreadUtilsV2.Instance.getGitQueueSize();
                if (qSize > 0)
                {
                    HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Waiting for {qSize} threads to finish");
                    return;
                }
                if (_RepoDTOs.Count > 0)
                {
                    //Loop through and run any crons

                    DateTime nextRunSoonest = DateTime.MaxValue;
                    foreach (RepoDTO repo in _RepoDTOs)
                    {
                        if (!String.IsNullOrEmpty(repo.backupFrequency))
                        {
                            CrontabSchedule crontabSchedule = CrontabSchedule.Parse(repo.backupFrequency);

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
                            HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Scan for jobs in {totalMinLeft + 1} minute(s) @ {String.Format("{0:g}", convertedDate)}");
                        if (totalMinLeft >= 0 && nextRunJobspan.TotalMinutes < .5)
                            runJobs = true;
                    }

                    if (runJobs)
                    {
                        bool queuedSuccess = false;
                        HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Checking for jobs to run");
                        foreach (RepoDTO repo in _RepoDTOs)
                        {
                            if (!String.IsNullOrEmpty(repo.backupFrequency))
                            {
                                CrontabSchedule crontabSchedule = CrontabSchedule.Parse(repo.backupFrequency);

                                DateTime now = Utils.getDBDateNow();
                                DateTime dt = crontabSchedule.GetNextOccurrence(DateTime.Now);
                                TimeSpan nextRunJobspan = nextRunSoonest.Subtract(DateTime.Now);
                                int totalMinLeft = ((int)nextRunJobspan.TotalMinutes);
                                TimeSpan lastBackupSpan = now.Subtract(repo.lastBackupDate);
                                TimeSpan lastSyncSpan = now.Subtract(repo.lastSyncDate);

                                if (lastSyncSpan.TotalMinutes < 15 || lastBackupSpan.TotalMinutes < 15)
                                {
                                    // HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Skip {repo.FullName} lastSyncSpan.TotalMinutes:{lastSyncSpan.TotalMinutes} lastBackupSpan.TotalMinutes:{lastBackupSpan.TotalMinutes}");
                                    continue;
                                }

                                if (totalMinLeft >= 0 && nextRunJobspan.TotalMinutes < .5)
                                {

                                    //HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"loading {repo.FullName} lastSyncSpan.TotalMinutes:{lastSyncSpan.TotalMinutes} lastBackupSpan.TotalMinutes:{lastBackupSpan.TotalMinutes}");
                                    
                                    if (!ThreadUtilsV2.Instance.isGitWorkerInQueue(repo.FullName))
                                    {
                                        GitHubArchiveWorker gitHubArchiveWorker = new GitHubArchiveWorker(this.GITHUB_PAT_Token, this.GITHUB_OrgName, this.customerGuid, this.googleBucketName, repo);
                                        repoListRefreshTime = DateTime.Now;
                                        repo.lastSyncDate = DateTime.Now;

                                        bool success = ThreadUtilsV2.Instance.addToGitHubWorkerQueue(gitHubArchiveWorker);

                                        if (!queuedSuccess)
                                            queuedSuccess = success;
                                        //Thread.Sleep(50);
                                    }
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
