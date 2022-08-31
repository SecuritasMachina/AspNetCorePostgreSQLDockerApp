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
using Azure.Storage.Blobs.Models;

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
        
        public override string ToString()
        {
            return this.backupName;
        }
        public BackupWorker(string customerGuid, string azureBlobEndpoint, string BlobContainerName, string backupName, string passPhrase)
        {
            
            this.customerGuid = customerGuid;
            this.azureBlobEndpoint = azureBlobEndpoint;
            this.BlobContainerName = BlobContainerName;
            this.backupName = backupName;
            this.passPhrase = passPhrase;

        }


        public async Task<object> StartAsync()
        {
            HTTPUtils.Instance.writeToLog(RunTimeSettings.topicCustomerGuid, "INFO", $"Starting BackupWorker worker for {backupName}");
           // Console.WriteLine("Starting BackupWorker for " + backupName);
            
            // Create a BlobServiceClient object which will be used to create a container client
            GenericMessage genericMessage = new GenericMessage();
            try
            {

                BlobServiceClient blobServiceClient = new BlobServiceClient(azureBlobEndpoint);
                BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(BlobContainerName);
                
                BlobClient blobClient = containerClient.GetBlobClient(backupName);
                if (!blobClient.Exists())
                {
                    string msg = $"{backupName} disappeared before it could be read, is there another agent running using azureBlobContainerName:{BlobContainerName}";
                    HTTPUtils.Instance.writeToLog(this.customerGuid, "INFO",msg);
                    
                    return msg;
                }
                BlobProperties properties = await blobClient.GetPropertiesAsync();

                Stream inStream = blobClient.OpenRead();

                //Store directly on fusepath
                string basebackupName = backupName;
                string outFileName = "/mnt/offsite/" + backupName + ".enc";
                int generationCount = 1;
                
                while (true)
                {
                    if (generationCount > 9999)
                    {
                        break;
                    }
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        outFileName = "c:\\temp\\" + backupName + ".enc";
                    }
                    else
                    {
                        outFileName = "/mnt/offsite/" + backupName + ".enc";
                    }
                    if (File.Exists(outFileName))
                    {
                        backupName = basebackupName +"-" +generationCount;
                        generationCount++;
                    }
                    else { break; }
                }
                long startTimeStamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();

                new Utils().AES_EncryptStream(this.customerGuid,inStream, outFileName, properties.ContentLength, backupName, passPhrase);
                FileInfo fi = new FileInfo(outFileName);
                //Check filelengths, make sure they match? or are reasonable
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

                //await ServiceBusUtils.postMsg2ControllerAsync(JsonConvert.SerializeObject(genericMessage));
                HTTPUtils.Instance.writeToLog(this.customerGuid, "BACKUP-END", "Completed encryption, deleted : " + basebackupName);
                HTTPUtils.Instance.writeBackupHistory(this.customerGuid, basebackupName,backupName, fi.Length, startTimeStamp);
                string messageType = HttpUtility.UrlEncode(basebackupName + "-backupComplete-" + this.customerGuid);
                ServiceBusUtils.postMsg2ControllerAsync("agent/putCache", this.customerGuid, messageType, JsonConvert.SerializeObject(genericMessage));
               // HTTPUtils.Instance.putCache(this.customerGuid, payload, JsonConvert.SerializeObject(genericMessage));
                //Console.WriteLine("Completed encryption, deleted : " + basebackupName + " payload:" + messageType);
                ThreadUtils.deleteFromQueue(basebackupName);
            }
            catch (Exception ex)
            {
                HTTPUtils.Instance.writeToLog(this.customerGuid, "ERROR", backupName+" "+ ex.ToString());
                Console.WriteLine();

            }
            //object genericMessage = null;
            return genericMessage.msg;
        }


    }
}
