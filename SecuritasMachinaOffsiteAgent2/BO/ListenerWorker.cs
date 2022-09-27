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
using SecuritasMachinaOffsiteAgent.Utils.Comm.GoogleAPI;
using System.Text;
using Confluent.Kafka;
using Newtonsoft.Json.Linq;
using static Confluent.Kafka.ConfigPropertyNames;

namespace SecuritasMachinaOffsiteAgent.BO
{
    internal class ListenerWorker
    {


        // static int RetentionDays = 45;

        // the client that owns the connection and can be used to create senders and receivers
        //ServiceBusClient client;

        // the processor that reads and processes messages from the queue
        //ServiceBusProcessor processor;
        // private int tLoopCount;
        private ScanWorkerCrons scanWorkerCrons;


        internal async Task startAsync()
        {
            try
            {
                RunTimeSettings.RetentionDays = Int32.Parse(Environment.GetEnvironmentVariable("retentionDays"));
            }
            catch (Exception ignore)
            {
                RunTimeSettings.RetentionDays = 45;

            }
            if (RunTimeSettings.RetentionDays == 0) RunTimeSettings.RetentionDays = 1;
            try
            {
                RunTimeSettings.MaxThreads = Int32.Parse(Environment.GetEnvironmentVariable("maxThreads"));
                if (RunTimeSettings.MaxThreads > 20) RunTimeSettings.MaxThreads = 20;
            }
            catch (Exception ignore)
            {
                RunTimeSettings.MaxThreads = 3;

            }
            RunTimeSettings.DATAPATH = Environment.GetEnvironmentVariable("DATAPATH");
            if (String.IsNullOrEmpty(RunTimeSettings.DATAPATH))
                RunTimeSettings.DATAPATH = "/mnt/localhome";
            RunTimeSettings.customerAgentAuthKey = Environment.GetEnvironmentVariable("customerAgentAuthKey");
            RunTimeSettings.GITHUB_PAT_Token = Environment.GetEnvironmentVariable("GITHUB_PAT_Token");
            RunTimeSettings.GITHUB_OrgName = Environment.GetEnvironmentVariable("GITHUB_OrgName");

            RunTimeSettings.GoogleArchiveBucketName = Environment.GetEnvironmentVariable("googleStorageBucketName");
            //RunTimeSettings.topicCustomerGuid = Environment.GetEnvironmentVariable("customerAgentAuthKey");
            RunTimeSettings.azureBlobEndpoint = Environment.GetEnvironmentVariable("azureBlobEndpoint");
            RunTimeSettings.envPassPhrase = Environment.GetEnvironmentVariable("encryptionPassPhrase");

            RunTimeSettings.azureSourceBlobContainerName = Environment.GetEnvironmentVariable("azureSourceBlobContainerName");
            RunTimeSettings.azureBlobRestoreContainerName = Environment.GetEnvironmentVariable("azureBlobRestoreContainerName");
            if (String.IsNullOrEmpty(RunTimeSettings.azureBlobRestoreContainerName))
                RunTimeSettings.azureBlobRestoreContainerName = "restored";

            Console.WriteLine();
            Console.WriteLine("Customer authkey:" + RunTimeSettings.customerAgentAuthKey);
            Console.WriteLine("Starting ListenerWorker azureBlobEndpoint:" + RunTimeSettings.azureBlobEndpoint);
            Console.WriteLine("AzureBlobContainerName:" + RunTimeSettings.azureSourceBlobContainerName);
            Console.WriteLine("AzureBlobRestoreContainerName:" + RunTimeSettings.azureBlobRestoreContainerName);
            Console.WriteLine("GoogleArchiveBucketName:" + RunTimeSettings.GoogleArchiveBucketName);
            Console.WriteLine("GITHUB_PAT_Token:" + RunTimeSettings.GITHUB_PAT_Token);
            Console.WriteLine("GITHUB_OrgName:" + RunTimeSettings.GITHUB_OrgName);

            Console.WriteLine("RetentionDays:" + RunTimeSettings.RetentionDays);
            Console.WriteLine("DATAPATH:" + RunTimeSettings.DATAPATH);
            Console.WriteLine("MaxThreads:" + RunTimeSettings.MaxThreads);



            if (RunTimeSettings.envPassPhrase != null)
            {
                if (RunTimeSettings.envPassPhrase.Length < 32)
                {
                    RunTimeSettings.envPassPhrase = RunTimeSettings.envPassPhrase.PadRight(32, 'P');
                    Console.WriteLine("Warning, encryption passphrase was under 32 characters long, right padding");
                }
                Console.WriteLine("encryptionPassPhrase Length:" + RunTimeSettings.envPassPhrase.Length);
            }
            else
            {
                Console.WriteLine("encryptionPassPhrase is Empty");
            }

            HTTPUtils.Instance.populateRuntime(RunTimeSettings.customerAgentAuthKey);
            HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "CONFIGINFO", $"azureBlobEndpoint length: {RunTimeSettings.azureBlobEndpoint.Length} azureBlobContainerName:{RunTimeSettings.azureSourceBlobContainerName} azureBlobRestoreContainerName:{RunTimeSettings.azureBlobRestoreContainerName} GoogleStorageBucketName: {RunTimeSettings.GoogleArchiveBucketName} RetentionDays:{RunTimeSettings.RetentionDays} MaxThreads: {RunTimeSettings.MaxThreads} encryptionPassPhrase Length: {RunTimeSettings.envPassPhrase.Length}");
            if (RunTimeSettings.SBConnectionString == null || RunTimeSettings.SBConnectionString.Length == 0)
            {
                Console.WriteLine("!!! Unable to retrieve configuration !!!");
                Environment.Exit(1);
            }


