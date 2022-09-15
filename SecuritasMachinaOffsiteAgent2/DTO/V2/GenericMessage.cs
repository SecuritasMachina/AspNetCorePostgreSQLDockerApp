using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTO.V2
{
    public class GenericMessage
    {
        public string msgType;
        public string msg;
        public string guid;
        
        public string? nameSpace;
        public string? authKey;
        public long timeStamp= new DateTimeOffset(DateTime.UtcNow).ToUniversalTime().ToUnixTimeMilliseconds();

        public enum msgTypes
        {
            dirListing,
            restoreRequest,
            restoreComplete,
            backupComplete,
            REPOLIST
        }
    }
}
