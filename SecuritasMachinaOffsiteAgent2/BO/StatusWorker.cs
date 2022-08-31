
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
        public async Task<object> StartAsync()
        {


            //Console.Write("Testing Azure Blob Endpoint at " + RunTimeSettings.azureBlobEndpoint + " " + RunTimeSettings.azureBlobContainerName);
            if (blobServiceClient == null)
                blobServiceClient = new BlobServiceClient(RunTimeSettings.azureBlobEndpoint);
            if (stagingContainerClient == null)
                stagingContainerClient = blobServiceClient.GetBlobContainerClient(RunTimeSettings.azureBlobContainerName);

            HTTPUtils.Instance.writeToLog(RunTimeSettings.topicCustomerGuid, "INFO", $"Starting status worker");
            while (true)
            {
                DirListingDTO agentDirList = Utils.doDirListing(RunTimeSettings.topicCustomerGuid, RunTimeSettings.mountedDir);

                DirListingDTO stagingContainerDirListingDTO1 = await Utils.doDirListingAsync(stagingContainerClient.GetBlobsAsync());

                BlobContainerClient restoredContainerName = blobServiceClient.GetBlobContainerClient(RunTimeSettings.azureBlobRestoreContainerName);

                DirListingDTO restoredListingDTO = await Utils.doDirListingAsync(restoredContainerName.GetBlobsAsync());

                StatusDTO statusDTO = new StatusDTO();
                statusDTO.activeJobs = ThreadUtils.getActiveThreads();
                statusDTO.activeThreads = (long)Process.GetCurrentProcess().Threads.Count;
                statusDTO.UserProcessorTime = Process.GetCurrentProcess().UserProcessorTime.Ticks;
                statusDTO.TotalProcessorTime = Process.GetCurrentProcess().TotalProcessorTime.Ticks;
                statusDTO.WorkingSet64 = Process.GetCurrentProcess().WorkingSet64;
                statusDTO.TotalMemory = System.GC.GetTotalMemory(false);
                // PerformanceCounter ramCounter = new PerformanceCounter("Memory", "Available Bytes");
                // statusDTO.TotalMemory = (long)ramCounter.NextValue() ;
                statusDTO.AgentFileDTOs = agentDirList.fileDTOs;
                statusDTO.StagingFileDTOs = stagingContainerDirListingDTO1.fileDTOs;
                statusDTO.RestoredListingDTO = restoredListingDTO.fileDTOs;


                ServiceBusUtils.postMsg2ControllerAsync("agent/status", RunTimeSettings.topicCustomerGuid, "status", JsonConvert.SerializeObject(statusDTO));
                Thread.Sleep(1 * 60 * 1000 * RunTimeSettings.PollBaseTime);
            }

        }


    }
}
