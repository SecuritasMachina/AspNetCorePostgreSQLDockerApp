
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

        public ArchiveWorker(string customerGuid, string inPath,int retentionDays)
        {
            this.customerGuid = customerGuid;
            this.inPath = inPath;
            this.retentionDays = retentionDays;

        }


        public async Task<object> StartAsync()
        {
            while (true)
            {
                HTTPUtils.Instance.writeToLog(this.customerGuid, "TRACE", $"Scanning {inPath} for Last Write Time over {retentionDays} old");
                DirectoryInfo directoryInfo = new DirectoryInfo(inPath);
                List<FileInfo> Files2 = directoryInfo.GetFiles("*").ToList();

                try
                {
                    foreach (FileInfo file in Files2)
                    {
                        if (file.LastWriteTime < DateTime.Now.AddDays(retentionDays * -1))
                            file.Delete();

                    }
                }
                catch (Exception ex)
                {
                    HTTPUtils.Instance.writeToLog(this.customerGuid, "ERROR", inPath + " " + ex.ToString());

                }
                Thread.Sleep(6*60*60 * 1000* RunTimeSettings.PollBaseTime);
            }
          
        }


    }
}
