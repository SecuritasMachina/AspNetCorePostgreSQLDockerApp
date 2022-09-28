using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTO.V2
{
    public class AgentConfig
    {
        public string azureBlobEndpoint { get; set; }
        public string serviceBusTopic { get; set; }
        public string clientSubscriptionName { get; set; }

        public string coordinatorTopicName { get; set; }
        public string passPhrase { get; set; }
        public Boolean subscriptionActive { get; set; }
        public int PollBaseTime { get; set; }
        public string name { get; set; }
        public string contactEmail { get; set; }
       
        public string controllerTopicName { get; set; }
        public string azureSourceBlobContainerName { get; set; }
        public string googleStorageBucketName { get; set; }
        public string azureBlobRestoreContainerName { get; set; }
        public string encryptionPassPhrase { get; set; }
        public int retentionDays { get; set; }
        public int maxThreads { get; set; }
        public string GOOGLE_APPLICATION_CREDENTIALS { get; set; }
        public string kafkaBootstrapServers { get; set; }
        public string kafkaPassword { get; set; }
        public string kafkaLogin { get; set; }
    }
}
