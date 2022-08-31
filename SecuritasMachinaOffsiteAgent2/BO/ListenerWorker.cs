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


        // static int RetentionDays = 45;

        // the client that owns the connection and can be used to create senders and receivers
        ServiceBusClient client;

        // the processor that reads and processes messages from the queue
        ServiceBusProcessor processor;
        // private int tLoopCount;



        internal async Task startAsync()
        {
            try
            {
                RunTimeSettings.RetentionDays = Int32.Parse(Environment.GetEnvironmentVariable("RetentionDays"));
            }
            catch (Exception ignore)
            {
                RunTimeSettings.RetentionDays = 45;

            }

            RunTimeSettings.topicCustomerGuid = Environment.GetEnvironmentVariable("customerGuid");
            RunTimeSettings.azureBlobEndpoint = Environment.GetEnvironmentVariable("azureBlobEndpoint");
            RunTimeSettings.envPassPhrase = Environment.GetEnvironmentVariable("passPhrase");
            RunTimeSettings.azureBlobContainerName = Environment.GetEnvironmentVariable("azureBlobContainerName");
            RunTimeSettings.azureBlobRestoreContainerName = Environment.GetEnvironmentVariable("azureBlobRestoreContainerName");
            if (RunTimeSettings.azureBlobRestoreContainerName == null)
                RunTimeSettings.azureBlobRestoreContainerName = "restored";

            Console.WriteLine();
            Console.WriteLine("Starting ListenerWorker azureBlobEndpoint:" + RunTimeSettings.azureBlobEndpoint);
            Console.WriteLine("azureBlobContainerName:" + RunTimeSettings.azureBlobContainerName);
            Console.WriteLine("azureBlobRestoreContainerName:" + RunTimeSettings.azureBlobRestoreContainerName);
            Console.WriteLine("customerGuid:" + RunTimeSettings.topicCustomerGuid);
            Console.WriteLine("RetentionDays:" + RunTimeSettings.RetentionDays);



            if (RunTimeSettings.envPassPhrase != null)
                Console.WriteLine("passPhrase Length:" + RunTimeSettings.envPassPhrase.Length);

            HTTPUtils.Instance.populateRuntime(RunTimeSettings.topicCustomerGuid);
            HTTPUtils.Instance.writeToLog(RunTimeSettings.topicCustomerGuid, "CONFIGINFO", $"azureBlobEndpoint length: {RunTimeSettings.azureBlobEndpoint.Length} azureBlobContainerName:{RunTimeSettings.azureBlobContainerName} azureBlobRestoreContainerName:{RunTimeSettings.azureBlobRestoreContainerName} RetentionDays:{RunTimeSettings.RetentionDays} passPhrase Length: {RunTimeSettings.envPassPhrase.Length}");
            if (RunTimeSettings.SBConnectionString == null || RunTimeSettings.SBConnectionString.Length == 0)
            {
                Console.WriteLine("!!! Unable to retrieve configuration !!!");
                Environment.Exit(1);
            }

            // Create the client object that will be used to create sender and receiver objects
            client = new ServiceBusClient(RunTimeSettings.SBConnectionString);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                RunTimeSettings.mountedDir = "c:\\temp\\";
            }

            DirectoryInfo directoryInfo = new DirectoryInfo(RunTimeSettings.mountedDir);
            FileInfo[] Files = directoryInfo.GetFiles("*"); //Getting Text files

            Console.WriteLine(RunTimeSettings.mountedDir + " has " + Files.Length + " files"); ;

            Console.Write("Testing writing to " + RunTimeSettings.mountedDir);
            try
            {
                using (StreamWriter sw = new StreamWriter(RunTimeSettings.mountedDir + "test.txt"))
                {
                    sw.WriteLine("test");
                }
                Console.WriteLine("...Success");
            }
            catch (Exception ex)
            {
                HTTPUtils.Instance.writeToLog(RunTimeSettings.topicCustomerGuid, "ERROR", "Error writing to " + RunTimeSettings.mountedDir + " - Ensure VM instance has full access to cloud storage");
                Console.WriteLine("...Error writing to " + RunTimeSettings.mountedDir + " - Ensure VM instance has full access to cloud storage");
            }
            Console.Write("Testing reading from " + RunTimeSettings.mountedDir);
            try
            {
                string line = "";
                using (StreamReader sr = new StreamReader(RunTimeSettings.mountedDir + "test.txt"))
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
                HTTPUtils.Instance.writeToLog(RunTimeSettings.topicCustomerGuid, "ERROR", "Error reading from  " + RunTimeSettings.mountedDir + " - Ensure VM instance has full access to cloud storage");
                Console.WriteLine("...Error reading from " + RunTimeSettings.mountedDir + " - Ensure VM instance has FULL access to Google cloud storage");
            }
            Console.Write("Testing delete at " + RunTimeSettings.mountedDir);
            try
            {
                File.Delete(RunTimeSettings.mountedDir + "test.txt");
                Console.WriteLine("...Success");
            }
            catch (Exception ex)
            {
                HTTPUtils.Instance.writeToLog(RunTimeSettings.topicCustomerGuid, "ERROR", "...Error deleting at " + RunTimeSettings.mountedDir + " - Ensure VM instance has FULL access to Google cloud storage");
                Console.WriteLine("...Error deleting at " + RunTimeSettings.mountedDir + " - Ensure VM instance has FULL access to Google cloud storage");
            }
            //TODO test dir listing of blob container
            BlobServiceClient? blobServiceClient = null;
            BlobContainerClient stagingContainerClient = null;
            try
            {
                Console.Write("Testing Azure Blob Endpoint at " + RunTimeSettings.azureBlobEndpoint + " " + RunTimeSettings.azureBlobContainerName);
                blobServiceClient = new BlobServiceClient(RunTimeSettings.azureBlobEndpoint);
                stagingContainerClient = blobServiceClient.GetBlobContainerClient(RunTimeSettings.azureBlobContainerName);

                await foreach (BlobItem blobItem in stagingContainerClient.GetBlobsAsync())
                {

                }
                Console.WriteLine("...Success");
            }
            catch (Exception ex)
            {

                HTTPUtils.Instance.writeToLog(RunTimeSettings.topicCustomerGuid, "ERROR", "...Error listing at " + RunTimeSettings.azureBlobEndpoint + " " + RunTimeSettings.azureBlobContainerName + " - Ensure VM instance has FULL access to Azure cloud storage " + ex.ToString());

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
                ArchiveWorker archiveWorker = new ArchiveWorker(RunTimeSettings.topicCustomerGuid, RunTimeSettings.mountedDir, RunTimeSettings.RetentionDays);

                Task task = Task.Run(() => archiveWorker.StartAsync());

                UpdateOffSiteBytesWorker updateOffSiteBytesWorker = new UpdateOffSiteBytesWorker(RunTimeSettings.topicCustomerGuid, RunTimeSettings.mountedDir, RunTimeSettings.RetentionDays);

                Task dailyTask = Task.Run(() => updateOffSiteBytesWorker.StartAsync());

                StatusWorker statusWorker = new StatusWorker();
                Task statusTask = Task.Run(() => statusWorker.StartAsync());
                ScanStageDirWorker scanStageDirWorker = new ScanStageDirWorker();
                Task scanStageDirWorkerTask = Task.Run(() => scanStageDirWorker.StartAsync());



            }
            finally
            {
                // Calling DisposeAsync on client types is required to ensure that network
                // resources and other unmanaged objects are properly cleaned up.
                //await processor.DisposeAsync();
                //await client.DisposeAsync();
            }
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

                HTTPUtils.Instance.writeToLog(RunTimeSettings.topicCustomerGuid, "TRACE", $"Agent Received msgType:{msgType} genericMessage.ssg:{genericMessage.msg.Length}");

                //Console.WriteLine($"Received: {body}");
                if (msgType == "restoreFile")
                {
                    //Console.WriteLine("Starting Restore for:" + backupName);
                    // string backupName = ;
                    string inFileName = RunTimeSettings.mountedDir + msgObj.backupName;
                    RestoreWorker restoreWorker = new RestoreWorker(RunTimeSettings.topicCustomerGuid, RunTimeSettings.azureBlobRestoreContainerName, RunTimeSettings.azureBlobEndpoint, RunTimeSettings.azureBlobContainerName, inFileName, RunTimeSettings.envPassPhrase);
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
                    Utils.doDirListing(RunTimeSettings.topicCustomerGuid, RunTimeSettings.mountedDir);
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

                HTTPUtils.Instance.writeToLog(RunTimeSettings.topicCustomerGuid, "ERROR", "ListenerWorker:" + ex.Message.ToString());
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
