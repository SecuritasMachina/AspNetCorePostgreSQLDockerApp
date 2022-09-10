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
using Google.Cloud.Storage.V1;
using System.ComponentModel.DataAnnotations;

namespace SecuritasMachinaOffsiteAgent.BO
{
    public class RestoreWorker
    {
        private string authToken;
        private string azureBlobEndpoint;

        private string _restoreName;
        private string _passPhrase;
        private string _azureBlobRestoreContainerName;
        private string _googleBucketName;
        //private BackgroundWorker worker;
        //private int loopCount = 0;

        public RestoreWorker(string customerGuid, string pGoogleBucketName,string azureBlobRestoreContainerName, string azureBlobEndpoint, string BlobContainerName, string restoreName, string passPhrase)
        {
            Console.WriteLine("Starting RestoreWorker for " + restoreName);
            this.authToken = customerGuid;
            this.azureBlobEndpoint = azureBlobEndpoint;
            //this.BlobContainerName = BlobContainerName;

            this._restoreName = restoreName;
            this._passPhrase = passPhrase;
            this._googleBucketName = pGoogleBucketName;
            this._azureBlobRestoreContainerName = azureBlobRestoreContainerName;

        }


        public async Task<object> StartAsync()
        {
            //string genericMessageJson;
            // Create a BlobServiceClient object which will be used to create a container client
            GenericMessage genericMessage = new GenericMessage();
            try
            {
                
                HTTPUtils.Instance.writeToLog(authToken, "RESTORE-START", "Starting Restore for:" + _restoreName);
                FileDTO fileDTO = new FileDTO();
                fileDTO.FileName = _restoreName;
                fileDTO.Status = "InProgress";

                string myJson = JsonConvert.SerializeObject(fileDTO);
                GenericMessage genericMessage2 = new GenericMessage();
                StorageClient googleClient = StorageClient.Create();
               
                //FileStream inStream = new FileStream(restoreName, FileMode.Open);
                BlobServiceClient blobServiceClient = new BlobServiceClient(azureBlobEndpoint);

                BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(_azureBlobRestoreContainerName);


                int generationCount = 1;
                string restoreFileName = _restoreName.Replace(".enc", "");
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

                        HTTPUtils.Instance.writeToLog(authToken, "INFO", $"{restoreFileName} exists in {_azureBlobRestoreContainerName}, retrying as {baseRestoreName + "-" + generationCount}");
                        restoreFileName = baseRestoreName + "-" + generationCount;
                        blockBlobClient = containerClient.GetBlobClient(restoreFileName);
                        generationCount++;
                    }
                    else
                    {
                        HTTPUtils.Instance.writeToLog(authToken, "INFO", $"Writing as {restoreFileName} in {_azureBlobRestoreContainerName}");
                        break;
                    }
                }


                Google.Apis.Storage.v1.Data.Object googleFile = googleClient.GetObject(_googleBucketName, _restoreName);
                string contentType = googleFile.ContentType;
                long? googleFileLength = (long?)googleFile.Size;
                var outStream = await blockBlobClient.OpenWriteAsync(true);

                new Utils().AES_DecryptStream(authToken, this._googleBucketName, _restoreName, outStream, (long)googleFileLength, _restoreName, _passPhrase);
                
                
                fileDTO.FileName = _restoreName;
                fileDTO.Status = "Success";
                

                genericMessage2 = new GenericMessage();

                genericMessage2.msgType = "restoreComplete";
                genericMessage2.msg = JsonConvert.SerializeObject(fileDTO);
                genericMessage2.guid = authToken;
                

                HTTPUtils.Instance.putCache(authToken, _restoreName + "-" + genericMessage2.msgType, JsonConvert.SerializeObject(genericMessage2));
                HTTPUtils.Instance.writeToLog(authToken, "RESTORE-END", $"Restore Completed for {_restoreName} restored as {restoreFileName}");

            }
            catch (Exception ex)
            {
                HTTPUtils.Instance.writeToLog(this.authToken, "ERROR", _restoreName + " " + ex.ToString());
                // Console.WriteLine();

            }
            //object genericMessage = null;
            return genericMessage.msg;
        }


    }
}
