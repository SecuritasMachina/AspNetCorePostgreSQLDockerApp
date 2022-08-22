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

namespace SecuritasMachinaOffsiteAgent.BO
{
    internal class ListenerWorker
    {

        static string? topiccustomerGuid = Environment.GetEnvironmentVariable("customerGuid");
        static string? azureBlobEndpoint = Environment.GetEnvironmentVariable("azureBlobEndpoint");
        static string? envPassPhrase = Environment.GetEnvironmentVariable("passPhrase");
        static string? azureBlobContainerName = Environment.GetEnvironmentVariable("azureBlobContainerName");
        static string? azureBlobRestoreContainerName = Environment.GetEnvironmentVariable("azureBlobRestoreContainerName");
        static int RetentionDays = 45;// Environment.GetEnvironmentVariable("RetentionDays");

        static string mountedDir = "/mnt/offsite/";
        // name of your Service Bus queue

        static string subscriptionName = "client";

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
            Console.WriteLine("Starting ListenerWorker azureBlobEndpoint:" + azureBlobEndpoint);
            Console.WriteLine("azureBlobContainerName:" + azureBlobContainerName);
            Console.WriteLine("azureBlobRestoreContainerName:" + azureBlobRestoreContainerName);
            Console.WriteLine("customerGuid:" + topiccustomerGuid);
            Console.WriteLine("RetentionDays:" + RetentionDays);

            if (envPassPhrase != null)
                Console.WriteLine("passPhrase Length:" + envPassPhrase.Length);

            HTTPUtils.Instance.populateRuntime(topiccustomerGuid);
            HTTPUtils.Instance.writeToLog(topiccustomerGuid, "INFO", $"..azureBlobEndpoint length: {azureBlobEndpoint.Length} azureBlobContainerName:{azureBlobContainerName} azureBlobRestoreContainerName:{azureBlobRestoreContainerName} RetentionDays:{RetentionDays} passPhrase Length: {envPassPhrase.Length}");
            if (RunTimeSettings.SBConnectionString == null || RunTimeSettings.SBConnectionString.Length == 0)
            {
                Console.WriteLine("!!! Unable to retrieve configuration !!!");
            }
            // Create the client object that will be used to create sender and receiver objects
            client = new ServiceBusClient(RunTimeSettings.SBConnectionString);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                mountedDir = "c:\\temp\\";
            }

            DirectoryInfo directoryInfo = new DirectoryInfo(mountedDir);
            FileInfo[] Files = directoryInfo.GetFiles("*"); //Getting Text files

