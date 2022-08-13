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

namespace SecuritasMachinaOffsiteAgent.BO
{
    internal class ListenerWorker
    {
        string connectionString = "Endpoint=sb://securitasmachinaoffsiteclients.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=z0RU2MtEivO9JGSwhwLkRb8P6fg6v7A9MET5tNuljbQ=";
        //Environment.GetEnvironmentVariable("connectionString");
        static string topicName = Environment.GetEnvironmentVariable("customerGuid");
        static string azureBlobEndpoint = Environment.GetEnvironmentVariable("azureBlobEndpoint");
        static string envPassPhrase = Environment.GetEnvironmentVariable("passPhrase");
        static string azureBlobContainerName = Environment.GetEnvironmentVariable("azureBlobContainerName");
        static string mountedDir = "/mnt/offsite/";
        // name of your Service Bus queue

        static string subscriptionName = "client";

        // the client that owns the connection and can be used to create senders and receivers
        ServiceBusClient client;

        // the processor that reads and processes messages from the queue
        ServiceBusProcessor processor;
        internal async Task startAsync()
        {
            Console.WriteLine("Starting ListenerWorker azureBlobEndpoint:" + azureBlobEndpoint);
            Console.WriteLine("azureBlobContainerName:" + azureBlobContainerName);
            Console.WriteLine("customerGuid:" + topicName);
            if(envPassPhrase!=null)
                Console.WriteLine("passPhrase Length:" + envPassPhrase.Length);
            // Create the client object that will be used to create sender and receiver objects
            client = new ServiceBusClient(connectionString);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                mountedDir = "c:\\temp\\";
            }

            DirectoryInfo d = new DirectoryInfo(mountedDir);
            FileInfo[] Files = d.GetFiles("*"); //Getting Text files
            string str = "";
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
                Console.WriteLine("...Error reading from " + mountedDir + " - Ensure VM instance has full access to cloud storage");
            }
            Console.Write("Testing delete at " + mountedDir);
            try
            {
                File.Delete(mountedDir + "test.txt");
                Console.WriteLine("...Success");
            }
            catch (Exception ex)
            {
                Console.WriteLine("...Error deleting at " + mountedDir + " - Ensure VM instance has full access to cloud storage");
            }
            //TODO test dir listing of blob container
            Console.Write("Testing Azure Blob Endpoint at " + azureBlobEndpoint+" "+ azureBlobContainerName);
            BlobServiceClient blobServiceClient = new BlobServiceClient(azureBlobEndpoint);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(azureBlobContainerName);

            await foreach (BlobItem blobItem in containerClient.GetBlobsAsync())
            {
                Console.WriteLine("\t" + blobItem.Name);
                //TODO spawn workers for files
                BackupWorker backupWorker = new BackupWorker(azureBlobEndpoint, azureBlobContainerName, blobItem.Name, envPassPhrase);
                
            }
            //TODO Send dir listing to master
            string[] fileArray = Directory.GetFiles(mountedDir);
            // create a processor that we can use to process the messages

            processor = client.CreateProcessor(topicName, subscriptionName, new ServiceBusProcessorOptions());


            try
            {
                Console.WriteLine("Listening");
                // add handler to process messages
                processor.ProcessMessageAsync += MessageHandler;

                // add handler to process any errors
                processor.ProcessErrorAsync += ErrorHandler;

                // start processing 
                await processor.StartProcessingAsync();
                while (true)
                {
                    Thread.Sleep(2000);
                    //Console.WriteLine("Listening");
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


        // handle received messages
        static async Task MessageHandler(ProcessMessageEventArgs args)
        {
            string body = args.Message.Body.ToString();
            Console.WriteLine($"Received: {body}");
            try
            {
                dynamic stuff = JsonConvert.DeserializeObject(body);
                string msgType = stuff.msgType;
                string customerGUID = stuff.customerGUID;
                // string azureBlobEndpoint = azureBlobEndpoint;
                string backupName = stuff.backupName;
                string passPhrase = stuff.passPhrase;
                string status = stuff.status;


                string BlobContainerName = stuff.BlobContainerName;

                string errorMsg = stuff.errormsg;
                int RetentionDays = stuff.RetentionDays;
                Console.WriteLine(msgType);
                if (msgType == "restoreFile")
                {
                    string inFileName = mountedDir + backupName + ".enc";

                    FileStream inStream = new FileStream(inFileName, FileMode.Open);
                    BlobServiceClient blobServiceClient = new BlobServiceClient(azureBlobEndpoint);
                    BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(BlobContainerName);
                    //BlobClient blobClient = containerClient.GetBlobClient(backupName);

                    var blockBlobClient = containerClient.GetBlobClient(backupName);
                    var outStream = await blockBlobClient.OpenWriteAsync(true);
                    if (envPassPhrase != null)
                        passPhrase = envPassPhrase;
                    new Utils().AES_DecryptStream(inStream, outStream, passPhrase);

                }
                else if (msgType == "backupStarted")
                {
                    if (envPassPhrase != null)
                        passPhrase = envPassPhrase;
                    BackupWorker backupWorker = new BackupWorker(azureBlobEndpoint, BlobContainerName, backupName, passPhrase);


                }
                else if (msgType == "Error")
                {
                    Console.WriteLine("error " + errorMsg);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
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
