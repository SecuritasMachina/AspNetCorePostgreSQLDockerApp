using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Newtonsoft.Json;

using System.Runtime.InteropServices;
using Azure.Storage.Blobs.Models;
using Common.Statics;
using Common.Utils.Comm;
using Common.DTO.V2;
using Timer = System.Timers.Timer;
using Object = System.Object;

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
            RunTimeSettings.customerAuthKey = Environment.GetEnvironmentVariable("customerAgentAuthKey");
            RunTimeSettings.GoogleStorageBucketName= Environment.GetEnvironmentVariable("GoogleStorageBucketName");
            //RunTimeSettings.topicCustomerGuid = Environment.GetEnvironmentVariable("customerAgentAuthKey");
            RunTimeSettings.azureBlobEndpoint = Environment.GetEnvironmentVariable("azureBlobEndpoint");
            RunTimeSettings.envPassPhrase = Environment.GetEnvironmentVariable("passPhrase");
            RunTimeSettings.azureBlobContainerName = Environment.GetEnvironmentVariable("azureBlobContainerName");
            RunTimeSettings.azureBlobRestoreContainerName = Environment.GetEnvironmentVariable("azureBlobRestoreContainerName");
            if (RunTimeSettings.azureBlobRestoreContainerName == null)
                RunTimeSettings.azureBlobRestoreContainerName = "restored";

            Console.WriteLine();
            Console.WriteLine("Starting ListenerWorker azureBlobEndpoint:" + RunTimeSettings.azureBlobEndpoint);
            Console.WriteLine("AzureBlobContainerName:" + RunTimeSettings.azureBlobContainerName);
            Console.WriteLine("AzureBlobRestoreContainerName:" + RunTimeSettings.azureBlobRestoreContainerName);
            Console.WriteLine("GoogleStorageBucketName:" + RunTimeSettings.GoogleStorageBucketName);
            Console.WriteLine("Customer authkey:" + RunTimeSettings.customerAuthKey);
            Console.WriteLine("RetentionDays:" + RunTimeSettings.RetentionDays);



            if (RunTimeSettings.envPassPhrase != null)
                Console.WriteLine("passPhrase Length:" + RunTimeSettings.envPassPhrase.Length);

            HTTPUtils.Instance.populateRuntime(RunTimeSettings.customerAuthKey);
            HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAuthKey, "CONFIGINFO", $"azureBlobEndpoint length: {RunTimeSettings.azureBlobEndpoint.Length} azureBlobContainerName:{RunTimeSettings.azureBlobContainerName} azureBlobRestoreContainerName:{RunTimeSettings.azureBlobRestoreContainerName} RetentionDays:{RunTimeSettings.RetentionDays} passPhrase Length: {RunTimeSettings.envPassPhrase.Length}");
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
                HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAuthKey, "ERROR", "Error writing to " + RunTimeSettings.mountedDir + " - Ensure VM instance has full access to cloud storage");
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
                HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAuthKey, "ERROR", "Error reading from  " + RunTimeSettings.mountedDir + " - Ensure VM instance has full access to cloud storage");
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
                HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAuthKey, "ERROR", "...Error deleting at " + RunTimeSettings.mountedDir + " - Ensure VM instance has FULL access to Google cloud storage");
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

                HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAuthKey, "ERROR", "...Error listing at " + RunTimeSettings.azureBlobEndpoint + " " + RunTimeSettings.azureBlobContainerName + " - Ensure VM instance has FULL access to Azure cloud storage " + ex.ToString());

            }



            try
            {
                // create a processor that we can use to process the messages
                HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAuthKey, "INFO", $"Starting Listener on {RunTimeSettings.customerAuthKey}");
                processor = client.CreateProcessor(RunTimeSettings.serviceBusTopic, RunTimeSettings.clientSubscriptionName, new ServiceBusProcessorOptions());

                // Console.WriteLine("Listening");
                // add handler to process messages
                processor.ProcessMessageAsync += MessageHandler;

                // add handler to process any errors
                processor.ProcessErrorAsync += ErrorHandler;

                // start processing 
                await processor.StartProcessingAsync();
                HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAuthKey, "INFO", $"Listening on {RunTimeSettings.customerAuthKey}");
                //Start up background jobs

                Timer archiveWorkerTimer = new Timer();
                archiveWorkerTimer.Interval = 1000 * 60 * 60 * 6;              
                archiveWorkerTimer.Elapsed += archiveWorkerOnTimedEvent;              
                archiveWorkerTimer.AutoReset = true; archiveWorkerTimer.Enabled = true;
                HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAuthKey, "INFO", $"Started Retention Expired worker for {RunTimeSettings.mountedDir}");

                Timer statusWorkerTimer = new Timer();
                statusWorkerTimer.Interval = 1000 * 60 * 1;              
                statusWorkerTimer.Elapsed += statusWorkerOnTimedEvent;              
                statusWorkerTimer.AutoReset = true; statusWorkerTimer.Enabled = true;
                HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAuthKey, "INFO", $"Started Status worker");

                Timer offSiteWorkerTimer = new Timer();
                offSiteWorkerTimer.Interval = 1000 * 60 * 10;              
                offSiteWorkerTimer.Elapsed += offsiteWorkerOnTimedEvent;             
                offSiteWorkerTimer.AutoReset = true; offSiteWorkerTimer.Enabled = true;
                HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAuthKey, "INFO", $"Started OffSite worker for {RunTimeSettings.mountedDir}");

                Timer scanStageWorkerTimer = new Timer();
                scanStageWorkerTimer.Interval = 1000 * 60 * 1;              
                scanStageWorkerTimer.Elapsed += scanStageWorkerOnTimedEvent;            
                scanStageWorkerTimer.AutoReset = true; scanStageWorkerTimer.Enabled = true;
                HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAuthKey, "INFO", $"Started Scan worker for container {RunTimeSettings.azureBlobContainerName}");


                while (true)
                {
                    Thread.Sleep(1000 * 60);
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
        private void scanStageWorkerOnTimedEvent(Object source, System.Timers.ElapsedEventArgs e)
        {
            ScanStageDirWorker scanStageDirWorker = new ScanStageDirWorker();
            scanStageDirWorker.StartAsync();
        }
        private void offsiteWorkerOnTimedEvent(Object source, System.Timers.ElapsedEventArgs e)
        {

            UpdateOffSiteBytesWorker updateOffSiteBytesWorker = new UpdateOffSiteBytesWorker(RunTimeSettings.customerAuthKey, RunTimeSettings.mountedDir, RunTimeSettings.RetentionDays);

            updateOffSiteBytesWorker.StartAsync();
        }
        private void statusWorkerOnTimedEvent(Object source, System.Timers.ElapsedEventArgs e)
        {
            StatusWorker statusWorker = new StatusWorker();
            statusWorker.StartAsync();
        }
        private void archiveWorkerOnTimedEvent(Object source, System.Timers.ElapsedEventArgs e)
        {
            ArchiveWorker archiveWorker = new ArchiveWorker(RunTimeSettings.customerAuthKey, RunTimeSettings.mountedDir, RunTimeSettings.RetentionDays);

            archiveWorker.StartAsync();
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

                HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAuthKey, "TRACE", $"Agent Received msgType:{msgType} genericMessage.ssg:{genericMessage.msg.Length}");

                //Console.WriteLine($"Received: {body}");
                if (msgType == "restoreFile")
                {
                    //Console.WriteLine("Starting Restore for:" + backupName);
                    // string backupName = ;
                    string inFileName = RunTimeSettings.mountedDir + msgObj.backupName;
                    RestoreWorker restoreWorker = new RestoreWorker(RunTimeSettings.customerAuthKey, RunTimeSettings.azureBlobRestoreContainerName, RunTimeSettings.azureBlobEndpoint, RunTimeSettings.azureBlobContainerName, inFileName, RunTimeSettings.envPassPhrase);
                    // restoreWorker.StartAsync();
                    Task restoreWorkerTask = Task.Run(() => restoreWorker.StartAsync());

                }
                else if (msgType == "backupComplete")
                {
                    HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAuthKey, "INFO", $"{msgObj.backupName} Backup Complete");
                    //   Utils.doDirListing(RunTimeSettings.topicCustomerGuid, mountedDir);
                }
                else if (msgType == "DirList")
                {
                    Utils.doDirListing(RunTimeSettings.customerAuthKey, RunTimeSettings.mountedDir);
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
                genericMessage2.guid = RunTimeSettings.customerAuthKey;

                HTTPUtils.Instance.writeToLog(RunTimeSettings.customerAuthKey, "ERROR", "ListenerWorker:" + ex.Message.ToString());
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
