using Azure.Storage.Blobs;
using Newtonsoft.Json;
using Common.DTO.V2;
using Common.Statics;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

using Common.Utils.Comm;
using System.Web;

namespace SecuritasMachinaOffsiteAgent.BO
{
    public class RestoreWorker
    {
        private string customerGuid;
        private string azureBlobEndpoint;

        private string restoreName;
        private string passPhrase;
        private string azureBlobRestoreContainerName;
        //private BackgroundWorker worker;
        //private int loopCount = 0;

        public RestoreWorker(string customerGuid,string azureBlobRestoreContainerName, string azureBlobEndpoint, string BlobContainerName,string restoreName, string passPhrase)
        {
            Console.WriteLine("Starting RestoreWorker for " + restoreName);
            this.customerGuid = customerGuid;
            this.azureBlobEndpoint = azureBlobEndpoint;
            //this.BlobContainerName = BlobContainerName;
           
            this.restoreName = restoreName;
            this.passPhrase = passPhrase;
            this.azureBlobRestoreContainerName = azureBlobRestoreContainerName;

        }


        public async Task<object> StartAsync()
        {
            //string genericMessageJson;
            // Create a BlobServiceClient object which will be used to create a container client
            GenericMessage genericMessage = new GenericMessage();
            try
            {
                string baseFilename = Path.GetFileName(restoreName);
                //string baseFilename = Path.GetFileName(restoreName);
                HTTPUtils.Instance.writeToLog(customerGuid, "RESTORE-START", "Starting Restore for:" + baseFilename);
                FileDTO fileDTO = new FileDTO();
                fileDTO.FileName = restoreName;
                fileDTO.Status = "InProgress";
                
                string myJson = JsonConvert.SerializeObject(fileDTO);
                GenericMessage genericMessage2 = new GenericMessage();
                
                FileStream inStream = new FileStream(restoreName, FileMode.Open);
                BlobServiceClient blobServiceClient = new BlobServiceClient(azureBlobEndpoint);
               
                BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(azureBlobRestoreContainerName);

                
                int generationCount = 1;
                string restoreFileName = baseFilename.Replace(".enc", "");
                string baseRestoreName = restoreFileName;
                var blockBlobClient = containerClient.GetBlobClient(restoreFileName);
                while (true)
                {
                    if (generationCount > 9999)
                    {
                        break;
                    }
                    if (blockBlobClient.Exists())
                    {
                        
                        HTTPUtils.Instance.writeToLog(customerGuid, "INFO", $"{restoreFileName} exists in {azureBlobRestoreContainerName}, retrying as {baseRestoreName + "-" + generationCount}");
                        restoreFileName = baseRestoreName + "-" + generationCount;
                        blockBlobClient = containerClient.GetBlobClient(restoreFileName);
                        generationCount++;
                    }
                    else {
                        HTTPUtils.Instance.writeToLog(customerGuid, "INFO", $"Writing as {restoreFileName} in {azureBlobRestoreContainerName}");
                        break; }
                }
                

               
                var outStream = await blockBlobClient.OpenWriteAsync(true);

                new Utils().AES_DecryptStream(customerGuid, inStream, outStream, new FileInfo(restoreName).Length, baseFilename, passPhrase);
                //FileDTO fileDTO = new FileDTO();
                fileDTO.FileName = baseFilename;
                fileDTO.Status = "Success";
                //fileDTO.length = outStream.Length;
                
                genericMessage2 = new GenericMessage();

                genericMessage2.msgType = "restoreComplete";
                genericMessage2.msg = JsonConvert.SerializeObject(fileDTO);
                genericMessage2.guid = customerGuid;


                HTTPUtils.Instance.putCache(baseFilename + "-" + genericMessage2.msgType + "-" + customerGuid, genericMessage2.msgType,JsonConvert.SerializeObject(genericMessage2));
                HTTPUtils.Instance.writeToLog(customerGuid, "RESTORE-END", $"Restore Completed for {baseFilename} restored as {restoreFileName}");

            }
            catch (Exception ex)
            {
                HTTPUtils.Instance.writeToLog(this.customerGuid, "ERROR", restoreName + " "+ ex.ToString());
               // Console.WriteLine();

            }
            //object genericMessage = null;
            return genericMessage.msg;
        }


    }
}
