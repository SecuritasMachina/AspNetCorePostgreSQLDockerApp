using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTO.V2
{
    public class BackupHistoryDTO
    {
        public long purgeDate { get; set; }
        public long endTimeStamp { get; set; }
        public string newFileName { get; set; }
        public long fileLength { get; set; }
        //public long timeStamp { get; set; }
        public long startTimeStamp { get; set; }
        public string backupFile { get; set; }

       
    }
}
