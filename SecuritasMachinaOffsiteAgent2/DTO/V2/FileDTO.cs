using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTO.V2
{
    public class FileDTO
    {
        public long length;

        public string FileName { get; set; }
        public string Path { get; set; }
        public string Status { get; set; }
    }
}
