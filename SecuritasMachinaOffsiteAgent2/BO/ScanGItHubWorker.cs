using Common.DTO.V2;
using Common.Statics;
using Common.Utils.Comm;
using NCrontab;
using Newtonsoft.Json;
using Octokit;
using SecuritasMachinaOffsiteAgent.DTO.V2;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace SecuritasMachinaOffsiteAgent.BO
{
    public class ScanGitHubWorker
    {
        private string customerGuid;
        private string googleBucketName;

        public ScanGitHubWorker(string GITHUB_PAT_Token, string GITHUB_OrgName, string customerGuid, string googleBucketName)
        {
            this.customerGuid = customerGuid;
            this.googleBucketName = googleBucketName;
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


                HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAuthKey, "TRACE", $"repoDTOs count:{repoDTOs.Count}");

                string genericMsg = HTTPUtils.Instance.getRepoList(RunTimeSettings.customerAuthKey);
                if (!String.IsNullOrEmpty(genericMsg))
                {
                    //Loop through and run any crons
                    repoDTOs = JsonConvert.DeserializeObject<List<RepoDTO>>(genericMsg);
                    long dateEnteredtimestamp = new DateTimeOffset(DateTime.UtcNow).ToUniversalTime().ToUnixTimeMilliseconds();
                    foreach (RepoDTO repo in repoDTOs)
                    {
                        if (!String.IsNullOrEmpty(repo.backupFrequency))
                        {
                            CrontabSchedule crontabSchedule = CrontabSchedule.Parse(repo.backupFrequency);
                            //crontabSchedule.GetNextOccurrence();
                            //Clone, then zip and store in google
                            string path = $"/mnt/offsite/Repos/{repo.FullName}";
                            DirectoryInfo di = new DirectoryInfo(path);
                            int retVal = 0;
                            if (!Directory.Exists(path))
                            {
                                di = Directory.CreateDirectory(path);
                                String command = @" git clone --mirror " + repo.Url + " " + path;
                                retVal = Utils.ShellExec(path, command);

                            }
                            else
                            {
                                retVal = Utils.ShellExec(path, "git fetch origin");
                            }

                            if (retVal != 0)
                            {
                                Console.WriteLine($"retVal:{retVal}");
                            }
                            else
                            {
                                string zipName = $"/mnt/offsite/Repos/{repo.FullName.Replace("/", "_")}-{dateEnteredtimestamp}.zip";
                                ZipFile.CreateFromDirectory(path, zipName);

                                Utils.writeFileToGoogle(RunTimeSettings.customerAuthKey, "application/zip", googleBucketName, zipName, RunTimeSettings.envPassPhrase);
                                //
                            }



                        }
                    }
                }
            }
            catch (Exception ex)
            {
                HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAuthKey, "ERROR", $"ScanGitHubWorker {ex.ToString()}");
            }

        }


    }
}
