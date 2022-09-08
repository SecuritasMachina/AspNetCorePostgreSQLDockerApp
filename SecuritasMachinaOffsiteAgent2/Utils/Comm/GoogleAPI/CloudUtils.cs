using Common.Statics;
using Google.Cloud.Storage.V1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecuritasMachinaOffsiteAgent.Utils.Comm.GoogleAPI
{
    internal class CloudUtils
    {
        public bool uploadFile(Stream pSource)
        {
            bool ret=false;
            var client = StorageClient.Create();
            var content = Encoding.UTF8.GetBytes("hello, world");
            var obj1 = client.UploadObject(RunTimeSettings.GoogleStorageBucketName, "file1.txt", "text/plain", new MemoryStream(content));
            ret = true;
            return ret;
        }
    }
}
