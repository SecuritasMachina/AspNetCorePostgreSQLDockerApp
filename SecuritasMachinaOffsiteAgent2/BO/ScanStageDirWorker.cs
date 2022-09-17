
using Azure.Storage.Blobs;
using Common.DTO.V2;
using Common.Statics;
using Common.Utils.Comm;
using Newtonsoft.Json;
using SecuritasMachinaOffsiteAgent.DTO.V2;
using System.Diagnostics;
using System.IO;
using System.Web;

namespace SecuritasMachinaOffsiteAgent.BO
{
    public class ScanStageDirWorker
    {


        public ScanStageDirWorker()
        {

        }

        BlobServiceClient? blobServiceClient = null;
        BlobContainerClient stagingContainerClient = null;
        public void StartAsync()
        {
            if (blobServiceClient == null)
                blobServiceClient = new BlobServiceClient(RunTimeSettings.azureBlobEndpoint);
            if (stagingContainerClient == null)
                stagingContainerClient = blobServiceClient.GetBlobContainerClient(RunTimeSettings.azureSourceBlobContainerName);

            HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Scanning Azure Blob ContainerName:{RunTimeSettings.azureSourceBlobContainerName}");
            DirListingDTO stagingContainerDirListingDTO1 = Utils.doDirListingAsync(stagingContainerClient.GetBlobsAsync()).Result;
            foreach (FileDTO fileDTO in stagingContainerDirListingDTO1.fileDTOs)
            {
                if (!ThreadUtilsV2.Instance.isInQueue(fileDTO.FileName))
                {
                    Thread.Sleep(new Random().Next(250) + (500));
                    HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "INFO", $"Queuing {fileDTO.FileName}");
                    // spawn workers for files
                    BackupWorker backupWorker = new BackupWorker(RunTimeSettings.customerAgentAuthKey, RunTimeSettings.GoogleStorageBucketName, RunTimeSettings.azureBlobEndpoint, RunTimeSettings.azureSourceBlobContainerName, fileDTO.FileName, RunTimeSettings.envPassPhrase);
                    ThreadUtilsV2.Instance.addToBackupWorkerQueue(backupWorker);
                }
            }
            


            //ServiceBusUtils.postMsg2ControllerAsync("agent/status", RunTimeSettings.topicCustomerGuid, "status", JsonConvert.SerializeObject(statusDTO));


        }


    }
}
