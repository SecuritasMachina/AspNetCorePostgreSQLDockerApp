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
        public enum msgTypes
        {
            dirListing,
            restoreRequest,
            restoreComplete,
            backupComplete
        }
    }
}
