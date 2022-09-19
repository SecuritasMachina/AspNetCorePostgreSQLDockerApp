
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
        private bool _isBusy;

        public UpdateOffSiteBytesWorker(string customerGuid, string inPath, int retentionDays)
        {
            this.customerGuid = customerGuid;
            this.inPath = inPath;
            this.retentionDays = retentionDays;

        }


        public void StartAsync()
        {
            _isBusy = true;
            Utils.UpdateOffsiteBytes(this.customerGuid, inPath);
            _isBusy = false;
        }

        internal bool isBusy()
        {
            return _isBusy;
        }
    }
}
