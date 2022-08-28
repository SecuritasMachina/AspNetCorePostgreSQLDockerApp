
using Common.Statics;
using Common.Utils.Comm;
using System.Web;

namespace SecuritasMachinaOffsiteAgent.BO
{
    public class DailyUpdateWorker
    {
        private int retentionDays;

        private string inPath;
        private string customerGuid;

        public DailyUpdateWorker(string customerGuid, string inPath, int retentionDays)
        {
            this.customerGuid = customerGuid;
            this.inPath = inPath;
            this.retentionDays = retentionDays;

        }


        public async Task<object> StartAsync()
        {
            while (true)
            {
                HTTPUtils.Instance.writeToLog(this.customerGuid, "TRACE", $"Scanning {inPath} total size");
                DirectoryInfo directoryInfo = new DirectoryInfo(inPath);
                List<FileInfo> Files2 = directoryInfo.GetFiles("*").ToList();
                long tsize = 0;
                try
                {
                    foreach (FileInfo file in Files2)
                    {
                        tsize += file.Length;

                    }
                    HTTPUtils.Instance.writeToLog(this.customerGuid, "TOTALOFFSITEBYTES", $"{tsize}");
                    HTTPUtils.Instance.writeToLog(this.customerGuid, "TOTALOFFSITECOUNT", $"{Files2.LongCount()}");
                }
                catch (Exception ex)
                {
                    HTTPUtils.Instance.writeToLog(this.customerGuid, "ERROR", inPath + " " + ex.ToString());

                }
                Thread.Sleep(6 * 60 * 60 * 1000 * RunTimeSettings.PollBaseTime);
            }
            
        }


    }
}
