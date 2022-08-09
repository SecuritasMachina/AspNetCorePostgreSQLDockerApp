using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Azure.Messaging.ServiceBus;
using Newtonsoft.Json;

namespace BackupCoordinator
{
    internal class ListenerWorker
    {
        string connectionString = "Endpoint=sb://securitasmachina.servicebus.windows.net/;SharedAccessKeyName=sbpolicy1;SharedAccessKey=hGQMBNMvG1djKydyi1hCJmtDJN/mgtegm/9rAaDMEGg=;EntityPath=offsitebackup";

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

        // name of your Service Bus queue
        string queueName = "offsitebackup";

        // the client that owns the connection and can be used to create senders and receivers
        ServiceBusClient client;

        // the processor that reads and processes messages from the queue
        ServiceBusProcessor processor;
        // handle received messages
        static async Task MessageHandler(ProcessMessageEventArgs args)
        {
            string body = args.Message.Body.ToString();
            Console.WriteLine($"Received: {body}");
            try
            {
                dynamic stuff = JsonConvert.DeserializeObject(body);
            
                string? customerGUID = stuff.customerGUID;
                string? backupName = stuff.backupName;
                string? status = stuff.status;
                string? StorageAccountName = stuff.StorageAccountName;
                string? BlobStorageEndpoint = stuff.BlobStorageEndpoint;
                //string StorageKey = stuff.StorageKey;
                string? BlobContainerName = stuff.BlobContainerName;
                string? BlobSASToken = stuff.BlobSASToken;
                int RetentionDays = stuff.RetentionDays;

                if (BlobSASToken != null)
                {
                    String command = $"azcopy copy \"{BlobStorageEndpoint}{BlobContainerName}/{backupName}?{BlobSASToken}\" \"/tmp/{backupName}\"";
                    Console.WriteLine(command);
                    ProcessStartInfo cmdsi = new ProcessStartInfo("/bin/bash");
                    cmdsi.Arguments = command;
                    Process cmd = Process.Start(cmdsi);
                    cmd.WaitForExit();
                }
            }catch(Exception ex)
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
