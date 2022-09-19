
using Azure.Storage.Blobs;
using Common.DTO.V2;
using Common.Statics;
using Common.Utils.Comm;
using Newtonsoft.Json;
using SecuritasMachinaOffsiteAgent.DTO.V2;
using System.Diagnostics;
using System.Web;

namespace SecuritasMachinaOffsiteAgent.BO
{
    public class StatusWorker
    {


        public StatusWorker()
        {

        }

        BlobServiceClient? blobServiceClient = null;
        BlobContainerClient stagingContainerClient = null;
        private bool _isBusy = false;

        public void StartAsync()
        {
            _isBusy = true;

            if (blobServiceClient == null)
                blobServiceClient = new BlobServiceClient(RunTimeSettings.azureBlobEndpoint);
            if (stagingContainerClient == null)
                stagingContainerClient = blobServiceClient.GetBlobContainerClient(RunTimeSettings.azureSourceBlobContainerName);

            DirListingDTO agentDirList = Utils.doDirListing(RunTimeSettings.customerAgentAuthKey, RunTimeSettings.GoogleArchiveBucketName);

            DirListingDTO stagingContainerDirListingDTO1 =  Utils.doDirListingAsync(stagingContainerClient.GetBlobsAsync()).Result;

            BlobContainerClient restoredContainerName = blobServiceClient.GetBlobContainerClient(RunTimeSettings.azureBlobRestoreContainerName);

            DirListingDTO restoredListingDTO = Utils.doDirListingAsync(restoredContainerName.GetBlobsAsync()).Result;

            StatusDTO statusDTO = new StatusDTO();
            statusDTO.activeThreads = (long)Process.GetCurrentProcess().Threads.Count;
            statusDTO.UserProcessorTime = Process.GetCurrentProcess().UserProcessorTime.Ticks;
            statusDTO.TotalProcessorTime = Process.GetCurrentProcess().TotalProcessorTime.Ticks;
            statusDTO.WorkingSet64 = Process.GetCurrentProcess().WorkingSet64;
            statusDTO.TotalMemory = System.GC.GetTotalMemory(false);
            statusDTO.AgentFileDTOs = agentDirList.fileDTOs;
            statusDTO.StagingFileDTOs = stagingContainerDirListingDTO1.fileDTOs;
            statusDTO.RestoredListingDTO = restoredListingDTO.fileDTOs;
            ServiceBusUtils.Instance.postMsg2ControllerAsync("agent/status", RunTimeSettings.customerAgentAuthKey, "status", JsonConvert.SerializeObject(statusDTO));
            _isBusy = false;

        }

        internal bool isBusy()
        {
            return _isBusy;
        }
    }
}
