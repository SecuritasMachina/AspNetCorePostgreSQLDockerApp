using System;
using System.Collections.Generic;
using System.Diagnostics;

using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Newtonsoft.Json;
using Google.Apis.Storage.v1.Data;
using Google.Cloud.Storage.V1;
using System.Security.Cryptography;

using System.Runtime.InteropServices;
using Azure.Storage.Blobs.Models;
using Common.Statics;
using Common.Utils.Comm;
using Common.DTO.V2;
using SecuritasMachinaOffsiteAgent.DTO.V2;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;

namespace SecuritasMachinaOffsiteAgent.BO
{
    internal class ListenerWorker
    {


        static string? azureBlobEndpoint = Environment.GetEnvironmentVariable("azureBlobEndpoint");
        static string? envPassPhrase = Environment.GetEnvironmentVariable("passPhrase");
        static string? azureBlobContainerName = Environment.GetEnvironmentVariable("azureBlobContainerName");
        static string? azureBlobRestoreContainerName = Environment.GetEnvironmentVariable("azureBlobRestoreContainerName");
        static int RetentionDays = 45;
        static string mountedDir = "/mnt/offsite/";
        // the client that owns the connection and can be used to create senders and receivers
        ServiceBusClient client;

        // the processor that reads and processes messages from the queue
        ServiceBusProcessor processor;
        // private int tLoopCount;



        internal async Task startAsync()
        {
            try
            {
                RetentionDays = Int32.Parse(Environment.GetEnvironmentVariable("RetentionDays"));
            }
            catch (Exception ignore) { }
            if (azureBlobRestoreContainerName == null)
                azureBlobRestoreContainerName = "restored";
            RunTimeSettings.topicCustomerGuid = Environment.GetEnvironmentVariable("customerGuid");

            Console.WriteLine();
            Console.WriteLine("Starting ListenerWorker azureBlobEndpoint:" + azureBlobEndpoint);
            Console.WriteLine("azureBlobContainerName:" + azureBlobContainerName);
            Console.WriteLine("azureBlobRestoreContainerName:" + azureBlobRestoreContainerName);
            Console.WriteLine("customerGuid:" + RunTimeSettings.topicCustomerGuid);
            Console.WriteLine("RetentionDays:" + RetentionDays);
            
            
            
            if (envPassPhrase != null)
                Console.WriteLine("passPhrase Length:" + envPassPhrase.Length);

            HTTPUtils.Instance.populateRuntime(RunTimeSettings.topicCustomerGuid);
            HTTPUtils.Instance.writeToLog(RunTimeSettings.topicCustomerGuid, "CONFIGINFO", $"azureBlobEndpoint length: {azureBlobEndpoint.Length} azureBlobContainerName:{azureBlobContainerName} azureBlobRestoreContainerName:{azureBlobRestoreContainerName} RetentionDays:{RetentionDays} passPhrase Length: {envPassPhrase.Length}");
            if (RunTimeSettings.SBConnectionString == null || RunTimeSettings.SBConnectionString.Length == 0)
            {
                Console.WriteLine("!!! Unable to retrieve configuration !!!");
                Environment.Exit(1);
            }

            // Create the client object that will be used to create sender and receiver objects
            client = new ServiceBusClient(RunTimeSettings.SBConnectionString);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                mountedDir = "c:\\temp\\";
            }

            DirectoryInfo directoryInfo = new DirectoryInfo(mountedDir);
            FileInfo[] Files = directoryInfo.GetFiles("*"); //Getting Text files

            Console.WriteLine(mountedDir + " has " + Files.Length + " files"); ;

