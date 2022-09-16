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
    public class GitHubArchiveWorker
    {
        private string customerGuid;
        private string googleBucketName;
        private RepoDTO repo;
        private string GITHUB_PAT_Token;
        private string GITHUB_OrgName;

        public GitHubArchiveWorker(string GITHUB_PAT_Token, string GITHUB_OrgName, string customerGuid, string googleBucketName, RepoDTO pRepoDTO)
        {
            this.GITHUB_PAT_Token = GITHUB_PAT_Token;
            this.GITHUB_OrgName = GITHUB_OrgName;
            this.customerGuid = customerGuid;
            this.googleBucketName = googleBucketName;
            this.repo = pRepoDTO;
        }
        public override string ToString()
        {
            return repo.FullName;
        }
        public async Task StartAsync()
        {
            try
            {
                if (!String.IsNullOrEmpty(repo.backupFrequency))
                {
                    CrontabSchedule crontabSchedule = CrontabSchedule.Parse(repo.backupFrequency);
                    DateTime now = DateTime.Now;
                    DateTime dt = crontabSchedule.GetNextOccurrence(now);
                    TimeSpan span = dt.Subtract(now);
                    TimeSpan span2 = now.Subtract(repo.lastBackupDate);

                    if (span2.TotalMinutes < 5) 
                    {
                        return;
                    }

                    if (((int)span.TotalMinutes) > 1)
                        return;


                    var github = new GitHubClient(new ProductHeaderValue($"SecuritasMachina_Agent_{VersionUtil.getAppName()}")); // TODO: other setup
                    var tokenAuth = new Credentials(GITHUB_PAT_Token); // NOTE: not real token
                    github.Credentials = tokenAuth;

                    //Clone, then zip and store in google
                    string path = $"/mnt/offsite/Repos/{repo.FullName}";
                    DirectoryInfo di = new DirectoryInfo(path);
                    int retVal = 0;
                    if (!Directory.Exists(path))
                    {
                        HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Cloning {repo.Url}");
                        di = Directory.CreateDirectory(path);
                        String command = @"git clone --mirror " + repo.Url + " " + path;
                        retVal = Utils.ShellExec(path, command);
                        if(retVal != 0)
                            HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAgentAuthKey, "ERROR", $"Error {retVal} while Cloning {repo.Url}");
                    }
                    else
                    {
                        HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Syncing {repo.Url}");
                        retVal = Utils.ShellExec(path, "git fetch origin");
                        if (retVal != 0)
                            HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAgentAuthKey, "ERROR", $"Error {retVal} while Syncing {repo.Url}");
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
                        try { File.Delete(zipName); } catch (Exception ignore) { }
                        ZipFile.CreateFromDirectory(path, zipName);

                        Utils.writeFileToGoogle(RunTimeSettings.customerAgentAuthKey, "application/zip", googleBucketName, basebackupName + ".zip", zipName, RunTimeSettings.envPassPhrase);
                        HTTPUtils.Instance.touchRepoLastBackup(RunTimeSettings.customerAgentAuthKey, repo);
                        HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAgentAuthKey, "BACKUP-END", "Completed encryption, synced and archived : " + basebackupName);



                    }



                }

            }
            catch (Exception ex)
            {
                HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAgentAuthKey, "ERROR", $"GitHubArchiveWorker {ex.ToString()}");
            }

        }


    }
}
