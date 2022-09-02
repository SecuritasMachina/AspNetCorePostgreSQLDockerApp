
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


            //Console.Write("Testing Azure Blob Endpoint at " + RunTimeSettings.azureBlobEndpoint + " " + RunTimeSettings.azureBlobContainerName);
            if (blobServiceClient == null)
                blobServiceClient = new BlobServiceClient(RunTimeSettings.azureBlobEndpoint);
            if (stagingContainerClient == null)
                stagingContainerClient = blobServiceClient.GetBlobContainerClient(RunTimeSettings.azureBlobContainerName);

            

   
                HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAuthKey, "TRACE", $"Scanning Azure Blob ContainerName:{RunTimeSettings.azureBlobContainerName}");
                DirListingDTO stagingContainerDirListingDTO1 =  Utils.doDirListingAsync(stagingContainerClient.GetBlobsAsync()).Result;
                foreach (FileDTO fileDTO in stagingContainerDirListingDTO1.fileDTOs)
                {

                    HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAuthKey, "INFO", $"Queuing {fileDTO.FileName}");
                    // spawn workers for files
                    BackupWorker backupWorker = new BackupWorker(RunTimeSettings.customerAuthKey, RunTimeSettings.azureBlobEndpoint, RunTimeSettings.azureBlobContainerName, fileDTO.FileName, RunTimeSettings.envPassPhrase);
                    ThreadUtils.addToQueue(backupWorker);
                }
                if (ThreadUtils.getActiveThreads() > 0)
                    Utils.UpdateOffsiteBytes(RunTimeSettings.customerAuthKey, RunTimeSettings.mountedDir);



                //ServiceBusUtils.postMsg2ControllerAsync("agent/status", RunTimeSettings.topicCustomerGuid, "status", JsonConvert.SerializeObject(statusDTO));
              

        }


    }
}
