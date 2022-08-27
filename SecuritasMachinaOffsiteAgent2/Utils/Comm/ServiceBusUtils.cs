using Azure.Messaging.ServiceBus;
using Common.DTO.V2;
using Common.Statics;
using Newtonsoft.Json;
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
        static ServiceBusClient? client;

        // the sender used to publish messages to the queue
        static ServiceBusSender? sender;
        public static async Task postMsg2ControllerAsync(string? nameSpace, string? guid, string? messageType, string? json)
        {
            try
            {
                if (client == null)
                    client = new ServiceBusClient(RunTimeSettings.SBConnectionString);
                if (sender == null)
                    sender = client.CreateSender("coordinator");
                GenericMessage genericMessage = new GenericMessage();
                genericMessage.guid = guid;
                genericMessage.nameSpace = nameSpace;
                genericMessage.msgType = messageType;
                genericMessage.msg = json;
                genericMessage.authKey = RunTimeSettings.authKey;
                //Console.WriteLine(messageType+" "+ json);
                ServiceBusMessageBatch messageBatch = await sender.CreateMessageBatchAsync();
                messageBatch.TryAddMessage(new ServiceBusMessage(JsonConvert.SerializeObject(genericMessage)));
                await sender.SendMessagesAsync(messageBatch);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
