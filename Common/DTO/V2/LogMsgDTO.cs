using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTO.V2
{
    public class LogMsgDTO
    {
        public string logType { get; set; }

        public long logTime { get; set; }
        public string id { get; set; }

        public string msg { get; set; }
       
    }
}
