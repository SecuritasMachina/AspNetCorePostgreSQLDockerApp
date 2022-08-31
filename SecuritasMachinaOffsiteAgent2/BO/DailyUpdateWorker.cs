
using Common.Statics;
using Common.Utils.Comm;
using System.Web;

namespace SecuritasMachinaOffsiteAgent.BO
{
    public class UpdateOffSiteBytesWorker
    {
        private int retentionDays;

        private string inPath;
        private string customerGuid;

        public UpdateOffSiteBytesWorker(string customerGuid, string inPath, int retentionDays)
        {
            this.customerGuid = customerGuid;
            this.inPath = inPath;
            this.retentionDays = retentionDays;

        }


        public async Task<object> StartAsync()
        {
            HTTPUtils.Instance.writeToLog(RunTimeSettings.topicCustomerGuid, "INFO", $"Starting OffSite worker for {inPath}");

            while (true)
            {
                Utils.UpdateOffsiteBytes(this.customerGuid, inPath);
               
                Thread.Sleep(1 * 60 * 60 * 1000 * RunTimeSettings.PollBaseTime);
            }
            
        }


    }
}
