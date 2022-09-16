using ASquare.WindowsTaskScheduler.Models;
using ASquare.WindowsTaskScheduler;
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
using Google.Cloud.Storage.V1;

namespace SecuritasMachinaOffsiteAgent.BO
{
    public class ScanGitHubWorker
    {
        private string customerGuid;
        private string googleBucketName;
        private static string workers = "";
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


                var github = new GitHubClient(new ProductHeaderValue("SwcuritasMachina1")); // TODO: other setup
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


                HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAgentAuthKey, "TRACE", $"repoDTOs count:{repoDTOs.Count}");

                string genericMsg = HTTPUtils.Instance.getRepoList(RunTimeSettings.customerAgentAuthKey);
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
                            DateTime now = DateTime.Now;
                            DateTime dt = crontabSchedule.GetNextOccurrence(now);
                            TimeSpan span = dt.Subtract(now);
                            TimeSpan span2 = repo.lastBackupDate.Subtract(now);

                            if (span2.TotalMinutes > -5)
                            {
                                continue;
                            }

                            if (((int)span.TotalMinutes) > 1)
                                continue;

                            if (workers.Contains(repo.FullName))
                            {
                                continue;
                            }
                            workers += repo.FullName;


                            //Clone, then zip and store in google
                            string path = $"/mnt/offsite/Repos/{repo.FullName}";
                            //Console.WriteLine(path);
                            DirectoryInfo di = new DirectoryInfo(path);
                            int retVal = 0;
                            if (!Directory.Exists(path))
                            {
                                HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Cloning {repo.Url}");
                                di = Directory.CreateDirectory(path);
                                String command = @"git clone --mirror " + repo.Url + " " + path;
                                retVal = Utils.ShellExec(path, command);
                            }
                            else
                            {
                                HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Syncing {repo.Url}");
                                retVal = Utils.ShellExec(path, "git fetch origin");
                            }

                            if (retVal != 0)
                            {
                                Console.WriteLine($"retVal:{retVal}");
                            }
                            else
                            {
                                int generationCount = 1;
                                StorageClient googleClient = StorageClient.Create();

                                string basebackupName = repo.FullName.Replace("/", "_");
                                string outFileName = basebackupName + ".zip";
                                while (true)
                                {
                                    if (generationCount > 99999)
                                    {
                                        break;
                                    }


                                    if (googleClient.ListObjects(googleBucketName, outFileName).Count() > 0)
                                    {

                                        outFileName = basebackupName + "-" + generationCount + ".zip";
                                        generationCount++;
                                    }
                                    else { break; }
                                }
                                string zipName = $"/mnt/offsite/Repos/{outFileName}";
                                ZipFile.CreateFromDirectory(path, zipName);

                                Utils.writeFileToGoogle(RunTimeSettings.customerAgentAuthKey, "application/zip", googleBucketName, basebackupName + ".zip", zipName, RunTimeSettings.envPassPhrase);
                                HTTPUtils.Instance.touchRepoLastBackup(RunTimeSettings.customerAgentAuthKey, repo);

                                workers = workers.Replace(repo.FullName, "");

                            }



                        }
                    }
                }
            }
            catch (Exception ex)
            {
                HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAgentAuthKey, "ERROR", $"ScanGitHubWorker {ex.ToString()}");
            }

        }


    }
}
