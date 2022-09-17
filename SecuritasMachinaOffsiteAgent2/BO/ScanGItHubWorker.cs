using Common.DTO.V2;
using Common.Statics;
using Common.Utils.Comm;
using NCrontab;
using Newtonsoft.Json;
using Octokit;

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

        public ScanGitHubWorker(string GITHUB_PAT_Token, string GITHUB_OrgName, string customerGuid, string googleBucketName)
        {
            this.GITHUB_PAT_Token = GITHUB_PAT_Token;
            this.GITHUB_OrgName = GITHUB_OrgName;
            this.customerGuid = customerGuid;
            this.googleBucketName = googleBucketName;
            cacheRefreshTime = DateTime.MinValue;
            repoListRefreshTime = DateTime.MinValue;
        }

        public async Task StartAsync()
        {
            List<RepoDTO> repoDTOs = new List<RepoDTO>();
            GenericMessage genericMessage = new GenericMessage();
            try
            {
                if (cacheRefreshTime.AddMinutes(2) < DateTime.Now)
                {
                    cacheRefreshTime = DateTime.Now;
                    var github = new GitHubClient(new ProductHeaderValue($"SecuritasMachina_Agent_{VersionUtil.getAppName()}")); // TODO: other setup
                    var tokenAuth = new Credentials(GITHUB_PAT_Token); // NOTE: not real token
                    github.Credentials = tokenAuth;
                    IReadOnlyList<Repository> contents = await github
                                                    .Repository.GetAllForUser(GITHUB_OrgName);

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
                    HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"{repoDTOs.Count} Repositories Found");


                }
                if (repoListRefreshTime.AddSeconds(25) < DateTime.Now)
                {
                    repoListRefreshTime = DateTime.Now;
                    repoListMsg = HTTPUtils.Instance.getRepoList(RunTimeSettings.customerAgentAuthKey);
                }

                if (!String.IsNullOrEmpty(repoListMsg))
                {
                    //Loop through and run any crons
                    repoDTOs = JsonConvert.DeserializeObject<List<RepoDTO>>(repoListMsg);
                    DateTime nextRunSoonest = DateTime.MaxValue;
                    foreach (RepoDTO repo in repoDTOs)
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
                        int totalMinLeft = ((int)nextRunJobspan.TotalMinutes);
                        HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Next Job in {totalMinLeft+1} minute(s) @ {nextRunSoonest.ToString()}");
                        if (totalMinLeft >= 0 && totalMinLeft < 2)
                            runJobs = true;
                    }
                    
                    if (runJobs)
                    {
                        bool queuedSuccess = false;
                        foreach (RepoDTO repo in repoDTOs)
                        {
                            if (!String.IsNullOrEmpty(repo.backupFrequency))
                            {
                                CrontabSchedule crontabSchedule = CrontabSchedule.Parse(repo.backupFrequency);
                                DateTime dt = crontabSchedule.GetNextOccurrence(DateTime.Now);
                                TimeSpan nextRunJobspan = nextRunSoonest.Subtract(DateTime.Now);
                                int totalMinLeft = ((int)nextRunJobspan.TotalMinutes);
                                if (totalMinLeft >= 0 && totalMinLeft < 2)
                                {
                                    GitHubArchiveWorker gitHubArchiveWorker = new GitHubArchiveWorker(this.GITHUB_PAT_Token, this.GITHUB_OrgName, this.customerGuid, this.googleBucketName, repo);
                                    if (queuedSuccess)
                                        ThreadUtilsV2.Instance.addToGitHubWorkerQueue(gitHubArchiveWorker);
                                    else
                                        queuedSuccess = ThreadUtilsV2.Instance.addToGitHubWorkerQueue(gitHubArchiveWorker);
                                    Thread.Sleep(50);

                                }
                            }
                        }
                        if (queuedSuccess)
                            repoListRefreshTime = DateTime.MinValue;
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