            Console.Write("Testing writing to " + mountedDir);
            try
            {
                using (StreamWriter sw = new StreamWriter(mountedDir + "test.txt"))
                {
                    sw.WriteLine("test");
                }
                Console.WriteLine("...Success");
            }
            catch (Exception ex)
            {
                HTTPUtils.Instance.writeToLog(RunTimeSettings.topicCustomerGuid, "ERROR", "Error writing to " + mountedDir + " - Ensure VM instance has full access to cloud storage");
                Console.WriteLine("...Error writing to " + mountedDir + " - Ensure VM instance has full access to cloud storage");
            }
            Console.Write("Testing reading from " + mountedDir);
            try
            {
                string line = "";
                using (StreamReader sr = new StreamReader(mountedDir + "test.txt"))
                {
                    while ((line = sr.ReadLine()) != null)
                    {
                        //Console.WriteLine(line);
                    }
                }
                Console.WriteLine("...Success");
            }
            catch (Exception ex)
            {
                HTTPUtils.Instance.writeToLog(RunTimeSettings.topicCustomerGuid, "ERROR", "Error reading from  " + mountedDir + " - Ensure VM instance has full access to cloud storage");
                Console.WriteLine("...Error reading from " + mountedDir + " - Ensure VM instance has FULL access to Google cloud storage");
            }
            Console.Write("Testing delete at " + mountedDir);
            try
            {
                File.Delete(mountedDir + "test.txt");
                Console.WriteLine("...Success");
            }
            catch (Exception ex)
            {
                HTTPUtils.Instance.writeToLog(RunTimeSettings.topicCustomerGuid, "ERROR", "...Error deleting at " + mountedDir + " - Ensure VM instance has FULL access to Google cloud storage");
                Console.WriteLine("...Error deleting at " + mountedDir + " - Ensure VM instance has FULL access to Google cloud storage");
            }
            //TODO test dir listing of blob container
            BlobServiceClient? blobServiceClient = null;
            BlobContainerClient stagingContainerClient = null;
            try
            {
                Console.Write("Testing Azure Blob Endpoint at " + azureBlobEndpoint + " " + azureBlobContainerName);
                blobServiceClient = new BlobServiceClient(azureBlobEndpoint);
                stagingContainerClient = blobServiceClient.GetBlobContainerClient(azureBlobContainerName);

                await foreach (BlobItem blobItem in stagingContainerClient.GetBlobsAsync())
                {
 
                }
                Console.WriteLine("...Success");
            }
            catch (Exception ex)
            {
                
                HTTPUtils.Instance.writeToLog(RunTimeSettings.topicCustomerGuid, "ERROR", "...Error listing at " + azureBlobEndpoint + " " + azureBlobContainerName + " - Ensure VM instance has FULL access to Azure cloud storage "+ex.ToString());
               
            }



