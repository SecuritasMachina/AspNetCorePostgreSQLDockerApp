using Azure;
using Azure.Storage.Blobs.Models;
using Common.DTO.V2;
using Common.Statics;
using Common.Utils.Comm;
using Google.Apis.Download;
using Google.Apis.Upload;
using Google.Cloud.Storage.V1;
using Microsoft.Azure.Amqp.Framing;
using Newtonsoft.Json;
using SecuritasMachinaOffsiteAgent.DTO.V2;
using SecuritasMachinaOffsiteAgent.Utils.Comm.GoogleAPI;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using static System.Net.WebRequestMethods;
using File = System.IO.File;

namespace SecuritasMachinaOffsiteAgent.BO
{
    public class Utils
    {
        private static StringBuilder cmdOutput = null;
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
                if (String.IsNullOrEmpty(password))
                    password = "AES_EncryptStreamAES_Encrypt2StreamAES_EncryptStreamAES_EncryptStreamAES_EncryptStream";
                password = password.Substring(0, 32);
                StorageClient googleClient = StorageClient.Create();
                byte[] salt = GenerateRandomSalt();


                using (Aes aesAlg = Aes.Create())
                {
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
                                 if (result >= 1)
                                 {
                                     int percentComplete = (int)Math.Round((double)(100 * p.BytesSent) / (double)pContentLength);
                                     HTTPUtils.Instance.writeToLogAsync(pCustomerGuid, "BACKUP-UPDATE", $"Backup {pBaseFileName} is {percentComplete}% complete");
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
                    HTTPUtils.Instance.writeToLogAsync(pCustomerGuid, "INFO", msg);
                    return;
                }
                else
                {
                    HTTPUtils.Instance.writeToLogAsync(pCustomerGuid, "ERROR", pBaseFileName + " " + ex.ToString());
                }

            }
            catch (Exception ex)
            {


                HTTPUtils.Instance.writeToLogAsync(pCustomerGuid, "ERROR", pBaseFileName + " " + ex.ToString());
                throw (ex);
            }

        }
        public void AES_DecryptStream(string pCustomerGuid, string pGoogleBucketName, string pFileToRestore, Stream pOutStream, long pContentLength, string pInfileName, string password)
        {

            using (Aes aesAlg = Aes.Create())
            {
                //byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
                if (String.IsNullOrEmpty(password))
                    password = "AES_EncryptStreamAES_Encrypt2StreamAES_EncryptStreamAES_EncryptStreamAES_EncryptStream";
                password = password.Substring(0, 32);

                StorageClient googleClient = StorageClient.Create();

                DownloadObjectOptions downloadObjectOptions = new DownloadObjectOptions();
                EncryptionKey encryptionKey = EncryptionKey.Create(Encoding.UTF8.GetBytes(password));
                downloadObjectOptions.EncryptionKey = encryptionKey;

                DateTime start = DateTime.Now;
                var progress = new Progress<IDownloadProgress>(
                   p =>
                   {
                       DateTime end = DateTime.Now;
                       var result = end.Subtract(start).TotalSeconds;
                       if (result >= 10)
                       {
                           start = DateTime.Now;
                           int percentComplete = (int)Math.Round((double)(100 * p.BytesDownloaded) / (double)pContentLength);
                           HTTPUtils.Instance.writeToLogAsync(pCustomerGuid, "BACKUP-UPDATE", $"Backup {pFileToRestore} is {percentComplete}% complete");

                       }
                   }
                );

                googleClient.DownloadObject(pGoogleBucketName, pFileToRestore, pOutStream, downloadObjectOptions, progress);

            }
        }


