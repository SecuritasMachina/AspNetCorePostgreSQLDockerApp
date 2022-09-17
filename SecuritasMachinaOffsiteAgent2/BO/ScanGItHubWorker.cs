using Common.DTO.V2;
using Common.Statics;
using Common.Utils.Comm;
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

        public ScanGitHubWorker(string GITHUB_PAT_Token, string GITHUB_OrgName, string customerGuid, string googleBucketName)
        {
            this.GITHUB_PAT_Token = GITHUB_PAT_Token;
            this.GITHUB_OrgName = GITHUB_OrgName;
            this.customerGuid = customerGuid;
            this.googleBucketName = googleBucketName;
        }

        public async Task StartAsync()
        {
            try
            {


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
                    //repoDTO.id = repo.Id;
                    repoDTOs.Add(repoDTO);

                }
                GenericMessage genericMessage = new GenericMessage();
                GenericMessage.msgTypes msgType = GenericMessage.msgTypes.REPOLIST;
                genericMessage.msgType = msgType.ToString();
                genericMessage.msg = JsonConvert.SerializeObject(repoDTOs);
                genericMessage.guid = RunTimeSettings.customerAgentAuthKey;
                HTTPUtils.Instance.putCache(RunTimeSettings.customerAgentAuthKey, "REPOLIST", JsonConvert.SerializeObject(genericMessage));


                HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"{repoDTOs.Count} Repositories Found");

                string genericMsg = HTTPUtils.Instance.getRepoList(RunTimeSettings.customerAgentAuthKey);
                if (!String.IsNullOrEmpty(genericMsg))
                {
                    //Loop through and run any crons
                    repoDTOs = JsonConvert.DeserializeObject<List<RepoDTO>>(genericMsg);
                    foreach (RepoDTO repo in repoDTOs)
                    {
                        GitHubArchiveWorker gitHubArchiveWorker = new GitHubArchiveWorker(this.GITHUB_PAT_Token, this.GITHUB_OrgName, this.customerGuid, this.googleBucketName, repo);
                        ThreadUtils.addToGitHubWorkerQueue(gitHubArchiveWorker);
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
