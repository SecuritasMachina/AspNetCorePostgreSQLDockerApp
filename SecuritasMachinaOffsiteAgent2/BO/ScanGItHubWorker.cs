
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
    public class ScanGitHubWorker
    {


        public ScanGitHubWorker(string GITHUB_PAT_Token,string GITHUB_OrgName)
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
                stagingContainerClient = blobServiceClient.GetBlobContainerClient(RunTimeSettings.azureSourceBlobContainerName);




            HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAuthKey, "TRACE", $"Scanning Azure Blob ContainerName:{RunTimeSettings.azureSourceBlobContainerName}");
            DirListingDTO stagingContainerDirListingDTO1 = Utils.doDirListingAsync(stagingContainerClient.GetBlobsAsync()).Result;
            foreach (FileDTO fileDTO in stagingContainerDirListingDTO1.fileDTOs)
            {
                Thread.Sleep(new Random().Next(250)+(1*1000));
                if (!ThreadUtils.isInQueue(fileDTO.FileName))
                {
                    HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAuthKey, "INFO", $"Queuing {fileDTO.FileName}");
                    // spawn workers for files
                    BackupWorker backupWorker = new BackupWorker(RunTimeSettings.customerAuthKey, RunTimeSettings.GoogleStorageBucketName, RunTimeSettings.azureBlobEndpoint, RunTimeSettings.azureSourceBlobContainerName, fileDTO.FileName, RunTimeSettings.envPassPhrase);
                    ThreadUtils.addToQueue(backupWorker);
                }
            }
            if (ThreadUtils.getActiveThreads() > 0)
                Utils.UpdateOffsiteBytes(RunTimeSettings.customerAuthKey, RunTimeSettings.GoogleStorageBucketName);



            //ServiceBusUtils.postMsg2ControllerAsync("agent/status", RunTimeSettings.topicCustomerGuid, "status", JsonConvert.SerializeObject(statusDTO));


        }


    }
}
