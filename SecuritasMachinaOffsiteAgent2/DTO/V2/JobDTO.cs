﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecuritasMachinaOffsiteAgent.DTO.V2
{
    internal class JobDTO
    {
        public string workerName { get; set; }
        public string id { get; set; }
        public string cronSpec { get; set; }
        public DateTime dateEntered { get; set; }
    }
}
