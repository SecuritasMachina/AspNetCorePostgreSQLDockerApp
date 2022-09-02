
using Common.Statics;
using Common.Utils.Comm;
using System.Web;

namespace SecuritasMachinaOffsiteAgent.BO
{
    public class ArchiveWorker
    {
        private int retentionDays;

        private string inPath;
        private string customerGuid;

        public ArchiveWorker(string customerGuid, string inPath, int retentionDays)
        {
            this.customerGuid = customerGuid;
            this.inPath = inPath;
            this.retentionDays = retentionDays;

        }


        public void StartAsync()
        {
            


            DateTime purgeOlderDate = DateTime.Now.AddDays(retentionDays * -1);

            HTTPUtils.Instance.writeToLog(this.customerGuid, "INFO", $"Scanning {inPath} for Last Write Time over {retentionDays} old ({purgeOlderDate.ToShortDateString()})");
            DirectoryInfo directoryInfo = new DirectoryInfo(inPath);
            List<FileInfo> Files2 = directoryInfo.GetFiles("*").ToList();

            try
            {
                bool filesDeleted = false;
                foreach (FileInfo file in Files2)
                {
                    if (file.LastWriteTime < purgeOlderDate)
                    {
                        file.Delete();
                        HTTPUtils.Instance.writeToLog(this.customerGuid, "DELETING", file.Name);
                        filesDeleted = true;
                    }

                }
                if (filesDeleted)
                    Utils.UpdateOffsiteBytes(RunTimeSettings.customerAuthKey, RunTimeSettings.mountedDir);
            }
            catch (Exception ex)
            {
                HTTPUtils.Instance.writeToLog(this.customerGuid, "ERROR", inPath + " ArchiveWorker: " + ex.ToString());

            }
            Thread.Sleep(6 * 60 * 60 * 1000 * RunTimeSettings.PollBaseTime);
        }




    }
}
