using Azure;
using Azure.Storage.Blobs.Models;
using Common.DTO.V2;
using Common.Utils.Comm;
using Google.Apis.Download;
using Google.Apis.Upload;
using Google.Cloud.Storage.V1;
using Newtonsoft.Json;
using SecuritasMachinaOffsiteAgent.DTO.V2;
using SecuritasMachinaOffsiteAgent.Utils.Comm.GoogleAPI;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;
using static System.Net.WebRequestMethods;

namespace SecuritasMachinaOffsiteAgent.BO
{
    public class Utils
    {
        public static String BytesToString(long byteCount)
        {
            string[] suf = { "B", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB" }; //Longs run out around EB
            if (byteCount == 0)
                return "0" + suf[0];
            long bytes = Math.Abs(byteCount);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num).ToString() + suf[place];
        }
        public static DirListingDTO doDirListing(string topiccustomerGuid, string pBucketName)
        {
            // DirectoryInfo directoryInfo = new DirectoryInfo(pBucketName);
            DirListingDTO ret = CloudUtils.Instance.listFiles(pBucketName);

            string myJson = JsonConvert.SerializeObject(ret);
            GenericMessage genericMessage = new GenericMessage();
            GenericMessage.msgTypes msgType = GenericMessage.msgTypes.dirListing;
            genericMessage.msgType = msgType.ToString();
            genericMessage.msg = myJson;
            genericMessage.guid = topiccustomerGuid;
            //string genericMessageJson = JsonConvert.SerializeObject(genericMessage);
            //TODO Don't send if same

            //
            return ret;

        }
        internal static async Task<DirListingDTO> doDirListingAsync(AsyncPageable<BlobItem> asyncPageable)
        {
            DirListingDTO ret = new DirListingDTO();
            await foreach (BlobItem blobItem in asyncPageable)
            {
                FileDTO fileDTO = new FileDTO();
                fileDTO.FileName = blobItem.Name;
                fileDTO.length = (long)blobItem.Properties.ContentLength;
                fileDTO.contentType = blobItem.Properties.ContentType;
                fileDTO.FileDate = blobItem.Properties.LastModified.Value.ToUnixTimeMilliseconds();

                ret.fileDTOs.Add(fileDTO);
            }
            return ret;
        }
        public static DirListingDTO doDirListingOld(string topiccustomerGuid, string pMountedDir)
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(pMountedDir);
            List<FileInfo> Files2 = directoryInfo.GetFiles("*")
                                                              .OrderByDescending(f => f.LastWriteTime)
                                                              .ToList();

            DirListingDTO ret = new DirListingDTO();

            foreach (FileInfo file in Files2)
            {
                FileDTO fDTO = new FileDTO();
                fDTO.FileName = file.Name;
                fDTO.length = file.Length;
                long unixTimeMilliseconds = new DateTimeOffset(file.LastWriteTime).ToUnixTimeMilliseconds();
                fDTO.FileDate = unixTimeMilliseconds;
                ret.fileDTOs.Add(fDTO);

            }
            string myJson = JsonConvert.SerializeObject(ret);
            GenericMessage genericMessage = new GenericMessage();
            GenericMessage.msgTypes msgType = GenericMessage.msgTypes.dirListing;
            genericMessage.msgType = msgType.ToString();
            genericMessage.msg = myJson;
            genericMessage.guid = topiccustomerGuid;
            //string genericMessageJson = JsonConvert.SerializeObject(genericMessage);
            //TODO Don't send if same

