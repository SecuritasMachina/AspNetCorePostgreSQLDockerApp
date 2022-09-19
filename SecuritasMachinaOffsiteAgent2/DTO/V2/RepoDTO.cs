using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTO.V2
{
    public class RepoDTO
    {
        public string Description  { get; set; }
        public string FullName  { get; set; }
        public string Url  { get; set; }
        public long Size { get; set; }
        public long UpdatedAt { get; set; }
        public string backupFrequency { get; set; }
        public long lastBackup { get; set; }
        public DateTime lastBackupDate { get; set; }
        public string id { get; set; }
        public DateTime lastSyncDate { get; set; }
        public int syncMinimumHours { get; set; }
        public int syncMinArchiveHours { get; set; }
    }
}