            try
            {
                Console.Write("Testing writing to Google Storage Bucket Name: " + RunTimeSettings.GoogleArchiveBucketName);
                if (CloudUtils.Instance.testWrite(RunTimeSettings.GoogleArchiveBucketName))
                {
                    Console.WriteLine("...Success");
                }
                else
                {

                }
            }
            catch (Exception ex)
            {
                HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "ERROR", $"Error writing to {RunTimeSettings.GoogleArchiveBucketName} - Ensure service account has Storage Object Creator and Viewer to cloud bucket {ex.ToString()}");
                Console.WriteLine($"Error writing from {RunTimeSettings.GoogleArchiveBucketName} - Ensure service account has Storage Object Creator and Viewer to cloud bucket");
            }


            try
            {
                long tSize = 0;
                Console.Write("Testing reading from Google Storage Bucket Name: " + RunTimeSettings.GoogleArchiveBucketName);
                DirListingDTO dirListingDTO = CloudUtils.Instance.listFiles(RunTimeSettings.GoogleArchiveBucketName);
                if (CloudUtils.Instance.testRead(RunTimeSettings.GoogleArchiveBucketName))
                {
                    Console.WriteLine();
                    foreach (FileDTO fileDTO in dirListingDTO.fileDTOs)
                    {
                        tSize += fileDTO.length;
                    }
                }
                Console.WriteLine(RunTimeSettings.GoogleArchiveBucketName + $" has {dirListingDTO.fileDTOs.Count} files for a total of {Utils.BytesToString(tSize)} bytes");
            }
            catch (Exception ex)
            {
                HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "ERROR", $"Error reading from {RunTimeSettings.GoogleArchiveBucketName} - Ensure service account has Storage Object Creator and Viewer to cloud bucket {ex.ToString()}");
                Console.WriteLine($"Error reading from {RunTimeSettings.GoogleArchiveBucketName} - Ensure service account has Storage Object Creator and Viewer to cloud bucket");
            }


            Console.Write("Testing delete at Google Storage Bucket Name: " + RunTimeSettings.GoogleArchiveBucketName);
            try
            {
                CloudUtils.Instance.testDelete(RunTimeSettings.GoogleArchiveBucketName);

                Console.WriteLine("...Success");
            }
            catch (Exception ex)
            {
                HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "ERROR", $"Error deleting file at {RunTimeSettings.GoogleArchiveBucketName} - Ensure service account has Storage Object Creator and Viewer to cloud bucket {ex.ToString()}");
                Console.WriteLine($"Error deleting file at {RunTimeSettings.GoogleArchiveBucketName} - Ensure service account has Storage Object Creator and Viewer to cloud bucket");
            }
            //TODO test dir listing of blob container
            BlobServiceClient? blobServiceClient = null;
            BlobContainerClient stagingContainerClient = null;
            try
            {
                Console.Write("Testing Azure Blob Endpoint at " + RunTimeSettings.azureBlobEndpoint + " " + RunTimeSettings.azureSourceBlobContainerName);
                blobServiceClient = new BlobServiceClient(RunTimeSettings.azureBlobEndpoint);
                stagingContainerClient = blobServiceClient.GetBlobContainerClient(RunTimeSettings.azureSourceBlobContainerName);

                await foreach (BlobItem blobItem in stagingContainerClient.GetBlobsAsync())
                {

                }
                Console.WriteLine("...Success");
            }
            catch (Exception ex)
            {

                HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "ERROR", "...Error listing at " + RunTimeSettings.azureBlobEndpoint + " " + RunTimeSettings.azureSourceBlobContainerName + " - Ensure VM instance has FULL access to Azure cloud storage " + ex.ToString());

            }



            try
            {
                var totalCount = 0;
                CancellationTokenSource cts = new CancellationTokenSource();
                IConsumer<string, string> consumer;
                try
                {
                    ClientConfig config = new ClientConfig();
                    config.BootstrapServers = "172.29.27.15:9093";
                    var consumerConfig = new ConsumerConfig(config);
                    consumerConfig.GroupId = "dotnet-example-group-1";
                    consumerConfig.AutoOffsetReset = AutoOffsetReset.Earliest;
                    consumerConfig.EnableAutoCommit = false;
                    consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
                    consumer.Subscribe(RunTimeSettings.serviceBusTopic);
                    HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "INFO", $"Starting Listener on {RunTimeSettings.customerAgentAuthKey}");

                }
                catch (Exception ex) { Console.WriteLine($"Error Connecting to Service Bus {ex.ToString()}"); return; }

                HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "INFO", $"Listening on {RunTimeSettings.customerAgentAuthKey}");

                scanWorkerCrons = new ScanWorkerCrons();
                Timer scanCronHubWorkerTimer = new Timer();
                scanCronHubWorkerTimer.Interval = (1000);
                scanCronHubWorkerTimer.Elapsed += scanCronHubWorkerTimedEvent;
                scanCronHubWorkerTimer.AutoReset = true; scanCronHubWorkerTimer.Enabled = true;
                HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "INFO", $"Started Cron Job Worker");

                Console.WriteLine();
                Console.WriteLine("All Workers Ready");
                try
                {
                    while (true)
                    {

                        var cr = consumer.Consume(cts.Token);
                        totalCount += JObject.Parse(cr.Message.Value).Value<int>("count");
                        Console.WriteLine($"Consumed record with key {cr.Message.Key} and value {cr.Message.Value}, and updated total count to {totalCount}");

                        Thread.Sleep(10);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Ctrl-C was pressed.
                }
                finally
                {
                    consumer.Close();
                }


            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "ERROR", ex.ToString());
            }
            finally
            {
                // Calling DisposeAsync on client types is required to ensure that network
                // resources and other unmanaged objects are properly cleaned up.
                //await processor.DisposeAsync();
                //await client.DisposeAsync();
            }
        }

        private void scanCronHubWorkerTimedEvent(Object source, System.Timers.ElapsedEventArgs e)
        {
            Timer tmpTimer = (System.Timers.Timer)source;
            tmpTimer.Enabled = false;
            tmpTimer.AutoReset = false;
            tmpTimer.Interval = double.MaxValue;
            this.scanWorkerCrons.StartAsync();
            tmpTimer.Interval = (1000 * 10);
            tmpTimer.AutoReset = true;
            tmpTimer.Enabled = true;
        }


        // handle received messages
        static async Task MessageHandler(ProcessMessageEventArgs args)
        {
            string body = args.Message.Body.ToString();

            try
            {
                GenericMessage genericMessage = JsonConvert.DeserializeObject<GenericMessage>(body);
                string msgType = genericMessage.msgType;

                dynamic msgObj = JsonConvert.DeserializeObject(genericMessage.msg);

                string passPhrase = "";

                HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"Agent Received msgType:{msgType} genericMessage.ssg:{genericMessage.msg.Length}");

                //Console.WriteLine($"Received: {body}");
                if (msgType == "restoreFile")
                {

                    string inFileName = msgObj.backupName;
                    RestoreWorker restoreWorker = new RestoreWorker(RunTimeSettings.customerAgentAuthKey, RunTimeSettings.GoogleArchiveBucketName, RunTimeSettings.azureBlobRestoreContainerName, RunTimeSettings.azureBlobEndpoint, RunTimeSettings.azureSourceBlobContainerName, inFileName, RunTimeSettings.envPassPhrase);

                    Task restoreWorkerTask = Task.Run(() => restoreWorker.StartAsync());

                }
                else if (msgType == "backupComplete")
                {
                    HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "INFO", $"{msgObj.backupName} Backup Complete");
                    //   Utils.doDirListing(RunTimeSettings.topicCustomerGuid, mountedDir);
                }
                else if (msgType == "DirList")
                {
                    Utils.doDirListing(RunTimeSettings.customerAgentAuthKey, RunTimeSettings.GoogleArchiveBucketName);
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
                genericMessage2.guid = RunTimeSettings.customerAgentAuthKey;

                HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "ERROR", "ListenerWorker:" + ex.Message.ToString());
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
