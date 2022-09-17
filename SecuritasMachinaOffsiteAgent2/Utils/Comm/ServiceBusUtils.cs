using Azure.Messaging.ServiceBus;
using Common.DTO.V2;
using Common.Statics;
using ConcurrentList;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Utils.Comm
{
    public class ServiceBusUtils
    {
        static SynchronizedCollection<ServiceBusMessage> serviceBusMessages = new SynchronizedCollection<ServiceBusMessage>();
        // the client that owns the connection and can be used to create senders and receivers
        static ServiceBusClient? client;
        static DateTime startTime = DateTime.Now;
        private static Timer aTimer;

        // the sender used to publish messages to the queue
        static ServiceBusSender? sender;
        public static async Task postMsg2ControllerAsync(string? nameSpace, string? pAuthKey, string? messageType, string? json)
        {
            // Console.WriteLine($"nameSpace:{nameSpace} messageType:{messageType} RunTimeSettings.sbrootConnectionString {RunTimeSettings.sbrootConnectionString}");
            try
            {
                if (client == null)
                    client = new ServiceBusClient(RunTimeSettings.sbrootConnectionString);
                if (sender == null)
                    sender = client.CreateSender("coordinator");
                if (aTimer == null)
                {
                    aTimer = new Timer(new TimerCallback(TimerProc));
                    aTimer.Change(10 * 1000, Timeout.Infinite);
                }
                

                GenericMessage genericMessage = new GenericMessage();
                genericMessage.guid = pAuthKey;
                genericMessage.nameSpace = nameSpace;
                genericMessage.msgType = messageType;
                genericMessage.msg = json;
                genericMessage.timeStamp = new DateTimeOffset(DateTime.UtcNow).ToUniversalTime().ToUnixTimeMilliseconds();
                genericMessage.authKey = RunTimeSettings.authKey;
                serviceBusMessages.Add(new ServiceBusMessage(JsonConvert.SerializeObject(genericMessage)));
                TimeSpan span2 = DateTime.Now.Subtract(startTime);

                if (serviceBusMessages.Count > 10 || span2.TotalSeconds > 5)
                {
                    await sender.SendMessagesAsync(serviceBusMessages);
                    serviceBusMessages.Clear();
                    startTime = DateTime.Now;
                    if (aTimer != null)
                    {
                        aTimer.Dispose();
                    }
                    
                        aTimer = new Timer(new TimerCallback(TimerProc));
                        aTimer.Change(10 * 1000, Timeout.Infinite);
                    

                }


            }
            catch (Exception ex)
            {

                Console.WriteLine("postMsg2ControllerAsync " + ex.ToString());
            }
        }
        private static async void TimerProc(object state)
        {
            if (client == null)
                client = new ServiceBusClient(RunTimeSettings.sbrootConnectionString);
            if (sender == null)
                sender = client.CreateSender("coordinator");
            // The state object is the Timer object.
            await sender.SendMessagesAsync(serviceBusMessages);
            serviceBusMessages.Clear();
            startTime = DateTime.Now;
            var t = (Timer)state;

            t.Dispose();
            //Console.WriteLine("The timer callback executes.");
            //active = false;

            // Action to do when timer is back to zero
        }
    }
    
}
