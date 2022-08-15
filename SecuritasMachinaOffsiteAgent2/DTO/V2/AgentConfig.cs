using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTO.V2
{
    public class AgentConfig
    {
        public string ServiceBusEndPoint;
        public string ServiceBusSubscription;
        public string topicName;
        public string passPhrase;
        public Boolean subscriptionActive = false;
    }
}
