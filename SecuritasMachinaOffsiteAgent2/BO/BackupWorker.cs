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
using Google.Cloud.Storage.V1;
using System.Reflection.Metadata;

namespace SecuritasMachinaOffsiteAgent.BO
{
    public class BackupWorker
    {
        private string _customerGuid;
        private string _azureBlobEndpoint;
        private string _BlobContainerName;
        private string _googleBucketName;
        private string _backupName;
        private string _passPhrase;
        //private BackgroundWorker worker;

        public override string ToString()
        {
            return this._backupName;
        }
        public BackupWorker(string customerGuid, string googleBucketName, string azureBlobEndpoint, string BlobContainerName, string backupName, string passPhrase)
        {

            this._customerGuid = customerGuid;
            this._azureBlobEndpoint = azureBlobEndpoint;
            this._BlobContainerName = BlobContainerName;
            this._googleBucketName = googleBucketName;
            this._backupName = backupName;
            this._passPhrase = passPhrase;

        }


        public async Task<object> StartAsync()
        {
            HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "INFO", $"Starting BackupWorker worker for {_backupName}");
            // Console.WriteLine("Starting BackupWorker for " + backupName);

            // Create a BlobServiceClient object which will be used to create a container client
            GenericMessage genericMessage = new GenericMessage();
            try
            {

                BlobServiceClient blobServiceClient = new BlobServiceClient(_azureBlobEndpoint);
                BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(_BlobContainerName);

                BlobClient azureBlobClient = containerClient.GetBlobClient(_backupName);
                BlobProperties props = azureBlobClient.GetProperties();
                string contentType = props.ContentType;
                if (!azureBlobClient.Exists())
                {
                    string msg = $"{_backupName} disappeared before it could be read, is there another agent running using azureBlobContainerName:{_BlobContainerName}";
                    HTTPUtils.Instance.writeToLogAsync(this._customerGuid, "INFO", msg);

                    return msg;
                }
                BlobProperties properties = await azureBlobClient.GetPropertiesAsync();
                StorageClient googleClient = StorageClient.Create();
                Stream inStream = azureBlobClient.OpenRead();

                //Store directly on fusepath
                string basebackupName = _backupName;
                string outFileName = _backupName + ".enc";
                
                outFileName = _backupName + "-" + DateTime.Now.ToString("yyyy-MM-dd_HH-MM") + ".enc";
                long startTimeStamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();

                new Utils().AES_EncryptStream(this._customerGuid, inStream, contentType, _googleBucketName, outFileName, properties.ContentLength, _backupName, _passPhrase);
                Google.Apis.Storage.v1.Data.Object outFileProperties = googleClient.GetObject(_googleBucketName, outFileName);

                
                azureBlobClient.Delete();
                FileDTO fileDTO = new FileDTO();
                fileDTO.FileName = _backupName;
                fileDTO.Status = "Success";
                string myJson = JsonConvert.SerializeObject(fileDTO);

                GenericMessage.msgTypes msgType = GenericMessage.msgTypes.backupComplete;
                genericMessage.msgType = msgType.ToString();
                genericMessage.msg = myJson;
                genericMessage.guid = _customerGuid;

                HTTPUtils.Instance.writeToLogAsync(this._customerGuid, "BACKUP-END", "Completed encryption, deleted : " + basebackupName);
                HTTPUtils.Instance.writeBackupHistory(this._customerGuid, basebackupName, _backupName, (long)outFileProperties.Size, startTimeStamp);
                string messageType = HttpUtility.UrlEncode(basebackupName + "-backupComplete");
                ServiceBusUtils.Instance.postMsg2ControllerAsync("agent/putCache", this._customerGuid, messageType, JsonConvert.SerializeObject(genericMessage));
                ThreadUtilsV2.Instance.deleteFromQueue(basebackupName);
            }
            catch (Exception ex)
            {
                HTTPUtils.Instance.writeToLogAsync(this._customerGuid, "ERROR", _backupName + " " + ex.ToString());
                Console.WriteLine();

            }
            //object genericMessage = null;
            return genericMessage.msg;
        }


    }
}
