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
        internal List<FileDTO> StagingFileDTOs = new List<FileDTO>();
    }
}