            Console.WriteLine("Showing directory listing for " + mountedDir);
            foreach (FileInfo file in Files)
            {
                Console.WriteLine(file.Name);
            }
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
                HTTPUtils.Instance.writeToLog(topiccustomerGuid, "ERROR", "Error writing to " + mountedDir + " - Ensure VM instance has full access to cloud storage");
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
                HTTPUtils.Instance.writeToLog(topiccustomerGuid, "ERROR", "Error reading from  " + mountedDir + " - Ensure VM instance has full access to cloud storage");
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
                HTTPUtils.Instance.writeToLog(topiccustomerGuid, "ERROR", "...Error deleting at " + mountedDir + " - Ensure VM instance has FULL access to Google cloud storage");
                Console.WriteLine("...Error deleting at " + mountedDir + " - Ensure VM instance has FULL access to Google cloud storage");
            }
            //TODO test dir listing of blob container
            BlobServiceClient blobServiceClient;
            BlobContainerClient containerClient = null;
            try
            {
                Console.Write("Testing Azure Blob Endpoint at " + azureBlobEndpoint + " " + azureBlobContainerName);
                blobServiceClient = new BlobServiceClient(azureBlobEndpoint);
                containerClient = blobServiceClient.GetBlobContainerClient(azureBlobContainerName);

                await foreach (BlobItem blobItem in containerClient.GetBlobsAsync())
                {
                    Console.WriteLine("\t" + blobItem.Name);


                }
                Console.WriteLine("Success");
            }
            catch (Exception ex)
            {
                HTTPUtils.Instance.writeToLog(topiccustomerGuid, "ERROR", "...Error deleting at " + azureBlobEndpoint + " " + azureBlobContainerName + " - Ensure VM instance has FULL access to Google cloud storage");
                Console.WriteLine("...Error deleting at " + azureBlobEndpoint + " " + azureBlobContainerName + " - Ensure Blob endpoint and container name match Azure & Access Key URL");
            }



            try
            {
                // create a processor that we can use to process the messages

                processor = client.CreateProcessor(topiccustomerGuid, subscriptionName, new ServiceBusProcessorOptions());
                HTTPUtils.Instance.writeToLog(topiccustomerGuid, "INFO", $"Listening on {topiccustomerGuid}");
                Console.WriteLine("Listening");
                // add handler to process messages
                processor.ProcessMessageAsync += MessageHandler;

                // add handler to process any errors
                processor.ProcessErrorAsync += ErrorHandler;

                // start processing 
                await processor.StartProcessingAsync();
                //string oldGenericMessageJson = "";
                while (true)
                {
                    HTTPUtils.Instance.writeToLog(topiccustomerGuid, "TRACE", $"Scanning azureBlobEndpoint length:{azureBlobEndpoint.Length} azureBlobContainerName:{azureBlobContainerName}");

                    try
                    {
                        // Send dir listing to master
                        Utils.doDirListing(topiccustomerGuid, mountedDir);
                        
                        //ServiceBusUtils.postMsg2ControllerAsync(genericMessageJson);
                        Console.WriteLine("Scanning " + azureBlobEndpoint + " " + azureBlobContainerName);
                        DirListingDTO dirListingDTO1 = new DirListingDTO();
                        await foreach (BlobItem blobItem in containerClient.GetBlobsAsync())
                        {
                            FileDTO fileDTO = new FileDTO();
                            fileDTO.FileName = blobItem.Name;
                            fileDTO.length = blobItem.Properties.ContentLength;
                            fileDTO.FileDate = blobItem.Properties.LastModified.Value.ToUnixTimeMilliseconds();

                            dirListingDTO1.fileDTOs.Add(fileDTO);
                        }
                        HTTPUtils.Instance.sendAgentDir(topiccustomerGuid, dirListingDTO1);
                        ThreadPool.SetMaxThreads(3, 6);
                        using (var countdownEvent = new CountdownEvent(dirListingDTO1.fileDTOs.Count))
                        {
                            foreach (FileDTO fileDTO in dirListingDTO1.fileDTOs)
                            {
                                Console.WriteLine("\t" + fileDTO.FileName);
                                HTTPUtils.Instance.writeToLog(topiccustomerGuid, "INFO", $"Started {fileDTO.FileName}");

                                // spawn workers for files
                                BackupWorker backupWorker = new BackupWorker(topiccustomerGuid, azureBlobEndpoint, azureBlobContainerName, fileDTO.FileName, envPassPhrase);
                                ThreadPool.QueueUserWorkItem(x =>
                                {
                                    backupWorker.StartAsync();
                                    countdownEvent.Signal();
                                });
                            }
                            countdownEvent.Wait();
                        }
                        Thread.Sleep(60 * 1000);
                    }
                    catch (Exception ex)
                    {
                        HTTPUtils.Instance.writeToLog(topiccustomerGuid, "ERROR", $"Listening on topiccustomerGuid:{topiccustomerGuid}, subscriptionName:{subscriptionName} {ex.Message}");
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
            string customerGuid = topiccustomerGuid;
            try
            {
                GenericMessage genericMessage = JsonConvert.DeserializeObject<GenericMessage>(body);
                string msgType = genericMessage.msgType;

                dynamic msgObj = JsonConvert.DeserializeObject(genericMessage.msg);
                string backupName = msgObj.backupName;
                string passPhrase = "";


                
                //Console.WriteLine($"Received: {body}");
                if (msgType == "restoreFile")
                {
                    //Console.WriteLine("Starting Restore for:" + backupName);
                    
                    string inFileName = mountedDir + backupName;
                    RestoreWorker backupWorker = new RestoreWorker(topiccustomerGuid, azureBlobRestoreContainerName,azureBlobEndpoint, azureBlobContainerName, inFileName, envPassPhrase);
                    backupWorker.StartAsync();
                    
                }
                else if (msgType == "DirList")
                {
                    Utils.doDirListing(topiccustomerGuid, mountedDir);
                }
                else if (msgType == "Error")
                {

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                GenericMessage genericMessage2 = new GenericMessage();

                genericMessage2.msgType = "restoreComplete";
                genericMessage2.msg = ex.Message.ToString();
                genericMessage2.guid = customerGuid;

                HTTPUtils.Instance.writeToLog(customerGuid, "ERROR", ex.Message.ToString());
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