            try
            {
                // create a processor that we can use to process the messages
                HTTPUtils.Instance.writeToLog(RunTimeSettings.topicCustomerGuid, "INFO", $"Starting Listener on {RunTimeSettings.topicCustomerGuid}");
                processor = client.CreateProcessor(RunTimeSettings.topicCustomerGuid, RunTimeSettings.clientSubscriptionName, new ServiceBusProcessorOptions());
                
                // Console.WriteLine("Listening");
                // add handler to process messages
                processor.ProcessMessageAsync += MessageHandler;

                // add handler to process any errors
                processor.ProcessErrorAsync += ErrorHandler;

                // start processing 
                await processor.StartProcessingAsync();
                HTTPUtils.Instance.writeToLog(RunTimeSettings.topicCustomerGuid, "INFO", $"Listening on {RunTimeSettings.topicCustomerGuid}");
                //Start up background jobs
                ArchiveWorker archiveWorker = new ArchiveWorker(RunTimeSettings.topicCustomerGuid, mountedDir, RetentionDays);

                Task task = Task.Run(() => archiveWorker.StartAsync());

                DailyUpdateWorker dailyUpdateWorker = new DailyUpdateWorker(RunTimeSettings.topicCustomerGuid, mountedDir, RetentionDays);

                Task dailyTask = Task.Run(() => dailyUpdateWorker.StartAsync());
                
                while (true)
                {
                    HTTPUtils.Instance.writeToLog(RunTimeSettings.topicCustomerGuid, "TRACE", $"Scanning Azure Blob ContainerName:{azureBlobContainerName}");

                    try
                    {
                        DirListingDTO agentDirList = Utils.doDirListing(RunTimeSettings.topicCustomerGuid, mountedDir);
                        
                        DirListingDTO stagingContainerDirListingDTO1 = await Utils.doDirListingAsync(stagingContainerClient.GetBlobsAsync());

                        BlobContainerClient restoredContainerName = blobServiceClient.GetBlobContainerClient(azureBlobRestoreContainerName);

                        DirListingDTO restoredListingDTO = await Utils.doDirListingAsync(restoredContainerName.GetBlobsAsync());

                        StatusDTO statusDTO = new StatusDTO();
                       
                        statusDTO.activeThreads = (long)Process.GetCurrentProcess().Threads.Count; 
                        statusDTO.UserProcessorTime = Process.GetCurrentProcess().UserProcessorTime.Ticks;
                        statusDTO.TotalProcessorTime = Process.GetCurrentProcess().TotalProcessorTime.Ticks;
                        statusDTO.WorkingSet64 = Process.GetCurrentProcess().WorkingSet64;
                        statusDTO.TotalMemory=System.GC.GetTotalMemory(false);
                       // PerformanceCounter ramCounter = new PerformanceCounter("Memory", "Available Bytes");
                       // statusDTO.TotalMemory = (long)ramCounter.NextValue() ;
                        statusDTO.AgentFileDTOs = agentDirList.fileDTOs;
                        statusDTO.StagingFileDTOs = stagingContainerDirListingDTO1.fileDTOs;
                        statusDTO.RestoredListingDTO = restoredListingDTO.fileDTOs;

                        
                        ServiceBusUtils.postMsg2ControllerAsync("agent/status", RunTimeSettings.topicCustomerGuid, "status", JsonConvert.SerializeObject(statusDTO));
                        //look for files to backup
                        foreach (FileDTO fileDTO in stagingContainerDirListingDTO1.fileDTOs)
                        {

                            HTTPUtils.Instance.writeToLog(RunTimeSettings.topicCustomerGuid, "INFO", $"Queuing {fileDTO.FileName}");
                            // spawn workers for files
                            BackupWorker backupWorker = new BackupWorker(RunTimeSettings.topicCustomerGuid, azureBlobEndpoint, azureBlobContainerName, fileDTO.FileName, envPassPhrase);
                            ThreadUtils.addToQueue(backupWorker);
                        }

                        Thread.Sleep(60 * 1000 * RunTimeSettings.PollBaseTime);
                    }
                    catch (Exception ex)
                    {
                        HTTPUtils.Instance.writeToLog(RunTimeSettings.topicCustomerGuid, "ERROR", $"ListenerWorker Listening on topiccustomerGuid:{RunTimeSettings.topicCustomerGuid} {ex.Message}");
                        Console.WriteLine(ex.Message);
                    }

                }

            }
            finally
            {
                // Calling DisposeAsync on client types is required to ensure that network
                // resources and other unmanaged objects are properly cleaned up.
                //await processor.DisposeAsync();
                //await client.DisposeAsync();
            }
        }
        private static void MyFunction()
        {
            // Loop in here
        }
        public static void RunBackup(object s)
        {
            BackupWorker say = s as BackupWorker;
            say.StartAsync();
            //Console.WriteLine(say);
        }
        // handle received messages
        static async Task MessageHandler(ProcessMessageEventArgs args)
        {
            string body = args.Message.Body.ToString();
            //string customerGuid = topiccustomerGuid;
            try
            {
                GenericMessage genericMessage = JsonConvert.DeserializeObject<GenericMessage>(body);
                string msgType = genericMessage.msgType;

                dynamic msgObj = JsonConvert.DeserializeObject(genericMessage.msg);

                string passPhrase = "";

                HTTPUtils.Instance.writeToLog(RunTimeSettings.topicCustomerGuid, "TRACE", $"Agent Received msgType:{msgType} genericMessage.ssg:{genericMessage.msg.Length}" );

                //Console.WriteLine($"Received: {body}");
                if (msgType == "restoreFile")
                {
                    //Console.WriteLine("Starting Restore for:" + backupName);
                   // string backupName = ;
                    string inFileName = mountedDir + msgObj.backupName;
                    RestoreWorker restoreWorker = new RestoreWorker(RunTimeSettings.topicCustomerGuid, azureBlobRestoreContainerName, azureBlobEndpoint, azureBlobContainerName, inFileName, envPassPhrase);
                   // restoreWorker.StartAsync();
                    Task restoreWorkerTask = Task.Run(() => restoreWorker.StartAsync());

                }
                else if (msgType == "backupComplete")
                {
                    HTTPUtils.Instance.writeToLog(RunTimeSettings.topicCustomerGuid, "INFO", $"{msgObj.backupName} Backup Complete");
                    //   Utils.doDirListing(RunTimeSettings.topicCustomerGuid, mountedDir);
                }
                else if (msgType == "DirList")
                {
                    Utils.doDirListing(RunTimeSettings.topicCustomerGuid, mountedDir);
                }
                else if (msgType == "Error")
                {

                }
            }
            catch (Exception ex)
            {
               // Console.WriteLine(ex.Message);
                GenericMessage genericMessage2 = new GenericMessage();

                genericMessage2.msgType = "restoreComplete";
                genericMessage2.msg = ex.Message.ToString();
                genericMessage2.guid = RunTimeSettings.topicCustomerGuid;

                HTTPUtils.Instance.writeToLog(RunTimeSettings.topicCustomerGuid, "ERROR", "ListenerWorker:"+ex.Message.ToString());
            }
            // complete the message. message is deleted from the queue. 
            await args.CompleteMessageAsync(args.Message);
        }

        // handle any errors when receiving messages
        static Task ErrorHandler(ProcessErrorEventArgs args)
        {
            Console.WriteLine(args.Exception.ToString());
            return Task.CompletedTask;
        }
    }
}
