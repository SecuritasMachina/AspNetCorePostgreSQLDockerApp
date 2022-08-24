using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecuritasMachinaOffsiteAgent.DTO.V2
{
    internal class BackupHistoryDTO
    {
        public string backupFile { get; set; }
        public string newFileName { get; set; }
        
        public long fileLength { get; set; }
        public long startTimeStamp { get; set; }
    }
}
