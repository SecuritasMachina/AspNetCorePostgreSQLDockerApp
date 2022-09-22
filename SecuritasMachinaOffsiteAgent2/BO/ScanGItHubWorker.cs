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

        private static DateTime cacheRefreshTime;
        private static List<RepoDTO> _RepoDTOs;
        private bool _isBusy = false;
        // private List<CUSTOMERREPOS_DTO> _CustomerRepos;

        public ScanGitHubWorker(string customerGuid, string googleBucketName)
        {
            this.customerGuid = customerGuid;
            this.googleBucketName = googleBucketName;
            cacheRefreshTime = DateTime.MinValue;
            //repoListRefreshTime = DateTime.MinValue;
            _RepoDTOs = new List<RepoDTO>();
        }

        public async Task StartAsync()
        {
            int qSize = ThreadUtilsV2.Instance.getGitQueueSize();
            if (qSize > 0)
            {
                HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Waiting for {qSize} threads to finish");
                return;
            }
            _isBusy = true;
            List<CUSTOMERREPOS_DTO> _CustomerRepos = HTTPUtils.Instance.getCustomerReposList(RunTimeSettings.customerAgentAuthKey);
            foreach (CUSTOMERREPOS_DTO cUSTOMERREPOS_DTO in _CustomerRepos)
            {
                if (String.IsNullOrEmpty(cUSTOMERREPOS_DTO.authToken))
                    continue;



                GenericMessage genericMessage = new GenericMessage();
                try
                {
                    if (cacheRefreshTime.AddMinutes(5) < DateTime.Now)
                    {
                        cacheRefreshTime=DateTime.Now;
                        var github = new GitHubClient(new ProductHeaderValue($"SecuritasMachina_Agent_{VersionUtil.getAppName()}"));
                        var tokenAuth = new Credentials(cUSTOMERREPOS_DTO.authToken);
                        github.Credentials = tokenAuth;
                        IReadOnlyList<Repository> contents = await github
                                                        .Repository.GetAllForUser(cUSTOMERREPOS_DTO.authName);
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
                        HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Found {repoDTOs.Count} Repositories @ {RunTimeSettings.GITHUB_OrgName}");

                    }

                    _RepoDTOs = HTTPUtils.Instance.getRepoList(RunTimeSettings.customerAgentAuthKey);
                    //Loop through and run any crons
                    bool queuedSuccess = false;
                    HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Checking {_RepoDTOs.Count} GitHub Repositories");
                    foreach (RepoDTO repo in _RepoDTOs.Where(i => !String.IsNullOrEmpty(i.backupFrequency)))
                    {
                        CrontabSchedule crontabSchedule = CrontabSchedule.Parse(repo.backupFrequency);

                        DateTime now = Utils.getDBDateNow();
                        TimeSpan lastBackupSpan = now.Subtract(repo.lastBackupDate);
                        TimeSpan lastSyncSpan = now.Subtract(repo.lastSyncDate);

                        if (lastSyncSpan.TotalHours < repo.syncMinimumHours && lastBackupSpan.TotalHours < repo.syncMinArchiveHours)
                        {
                            HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Skip {repo.FullName} last Sync @ {lastSyncSpan.TotalHours} last Backup: {lastBackupSpan.TotalHours}");
                            continue;
                        }

                        if (!ThreadUtilsV2.Instance.isGitWorkerInQueue(repo.FullName))
                        {
                            GitHubArchiveWorker gitHubArchiveWorker = new GitHubArchiveWorker(cUSTOMERREPOS_DTO.authToken, cUSTOMERREPOS_DTO.authName, this.customerGuid, this.googleBucketName, repo);
                            bool success = ThreadUtilsV2.Instance.addToGitHubWorkerQueue(gitHubArchiveWorker);

                            if (!queuedSuccess)
                                queuedSuccess = success;
                            Thread.Sleep(10);
                        }

                    }



                }
                catch (Exception ex)
                {
                    HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "ERROR", $"ScanGitHubWorker {ex.ToString()}");
                }
            }

            _isBusy = false;

        }

        internal bool isBusy()
        {
            return _isBusy;
        }
    }
}
