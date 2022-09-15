using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecuritasMachinaOffsiteAgent.DTO.V2
{
    internal class RepoDTO
    {
        public string Description { get; set; }
        public string FullName { get; set; }
        public string Url { get; set; }
        public long Size { get; set; }
        public long UpdatedAt { get; set; }
    }
}
