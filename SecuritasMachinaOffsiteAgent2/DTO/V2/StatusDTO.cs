using Common.DTO.V2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecuritasMachinaOffsiteAgent.DTO.V2
{
    internal class StatusDTO
    {
        public long activeThreads { get; set; }
        public List<FileDTO> AgentFileDTOs = new List<FileDTO>();
        public List<FileDTO> StagingFileDTOs = new List<FileDTO>();
        public  List<FileDTO> RestoredListingDTO = new List<FileDTO>();

        public long TotalMemory { get; set; }

        public long WorkingSet64 { get; set; }

        public long TotalProcessorTime { get; set; }

        public long  UserProcessorTime { get; set; }
    }
}
