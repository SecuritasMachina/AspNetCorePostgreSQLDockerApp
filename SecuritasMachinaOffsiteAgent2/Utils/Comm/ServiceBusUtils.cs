using Azure.Messaging.ServiceBus;
using Common.Statics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Utils.Comm
{
    public class ServiceBusUtils
    {

        // the client that owns the connection and can be used to create senders and receivers
        static ServiceBusClient client;

        // the sender used to publish messages to the queue
        static ServiceBusSender sender;
        public static async Task postMsg2ControllerAsync(string myJson)
        {
            try
            {
                if (client == null)
                    client = new ServiceBusClient(RunTimeSettings.SBConnectionString);
                if (sender == null)
                    sender = client.CreateSender(RunTimeSettings.topicNameCustomerGuid);
                ServiceBusMessageBatch messageBatch = await sender.CreateMessageBatchAsync();
                messageBatch.TryAddMessage(new ServiceBusMessage(myJson));
                await sender.SendMessagesAsync(messageBatch);
                HTTPUtils.writeToLog(RunTimeSettings.topicNameCustomerGuid, "INFO", "Sent " + myJson.Length + " bytes to message handler");
                //Console.WriteLine("Posting " + myJson);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