        internal static void UpdateOffsiteBytes(string customerGuid, string pGoogleBucketName)
        {
            HTTPUtils.Instance.writeToLogAsync(customerGuid, "TRACE", $"Scanning Google Storage Bucket {pGoogleBucketName}");
            DirListingDTO dirlistingDTO = CloudUtils.Instance.listFiles(pGoogleBucketName);

            long? tsize = 0;
            try
            {
                foreach (FileDTO file in dirlistingDTO.fileDTOs)
                {
                    tsize += file.length;

                }
                HTTPUtils.Instance.writeToLogAsync(customerGuid, "TOTALOFFSITEBYTES", $"{tsize}");
                HTTPUtils.Instance.writeToLogAsync(customerGuid, "TOTALOFFSITECOUNT", $"{dirlistingDTO.fileDTOs.Count}");
            }
            catch (Exception ex)
            {
                HTTPUtils.Instance.writeToLogAsync(customerGuid, "ERROR", pGoogleBucketName + " DailyUpdateWorker:" + ex.ToString());

            }
        }
        public static int ShellExec(String workingDir, String command)
        {
            //Console.WriteLine(command);
            ProcessStartInfo info;
            cmdOutput = new StringBuilder();
            // Style = ProgressBarStyle.Marquee;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                info = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    LoadUserProfile = true,
                    ErrorDialog = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    RedirectStandardError = true,
                    StandardErrorEncoding = Encoding.UTF8,
                    WorkingDirectory = workingDir,
                    FileName = "bash",
                    Arguments = "/k " + command
                };
            }
            else
            {
                info = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    LoadUserProfile = true,
                    ErrorDialog = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    RedirectStandardError = true,
                    StandardErrorEncoding = Encoding.UTF8,
                    WorkingDirectory = workingDir,
                    FileName = "cmd.exe",
                    Arguments = "/c " + command
                };

            }

            Process shell = new Process();
            shell.StartInfo = info;
            shell.EnableRaisingEvents = true;
            shell.ErrorDataReceived += new DataReceivedEventHandler(ShellErrorDataReceived);
            shell.OutputDataReceived += new DataReceivedEventHandler(ShellOutputDataReceived);

            shell.Start();
            shell.BeginErrorReadLine();
            shell.BeginOutputReadLine();
            shell.WaitForExit();

            return shell.ExitCode;
        }
        static int numOutputLines = 0;
        private static void ShellErrorDataReceived(object sendingProcess,
           DataReceivedEventArgs outLine)
        {
            // Collect the sort command output.
            if (!String.IsNullOrEmpty(outLine.Data))
            {
                numOutputLines++;

                // Add the text to the collected output.
                //cmdOutput.Append(Environment.NewLine +
                 //   $"[{numOutputLines}] - {outLine.Data}");
                HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"[{numOutputLines}] - {outLine.Data}");
            }
        }
        private static void ShellOutputDataReceived(object sendingProcess,
           DataReceivedEventArgs outLine)
        {
            // Collect the sort command output.
            if (!String.IsNullOrEmpty(outLine.Data))
            {
                numOutputLines++;

                // Add the text to the collected output.
                //cmdOutput.Append(Environment.NewLine +
               //     $"[{numOutputLines}] - {outLine.Data}");
                HTTPUtils.Instance.writeToLogAsync(RunTimeSettings.customerAgentAuthKey, "TRACE", $"[{numOutputLines}] - {outLine.Data}");
            }
        }

        internal static void writeFileToGoogle(string _customerGuid, string contentType, string _googleBucketName,string pBaseBackupName,  string outFileName, string _passPhrase)
        {
            try
            {
                long startTimeStamp = new DateTimeOffset(DateTime.UtcNow).ToUniversalTime().ToUnixTimeMilliseconds();
                long fileLength = new System.IO.FileInfo(outFileName).Length;
                string _backupName = new System.IO.FileInfo(outFileName).Name;
                StorageClient googleClient = StorageClient.Create();
                using (var inStream = System.IO.File.OpenRead(outFileName))
                {
                    new Utils().AES_EncryptStream(_customerGuid, inStream, contentType, _googleBucketName, _backupName, fileLength, _backupName, _passPhrase);
                }
                File.Delete(outFileName);
                Google.Apis.Storage.v1.Data.Object outFileProperties = googleClient.GetObject(_googleBucketName, _backupName);
                HTTPUtils.Instance.writeBackupHistory(_customerGuid, pBaseBackupName, _backupName, (long)outFileProperties.Size, startTimeStamp);
                //
            }
            catch(Exception ex)
            {

            }
            
            //throw new NotImplementedException();
        }
    }
}
