using Common.DTO.V2;
using Common.Statics;
using Common.Utils.Comm;
using Newtonsoft.Json;
using Octokit;
using SecuritasMachinaOffsiteAgent.DTO.V2;

namespace SecuritasMachinaOffsiteAgent.BO
{
    public class ScanGitHubWorker
    {


        public ScanGitHubWorker(string GITHUB_PAT_Token, string GITHUB_OrgName)
        {

        }

        public async Task StartAsync()
        {
            try
            {
                var github = new GitHubClient(new ProductHeaderValue("SwcuritasMachina1")); // TODO: other setup
                var tokenAuth = new Credentials("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"); // NOTE: not real token
                github.Credentials = tokenAuth;
                IReadOnlyList<Repository> contents = await github
                                                .Repository.GetAllForUser("SecuritasMAchina");
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
                genericMessage.guid = RunTimeSettings.customerAuthKey;
                HTTPUtils.Instance.putCache(RunTimeSettings.customerAuthKey, "REPOLIST", JsonConvert.SerializeObject(genericMessage));


                //git clone --mirror https://primary_repo_url/primary_repo.git
                // git fetch origin

                //Console.Write("Testing Azure Blob Endpoint at " + RunTimeSettings.azureBlobEndpoint + " " + RunTimeSettings.azureBlobContainerName);



                HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAuthKey, "TRACE", $"repoDTOs count:{repoDTOs.Count}");
            }
            catch (Exception ex)
            {
                HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAuthKey, "ERROR", $"ScanGitHubWorker {ex.ToString()}");
            }

        }


    }
}
