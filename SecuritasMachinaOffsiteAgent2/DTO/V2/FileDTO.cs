using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTO.V2
{
    public class FileDTO
    {
        public string? contentType { get; set; }

        public long length { get; set; }
        public long? FileDate { get; set; }

        public string FileName { get; set; }
        public string Path { get; set; }
        public string Status { get; set; }
        public DateTime lastWriteDateTime;
    }
}
