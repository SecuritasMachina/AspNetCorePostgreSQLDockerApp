using Common.DTO.V2;
using Common.Statics;
using Common.Utils.Comm;
using System.IO.Compression;
using Google.Cloud.Storage.V1;

namespace SecuritasMachinaOffsiteAgent.BO
{
    public class GitHubArchiveWorker
    {
        private string customerGuid;
        private string googleBucketName;
        private RepoDTO repo;

        public GitHubArchiveWorker( string customerGuid, string googleBucketName, RepoDTO pRepoDTO)
        {
            this.customerGuid = customerGuid;
            this.googleBucketName = googleBucketName;
            this.repo = pRepoDTO;
        }
        public override string ToString()
        {
            return repo.FullName;
        }
        public async Task<bool> StartAsync()
        {
            try
            {
                //Clone, then zip and store in google
                string path = $"{RunTimeSettings.DATAPATH}/Repos/{repo.FullName}";
                DirectoryInfo di = new DirectoryInfo(path);
                int retVal = 0;
                repo.lastSyncDate = Utils.getDBDateNow();
                if (!Directory.Exists(path))
                {
                    HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Mirroring {repo.Url} in {path}");
                    di = Directory.CreateDirectory(path);
                    String command = @"git clone --mirror " + repo.Url + " " + path;
                    retVal = Utils.ShellExec(path, command);
                    if (retVal != 0)
                        HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "ERROR", $"Error {retVal} while Cloning {repo.Url}");
                }
                else
                {
                    HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Syncing {repo.Url}");
                    retVal = Utils.ShellExec(path, "git fetch origin");
                    if (retVal != 0)
                        HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "ERROR", $"Error {retVal} while Syncing {repo.Url}");
                }
                
                DateTime now = Utils.getDBDateNow();
                TimeSpan lastBackupSpan = now.Subtract(repo.lastBackupDate);
                if (retVal == 0 && lastBackupSpan.TotalHours > repo.syncMinArchiveHours)
                {
                    
                    //repo.lastSyncDate = Utils.getDBDateNow();
                    HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Debug lastBackupSpan.TotalHours:{lastBackupSpan.TotalHours} while Syncing {repo.FullName}");
                    repo.lastBackupDate = Utils.getDBDateNow();
                    StorageClient googleClient = StorageClient.Create();

                    string basebackupName = repo.FullName.Replace("/", "_");
                    string outFileName = basebackupName + ".zip";
                   
                    outFileName = basebackupName + "-" + DateTime.Now.ToString("yyyy-MM-dd") + ".zip";

                    string zipName = $"{RunTimeSettings.DATAPATH}/Repos/{outFileName}";
                    try { File.Delete(zipName); } catch (Exception ignore) { }
                    ZipFile.CreateFromDirectory(path, zipName);
                    Utils.writeFileToGoogle(RunTimeSettings.customerAgentAuthKey, "application/zip", googleBucketName, basebackupName + ".zip", zipName, RunTimeSettings.envPassPhrase);
                    HTTPUtils.Instance.touchRepoLastBackup(RunTimeSettings.customerAgentAuthKey, repo);
                    HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "BACKUP-END", "Completed encryption, synced and archived : " + basebackupName);

                }
                else
                {
                    HTTPUtils.Instance.touchRepoLastSync(RunTimeSettings.customerAgentAuthKey, repo);
                    HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "SYNC-END", "Completed sync: " + repo.FullName);
                }


                return true;
            }
            catch (Exception ex)
            {
                HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "ERROR", $"GitHubArchiveWorker {ex.ToString()}");
            }
            return false;

        }


    }
}
