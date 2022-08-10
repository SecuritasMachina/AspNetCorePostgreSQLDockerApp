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
using AspNetCorePostgreSQLDockerApp.APIs;
using System.Runtime.InteropServices;

namespace BackupCoordinator
{
    internal class ListenerWorker
    {
        //string connectionString = "Endpoint=sb://securitasmachina.servicebus.windows.net/;SharedAccessKeyName=sbpolicy1;SharedAccessKey=hGQMBNMvG1djKydyi1hCJmtDJN/mgtegm/9rAaDMEGg=;EntityPath=offsitebackup";
        string connectionString = "Endpoint=sb://securitasmachina.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=IOC5nIXihyX3eKDzmvzzH20PdUnr/hyt3wydgtNe5z8=";

        // name of your Service Bus queue
        string queueName = "offsitebackup";

        // the client that owns the connection and can be used to create senders and receivers
        ServiceBusClient client;

        // the processor that reads and processes messages from the queue
        ServiceBusProcessor processor;
        internal async Task startAsync()
        {
            Console.WriteLine("Starting ListenerWorker");
            // Create the client object that will be used to create sender and receiver objects
            client = new ServiceBusClient(connectionString);

            // create a processor that we can use to process the messages
            processor = client.CreateProcessor(queueName, new ServiceBusProcessorOptions());

            try
            {
                // add handler to process messages
                processor.ProcessMessageAsync += MessageHandler;

                // add handler to process any errors
                processor.ProcessErrorAsync += ErrorHandler;

                // start processing 
                await processor.StartProcessingAsync();
                while (true)
                {
                    Thread.Sleep(2000);
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
                string? msgType = stuff.msgType;
                string? customerGUID = stuff.customerGUID;
                string? azureBlobEndpoint = stuff.azureBlobEndpoint;
                string? backupName = stuff.backupName;
                string? status = stuff.status;
                
                //string StorageKey = stuff.StorageKey;
                string? BlobContainerName = stuff.BlobContainerName;
                
                string? errorMsg = stuff.errormsg;
                int RetentionDays = stuff.RetentionDays;
                Console.WriteLine(msgType);
                if (msgType == "backupstart")
                {
                    
                }
                else if (msgType== "restoreFile")
                {
                    string inFileName = "/tmp/" + backupName + ".enc";
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        inFileName = "c:\\temp\\" + backupName + ".enc";
                    }
                    FileStream inStream = new FileStream(inFileName, FileMode.Open);
                    BlobServiceClient blobServiceClient = new BlobServiceClient(azureBlobEndpoint);
                    BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(BlobContainerName);
                    BlobClient blobClient = containerClient.GetBlobClient(backupName);
                    
                    var blockBlobClient = containerClient.GetBlobClient(backupName);
                    var outStream = await blockBlobClient.OpenWriteAsync(true);
                    new Utils().AES_DecryptStream(inStream, outStream, "password");

                }
                else if (msgType == "backupfinished")
                {
                    // Create a BlobServiceClient object which will be used to create a container client
                    BlobServiceClient blobServiceClient = new BlobServiceClient(azureBlobEndpoint);
                    BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(BlobContainerName);
                    BlobClient blobClient = containerClient.GetBlobClient(backupName);
                    Stream inStream = blobClient.OpenRead();
                    
                    //Store directly on fusepath
                    string outFileName = "/tmp/" + backupName + ".enc";
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        outFileName = "c:\\temp\\" + backupName + ".enc";
                    }
                    new Utils().AES_EncryptStream(inStream, outFileName, "password");
                    //Delete bacpac file on Azure 
                    blobClient.Delete();
                    
                }
                else if (msgType == "Error")
                {
                    Console.WriteLine("error "+ errorMsg);
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
