using Azure.Storage.Blobs;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Timers;

namespace AspNetCorePostgreSQLDockerApp.BO
{
    public class BackupWorker
    {
        private string azureBlobEndpoint;
        private string BlobContainerName;
        private string backupName;
        private string passPhrase;
        private BackgroundWorker worker;
        private int loopCount = 0;

        public BackupWorker(string azureBlobEndpoint, string BlobContainerName, string backupName, string passPhrase)
        {
            Console.WriteLine("Starting BackupWorker for "+ backupName);
            
            this.azureBlobEndpoint = azureBlobEndpoint;
            this.BlobContainerName = BlobContainerName;
            this.backupName = backupName;
            this.passPhrase = passPhrase;
            worker = new BackgroundWorker();
            worker.DoWork += worker_DoWork;
            Timer timer = new Timer(1000 * 60);
            timer.Elapsed += timer_Elapsed;
            timer.Start();
        }

        void timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!worker.IsBusy)
                worker.RunWorkerAsync();
        }

        void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            // Create a BlobServiceClient object which will be used to create a container client
            try
            {
                loopCount++;
                Console.WriteLine("Looking for: " + backupName);
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
                //report progress
                worker.CancelAsync();
            }
            catch (Exception ex)
            {
                if (loopCount > 12 * 60)
                {
                    Console.WriteLine("Over 12 hours has elapsed, cancelling thread looking for: " + backupName);
                    worker.CancelAsync();
                }
            }
        }
    }
}
