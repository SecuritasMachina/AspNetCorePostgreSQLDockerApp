using Common.DTO.V2;
using Common.Statics;
using Common.Utils.Comm;
using Google.Cloud.Storage.V1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SecuritasMachinaOffsiteAgent.Utils.Comm.GoogleAPI
{
    internal class CloudUtils
    {
        private static CloudUtils? instance;
        private static StorageClient _client;
        private static string _testFileName = "TempTestFile.txt";

        //private static var? _client = null;
        public static CloudUtils Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new CloudUtils();
                }
                return instance;
            }
        }
        private CloudUtils()
        {

            if (_client == null)
            {

                _client = StorageClient.Create();
                _testFileName = "TempTestFile-" + Path.GetRandomFileName();
            }
            // _client.BaseAddress = new Uri(RunTimeSettings.WebListenerURL);
        }
       
        public DirListingDTO listFiles(string pGoogleStorageBucketName)
        {

            // List objects\
            DirListingDTO ret = new DirListingDTO();
            foreach (var file in _client.ListObjects(pGoogleStorageBucketName, "").OrderByDescending(f => f.Updated))
            {
                FileDTO fDTO = new FileDTO();
                fDTO.FileName = file.Name;
                fDTO.contentType = file.ContentType;
                fDTO.length = (long)file.Size;
                fDTO.lastWriteDateTime = (DateTime)file.Updated;
                long unixTimeMilliseconds = new DateTimeOffset((DateTime)file.Updated).ToUnixTimeMilliseconds();
                fDTO.FileDate = unixTimeMilliseconds;
                ret.fileDTOs.Add(fDTO);
            }
            return ret;
        }

        internal bool testWrite(string googleStorageBucketName)
        {
            bool ret = false;
            UploadObjectOptions uploadObjectOptions = new UploadObjectOptions();
            string password = "TheQUickBrownFoxJumpedOvertheLazyBrownDog".Substring(0,32);
            EncryptionKey encryptionKey = EncryptionKey.Create(Encoding.UTF8.GetBytes(password));
            uploadObjectOptions.EncryptionKey = encryptionKey;
            var content = Encoding.UTF8.GetBytes("hello, world");
            var obj1 = _client.UploadObject(googleStorageBucketName, _testFileName, "text/plain", new MemoryStream(content), uploadObjectOptions);
            ret = true;
            return ret;
        }
        internal bool testRead(string googleStorageBucketName)
        {
            bool ret = false;
            var content = Encoding.UTF8.GetBytes("hello, world");
            DownloadObjectOptions downloadObjectOptions = new DownloadObjectOptions();
            string password = "TheQUickBrownFoxJumpedOvertheLazyBrownDog".Substring(0, 32);
            EncryptionKey encryptionKey = EncryptionKey.Create(Encoding.UTF8.GetBytes(password));
            downloadObjectOptions.EncryptionKey = encryptionKey;
            _client.DownloadObject(googleStorageBucketName, _testFileName, new MemoryStream(content), downloadObjectOptions);


            ret = true;
            return ret;
        }
        internal bool testDelete(string googleStorageBucketName)
        {
            bool ret = false;


            _client.DeleteObject(googleStorageBucketName, _testFileName);


            ret = true;
            return ret;
        }

        internal bool deleteFile(string googleBucketName, string fileName)
        {
            bool ret = false;
            _client.DeleteObject(googleBucketName, fileName);
            ret = true;
            return ret;
        }
    }
}
