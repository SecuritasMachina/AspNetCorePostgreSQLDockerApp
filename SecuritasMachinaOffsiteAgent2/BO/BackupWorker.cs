﻿using Azure.Storage.Blobs;
using Newtonsoft.Json;
using Common.DTO.V2;
using Common.Statics;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

using Common.Utils.Comm;

namespace SecuritasMachinaOffsiteAgent.BO
{
    public class BackupWorker
    {
        private string customerGuid;
        private string azureBlobEndpoint;
        private string BlobContainerName;
        private string backupName;
        private string passPhrase;
        //private BackgroundWorker worker;
        private int loopCount = 0;

        public BackupWorker(string customerGuid, string azureBlobEndpoint, string BlobContainerName, string backupName, string passPhrase)
        {
            Console.WriteLine("Starting BackupWorker for " + backupName);
            this.customerGuid = customerGuid;
            this.azureBlobEndpoint = azureBlobEndpoint;
            this.BlobContainerName = BlobContainerName;
            this.backupName = backupName;
            this.passPhrase = passPhrase;

        }


        public async Task<object> StartAsync()
        {
            //string genericMessageJson;
            // Create a BlobServiceClient object which will be used to create a container client
            GenericMessage genericMessage = new GenericMessage();
            try
            {
                loopCount++;
                //Console.WriteLine("Looking for: " + backupName);
                //TODO send post with status
                //report progress
                BlobServiceClient blobServiceClient = new BlobServiceClient(azureBlobEndpoint);
                BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(BlobContainerName);
                BlobClient blobClient = containerClient.GetBlobClient(backupName);
                Stream inStream = blobClient.OpenRead();

                //Store directly on fusepath
                string outFileName = "/mnt/offsite/" + backupName + ".enc";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    outFileName = "c:\\temp\\" + backupName + ".enc";
                }

                new Utils().AES_EncryptStream(inStream, outFileName, passPhrase);
                //Delete bacpac file on Azure 
                blobClient.Delete();
                FileDTO fileDTO = new FileDTO();
                fileDTO.FileName = backupName;
                fileDTO.Status = "Success";
                string myJson = JsonConvert.SerializeObject(fileDTO);
               
                GenericMessage.msgTypes msgType = GenericMessage.msgTypes.backupComplete;
                genericMessage.msgType = msgType.ToString();
                genericMessage.msg = myJson;
                genericMessage.guid = customerGuid;
               
                await ServiceBusUtils.postMsg2ControllerAsync(JsonConvert.SerializeObject(genericMessage));
                HTTPUtils.writeToLog(this.customerGuid, "INFO", "Completed encryption, deleted : " + backupName);


                Console.WriteLine("Completed encryption, deleted : " + backupName);
                //TODO send post with status
                //report progress
                //worker.CancelAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());

            }
            //object genericMessage = null;
            return genericMessage.msg;
        }


    }
}
