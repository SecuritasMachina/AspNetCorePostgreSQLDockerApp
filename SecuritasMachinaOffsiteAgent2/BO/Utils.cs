using Azure;
using Azure.Storage.Blobs.Models;
using Common.DTO.V2;
using Common.Utils.Comm;
using Newtonsoft.Json;
using SecuritasMachinaOffsiteAgent.DTO.V2;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SecuritasMachinaOffsiteAgent.BO
{
    public class Utils
    {

        public static DirListingDTO doDirListing(string topiccustomerGuid, string pMountedDir)
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
        public void AES_EncryptStream(string pCustomerGuid, Stream fsIn, string outputFileName, long pContentLength, string pBaseFileName, string password)
        {
            //http://stackoverflow.com/questions/27645527/aes-encryption-on-large-files

            //generate random salt
            byte[] salt = GenerateRandomSalt();

            //create output file name
            FileStream fsCrypt = new FileStream(outputFileName, FileMode.Create);

            //convert password string to byte arrray
            byte[] passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);

            //Set Rijndael symmetric encryption algorithm
            RijndaelManaged AES = new RijndaelManaged();
            AES.KeySize = 256;
            AES.BlockSize = 128;
            AES.Padding = PaddingMode.PKCS7;

            //http://stackoverflow.com/questions/2659214/why-do-i-need-to-use-the-rfc2898derivebytes-class-in-net-instead-of-directly
            //"What it does is repeatedly hash the user password along with the salt." High iteration counts.
            var key = new Rfc2898DeriveBytes(passwordBytes, salt, 50000);
            AES.Key = key.GetBytes(AES.KeySize / 8);
            AES.IV = key.GetBytes(AES.BlockSize / 8);

            //Cipher modes: http://security.stackexchange.com/questions/52665/which-is-the-best-cipher-mode-and-padding-mode-for-aes-encryption
            AES.Mode = CipherMode.CBC;// .CFB;

            //write salt to the begining of the output file, so in this case can be random every time
            fsCrypt.Write(salt, 0, salt.Length);

            CryptoStream cs = new CryptoStream(fsCrypt, AES.CreateEncryptor(), CryptoStreamMode.Write);

            // FileStream fsIn = new FileStream(inputFile, FileMode.Open);

            //create a buffer (1mb) so only this amount will allocate in the memory and not the whole file
            byte[] buffer = new byte[1048576];
            int read;
            long lengthRead = 0;
            bool wrote25 = false;
            bool wrote50 = false;
            bool wrote75 = false;
            try
            {
                while ((read = fsIn.Read(buffer, 0, buffer.Length)) > 0)
                {
                    //   Application.DoEvents(); // -> for responsive GUI, using Task will be better!
                    cs.Write(buffer, 0, read);
                    lengthRead += read;
                   // double pctComplete = ((double)lengthRead / (double)pContentLength);
                    int percentComplete = (int)Math.Round((double)(100 * lengthRead) / (double)pContentLength);
                    if (pContentLength > 1024 * 1024 * 10)
                    {
                        if (percentComplete > 25 && !wrote25)
                        {
                            HTTPUtils.Instance.writeToLog(pCustomerGuid, "BACKUP-UPDATE", $"Backup {pBaseFileName} is {percentComplete}% complete");
                            wrote25 = true;
                        }
                        if (percentComplete > 50 && !wrote50)
                        {
                            HTTPUtils.Instance.writeToLog(pCustomerGuid, "BACKUP-UPDATE", $"Backup {pBaseFileName} is {percentComplete}% complete");
                            wrote50 = true;
                        }
                        if (percentComplete > 75 && !wrote75)
                        {
                            HTTPUtils.Instance.writeToLog(pCustomerGuid, "BACKUP-UPDATE", $"Backup {pBaseFileName} is {percentComplete}% complete");
                            wrote75 = true;
                        }
                    }
                }

                //close up
                fsIn.Close();

            }
            catch(Azure.RequestFailedException ex)
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
            finally
            {
                cs.Close();
                fsCrypt.Close();
            }
        }
        public void AES_DecryptStream(string topiccustomerGuid, Stream fsCrypt, Stream fsOut, long pContentLength, string pInfileName, string password)
        {
            //todo:
            // - create error message on wrong password
            // - on cancel: close and delete file
            // - on wrong password: close and delete file!
            // - create a better filen name
            // - could be check md5 hash on the files but it make this slow

            byte[] passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);
            byte[] salt = new byte[32];

            //FileStream fsCrypt = new FileStream(inputFile, FileMode.Open);
            fsCrypt.Read(salt, 0, salt.Length);

            RijndaelManaged AES = new RijndaelManaged();
            AES.KeySize = 256;
            AES.BlockSize = 128;
            var key = new Rfc2898DeriveBytes(passwordBytes, salt, 50000);
            AES.Key = key.GetBytes(AES.KeySize / 8);
            AES.IV = key.GetBytes(AES.BlockSize / 8);
            AES.Padding = PaddingMode.PKCS7;
            AES.Mode = CipherMode.CBC;// .CFB;

            CryptoStream cs = new CryptoStream(fsCrypt, AES.CreateDecryptor(), CryptoStreamMode.Read);

            //FileStream fsOut = new FileStream(inputFile + ".decrypted", FileMode.Create);

            int read;
            byte[] buffer = new byte[1048576];
            long lengthRead = 0;
            bool wrote25 = false;
            bool wrote50 = false;
            bool wrote75 = false;
            try
            {
                long tGB = 0;
                while ((read = cs.Read(buffer, 0, buffer.Length)) > 0)
                {
                    //Application.DoEvents();
                    tGB++;
                    fsOut.Write(buffer, 0, read);
                    lengthRead += read;

                    int percentComplete = (int)Math.Round((double)(100 * lengthRead) / (double)pContentLength);
                    if (pContentLength > 1024 * 1024 * 10)
                    {
                        if (percentComplete > 25 && !wrote25)
                        {
                            HTTPUtils.Instance.writeToLog(topiccustomerGuid, "RESTORE-UPDATE", $"Restoring {pInfileName} is {percentComplete}% complete");
                            wrote25 = true;
                        }
                        if (percentComplete > 50 && !wrote50)
                        {
                            HTTPUtils.Instance.writeToLog(topiccustomerGuid, "RESTORE-UPDATE", $"Restoring {pInfileName} is {percentComplete}% complete");
                            wrote50 = true;
                        }
                        if (percentComplete > 75 && !wrote75)
                        {
                            HTTPUtils.Instance.writeToLog(topiccustomerGuid, "RESTORE-UPDATE", $"Restoring {pInfileName} is {percentComplete}% complete");
                            wrote75 = true;
                        }
                    }
                    //HTTPUtils.Instance.writeToLog(topiccustomerGuid, "TRACE", $"...Decypted {tGB} GB of {pInfileName}");
                }
            }
            catch (CryptographicException ex_CryptographicException)
            {
                Debug.WriteLine("CryptographicException error: " + ex_CryptographicException.Message);
                HTTPUtils.Instance.writeToLog(topiccustomerGuid, "ERROR", $"...Error on {pInfileName} AES Decypt {ex_CryptographicException.Message.ToString()}, check passphrase");
                throw (ex_CryptographicException);
            }
            catch (Exception ex)
            {
                HTTPUtils.Instance.writeToLog(topiccustomerGuid, "ERROR", $"...Error on {pInfileName} AES Decypt {ex.Message.ToString()}");
                throw (ex);
            }

            try
            {
                cs.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error by closing CryptoStream: " + ex.Message);
            }
            finally
            {
                fsOut.Close();
                fsCrypt.Close();
            }
        }

        internal static async Task<DirListingDTO> doDirListingAsync(AsyncPageable<BlobItem> asyncPageable)
        {
            DirListingDTO ret = new DirListingDTO();
            await foreach (BlobItem blobItem in asyncPageable)
            {
                FileDTO fileDTO = new FileDTO();
                fileDTO.FileName = blobItem.Name;
                fileDTO.length = blobItem.Properties.ContentLength;
                fileDTO.FileDate = blobItem.Properties.LastModified.Value.ToUnixTimeMilliseconds();

                ret.fileDTOs.Add(fileDTO);
            }
            return ret;
        }

        internal static void UpdateOffsiteBytes(string customerGuid, string inPath)
        {
            HTTPUtils.Instance.writeToLog(customerGuid, "TRACE", $"Scanning {inPath} total size");
            DirectoryInfo directoryInfo = new DirectoryInfo(inPath);
            List<FileInfo> Files2 = directoryInfo.GetFiles("*").ToList();
            long tsize = 0;
            try
            {
                foreach (FileInfo file in Files2)
                {
                    tsize += file.Length;

                }
                HTTPUtils.Instance.writeToLog(customerGuid, "TOTALOFFSITEBYTES", $"{tsize}");
                HTTPUtils.Instance.writeToLog(customerGuid, "TOTALOFFSITECOUNT", $"{Files2.LongCount()}");
            }
            catch (Exception ex)
            {
                HTTPUtils.Instance.writeToLog(customerGuid, "ERROR", inPath + " DailyUpdateWorker:" + ex.ToString());

            }
        }
    }
}