            //
            return ret;

        }
        public static byte[] GenerateRandomSalt()
        {
            //Source: http://www.dotnetperls.com/rngcryptoserviceprovider
            byte[] data = new byte[32];

            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                // Ten iterations.
                for (int i = 0; i < 10; i++)
                {
                    // Fill buffer.
                    rng.GetBytes(data);
                }
            }
            return data;
        }
        public void AES_EncryptStream(string pCustomerGuid, Stream pInStream, string pContentType, string googleStorageBucketName, string pOutputFileName, long pContentLength, string pBaseFileName, string password)
        {
            //generate random salt
            try
            {
                password = password.Substring(0, 32);
                StorageClient googleClient = StorageClient.Create();
                byte[] salt = GenerateRandomSalt();

                //convert password string to byte arrray
                //  byte[] passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);

                // Create an Aes object
                // with the specified key and IV.
                using (Aes aesAlg = Aes.Create())
                {
                    //aesAlg.Key = passwordBytes;
                    //  aesAlg.IV = Encoding.ASCII.GetBytes("01234A678C123456");

                    // Create an encryptor to perform the stream transform.
                    // ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                    // Create the streams used for encryption.
                    using (pInStream)
                    {

                        //Write all data to the stream.
                        UploadObjectOptions uploadObjectOptions = new UploadObjectOptions();
                        EncryptionKey encryptionKey = EncryptionKey.Create(Encoding.UTF8.GetBytes(password));
                        uploadObjectOptions.EncryptionKey = encryptionKey;
                        DateTime start = DateTime.Now;
                        var progress = new Progress<IUploadProgress>(
                             p =>
                             {
                                 DateTime end = DateTime.Now;
                                 var result = end.Subtract(start).TotalMinutes;
                                 if (result > 1)
                                 {
                                     int percentComplete = (int)Math.Round((double)(100 * p.BytesSent) / (double)pContentLength);
                                     HTTPUtils.Instance.writeToLog(pCustomerGuid, "BACKUP-UPDATE", $"Backup {pBaseFileName} is {percentComplete}% complete");
                                     start = DateTime.Now;
                                 }
                             }
                        );
                        googleClient.UploadObject(googleStorageBucketName, pOutputFileName, pContentType, pInStream, uploadObjectOptions, progress);


                    }


                }
            }
            catch (Azure.RequestFailedException ex)
            {
                if (ex.ToString().ToLower().IndexOf("BlobNotFound".ToLower()) > 0)
                {
                    string msg = $"{pBaseFileName} disappeared before it could be read, is there another agent running?";
                    HTTPUtils.Instance.writeToLog(pCustomerGuid, "INFO", msg);
                    return;
                }
                else
                {
                    HTTPUtils.Instance.writeToLog(pCustomerGuid, "ERROR", pBaseFileName + " " + ex.ToString());
                }

            }
            catch (Exception ex)
            {


                HTTPUtils.Instance.writeToLog(pCustomerGuid, "ERROR", pBaseFileName + " " + ex.ToString());
                throw (ex);
            }

        }
        public void AES_DecryptStream(string pCustomerGuid, string pGoogleBucketName, string pFileToRestore, Stream pOutStream, long pContentLength, string pInfileName, string password)
        {

            using (Aes aesAlg = Aes.Create())
            {
                //byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
                password = password.Substring(0, 32);
                //aesAlg.Key = passwordBytes;
                //aesAlg.IV = Encoding.ASCII.GetBytes("01234A678C123456");
                StorageClient googleClient = StorageClient.Create();
                // Create an encryptor to perform the stream transform.
                //ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                // googleFile.
                //googleFile.
                // Create the streams used for encryption.
                DownloadObjectOptions downloadObjectOptions = new DownloadObjectOptions();
                EncryptionKey encryptionKey = EncryptionKey.Create(Encoding.UTF8.GetBytes(password));
                downloadObjectOptions.EncryptionKey = encryptionKey;
                DateTime start = DateTime.Now;
                var progress = new Progress<IDownloadProgress>(
                   p =>
                   {
                       DateTime end = DateTime.Now;
                       var result = end.Subtract(start).TotalMinutes;
                       if (result > 1)
                       {
                           int percentComplete = (int)Math.Round((double)(100 * p.BytesDownloaded) / (double)pContentLength);
                           HTTPUtils.Instance.writeToLog(pCustomerGuid, "BACKUP-UPDATE", $"Backup {pFileToRestore} is {percentComplete}% complete");
                           start = DateTime.Now;
                       }
                   }
                );

                googleClient.DownloadObject(pGoogleBucketName, pFileToRestore, pOutStream, downloadObjectOptions, progress);

            }
        }


        internal static void UpdateOffsiteBytes(string customerGuid, string pGoogleBucketName)
        {
            HTTPUtils.Instance.writeToLog(customerGuid, "TRACE", $"Scanning Google Storage Bucket {pGoogleBucketName}");
            DirListingDTO dirlistingDTO = CloudUtils.Instance.listFiles(pGoogleBucketName);

            long? tsize = 0;
            try
            {
                foreach (FileDTO file in dirlistingDTO.fileDTOs)
                {
                    tsize += file.length;

                }
                HTTPUtils.Instance.writeToLog(customerGuid, "TOTALOFFSITEBYTES", $"{tsize}");
                HTTPUtils.Instance.writeToLog(customerGuid, "TOTALOFFSITECOUNT", $"{dirlistingDTO.fileDTOs.Count}");
            }
            catch (Exception ex)
            {
                HTTPUtils.Instance.writeToLog(customerGuid, "ERROR", pGoogleBucketName + " DailyUpdateWorker:" + ex.ToString());

            }
        }
    }
}
