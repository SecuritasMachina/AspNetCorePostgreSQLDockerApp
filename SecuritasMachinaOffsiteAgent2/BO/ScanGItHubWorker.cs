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
        private static List<RepoDTO> _RepoDTOs;
        private bool _isBusy = false;

        public ScanGitHubWorker(string GITHUB_PAT_Token, string GITHUB_OrgName, string customerGuid, string googleBucketName)
        {
            this.GITHUB_PAT_Token = GITHUB_PAT_Token;
            this.GITHUB_OrgName = GITHUB_OrgName;
            this.customerGuid = customerGuid;
            this.googleBucketName = googleBucketName;
            cacheRefreshTime = DateTime.MinValue;
            //repoListRefreshTime = DateTime.MinValue;
            _RepoDTOs = new List<RepoDTO>();
        }

        public async Task StartAsync()
        {

            if (String.IsNullOrEmpty(GITHUB_PAT_Token))
            {
                HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "ERROR", $"GITHUB_PAT_Token is not defined, skipping GitHub");
                return;
            }
            int qSize = ThreadUtilsV2.Instance.getGitQueueSize();
            if (qSize > 0)
            {
                HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Waiting for {qSize} threads to finish");
                return;
            }
            _isBusy = true;
            GenericMessage genericMessage = new GenericMessage();
            try
            {
                if (cacheRefreshTime.AddMinutes(5) < DateTime.Now)
                {
                    cacheRefreshTime = DateTime.Now;
                    var github = new GitHubClient(new ProductHeaderValue($"SecuritasMachina_Agent_{VersionUtil.getAppName()}")); 
                    var tokenAuth = new Credentials(GITHUB_PAT_Token); 
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
                        //HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Skip {repo.FullName} last Sync @ {repo.lastSyncDate.ToString()}");
                        continue;
                    }

                    if (!ThreadUtilsV2.Instance.isGitWorkerInQueue(repo.FullName))
                    {
                        GitHubArchiveWorker gitHubArchiveWorker = new GitHubArchiveWorker(this.GITHUB_PAT_Token, this.GITHUB_OrgName, this.customerGuid, this.googleBucketName, repo);
                        bool success = ThreadUtilsV2.Instance.addToGitHubWorkerQueue(gitHubArchiveWorker);

                        if (!queuedSuccess)
                            queuedSuccess = success;
                        Thread.Sleep(10);
                    }

                }
                _isBusy = false;


            }
            catch (Exception ex)
            {
                HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "ERROR", $"ScanGitHubWorker {ex.ToString()}");
            }

        }

        internal bool isBusy()
        {
            return _isBusy;
        }
    }
}
