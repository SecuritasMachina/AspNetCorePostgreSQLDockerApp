
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
        public async Task<object> StartAsync()
        {


            //Console.Write("Testing Azure Blob Endpoint at " + RunTimeSettings.azureBlobEndpoint + " " + RunTimeSettings.azureBlobContainerName);
            if (blobServiceClient == null)
                blobServiceClient = new BlobServiceClient(RunTimeSettings.azureBlobEndpoint);
            if (stagingContainerClient == null)
                stagingContainerClient = blobServiceClient.GetBlobContainerClient(RunTimeSettings.azureBlobContainerName);

            HTTPUtils.Instance.writeToLog(RunTimeSettings.topicCustomerGuid, "INFO", $"Starting Staging scanner worker for {RunTimeSettings.azureBlobContainerName}");

            while (true)
            {
                HTTPUtils.Instance.writeToLog(RunTimeSettings.topicCustomerGuid, "TRACE", $"Scanning Azure Blob ContainerName:{RunTimeSettings.azureBlobContainerName}");
                DirListingDTO stagingContainerDirListingDTO1 = await Utils.doDirListingAsync(stagingContainerClient.GetBlobsAsync());
                foreach (FileDTO fileDTO in stagingContainerDirListingDTO1.fileDTOs)
                {

                    HTTPUtils.Instance.writeToLog(RunTimeSettings.topicCustomerGuid, "INFO", $"Queuing {fileDTO.FileName}");
                    // spawn workers for files
                    BackupWorker backupWorker = new BackupWorker(RunTimeSettings.topicCustomerGuid, RunTimeSettings.azureBlobEndpoint, RunTimeSettings.azureBlobContainerName, fileDTO.FileName, RunTimeSettings.envPassPhrase);
                    ThreadUtils.addToQueue(backupWorker);
                }
                if (ThreadUtils.getActiveThreads() > 0)
                    Utils.UpdateOffsiteBytes(RunTimeSettings.topicCustomerGuid, RunTimeSettings.mountedDir);



                //ServiceBusUtils.postMsg2ControllerAsync("agent/status", RunTimeSettings.topicCustomerGuid, "status", JsonConvert.SerializeObject(statusDTO));
                Thread.Sleep(1 * 60 * 1000 * RunTimeSettings.PollBaseTime);
            }

        }


    }
}
