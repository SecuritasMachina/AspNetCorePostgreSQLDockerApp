using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.Azure.Amqp.Serialization.SerializableType;

namespace Common.DTO.V2
{
    public class DirListingDTO
    {
        public List<FileDTO> fileDTOs = new List<FileDTO>();
        //public string[] fileArray;

        //public string msgType = "";
        //public FileInfo[] fileInfo;
    }
}
