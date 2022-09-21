using System;
using System.Collections.Generic;


namespace Common.DTO.V2
{
    public class DirListingDTO
    {
        public List<FileDTO> fileDTOs= new  List<FileDTO>();
        public string[] fileArray;
        
        //public string msgType = "";
        //public FileInfo[] fileInfo;
    }
}
