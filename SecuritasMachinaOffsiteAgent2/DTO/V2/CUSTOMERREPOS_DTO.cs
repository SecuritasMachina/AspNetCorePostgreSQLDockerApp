using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTO.V2
{
    public class CUSTOMERREPOS_DTO
    {
   
         public string? id{ get; set; }  
         public string? customerIDFK{ get; set; }  
         public string? repoName{ get; set; }  
         public string? repoType{ get; set; }  
         public string? authToken{ get; set; }  
         public string? authName{ get; set; }  
         public DateTime? dateEntered{ get; set; }
    }
}
