
using Common.DTO.V2;
using Common.Statics;
using Common.Utils.Comm;
using SecuritasMachinaOffsiteAgent.Utils.Comm.GoogleAPI;

namespace SecuritasMachinaOffsiteAgent.BO
{
    public class ArchiveWorker
    {
        private int retentionDays;

        private string googleBucketName;
        private string authtoken;

        public ArchiveWorker(string pAuthkey, string inPath, int retentionDays)
        {
            this.authtoken = pAuthkey;
            this.googleBucketName = inPath;
            this.retentionDays = retentionDays;

        }


        public void StartAsync()
        {
        
            DateTime purgeOlderDate = DateTime.Now.AddDays(retentionDays * -1);

            HTTPUtils.Instance.writeToLogAsync(this.authtoken, "INFO", $"Scanning {googleBucketName} for Last Write Time over {retentionDays} days old ({purgeOlderDate.ToShortDateString()})");

            DirListingDTO dirListingDTO = CloudUtils.Instance.listFiles(googleBucketName);
           

            try
            {
                bool filesDeleted = false;
                foreach (FileDTO file in dirListingDTO.fileDTOs)
                {
                    if (file.lastWriteDateTime < purgeOlderDate)
                    {
                        
                        HTTPUtils.Instance.writeToLogAsync(this.authtoken, "DELETING", file.FileName);
                        CloudUtils.Instance.deleteFile(googleBucketName, file.FileName);
                        filesDeleted = true;
                    }

                }
                if (filesDeleted)
                    Utils.UpdateOffsiteBytes(RunTimeSettings.customerAgentAuthKey, RunTimeSettings.GoogleStorageBucketName);
            }
            catch (Exception ex)
            {
                HTTPUtils.Instance.writeToLogAsync(this.authtoken, "ERROR", googleBucketName + " ArchiveWorker: " + ex.ToString());

            }
            //Thread.Sleep(6 * 60 * 60 * 1000 * RunTimeSettings.PollBaseTime);
        }




    }
}
