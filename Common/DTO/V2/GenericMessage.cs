using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTO.V2
{
    public class GenericMessage
    {
        public string msgType { get; set; }
        public string msg { get; set; }
        public string guid { get; set; }
        public string nameSpace { get; set; }
        public long timeStamp { get; set; }
        public enum msgTypes
        {
            dirListing,
            restoreRequest,
            restoreComplete,
            backupComplete
        }
    }
}
