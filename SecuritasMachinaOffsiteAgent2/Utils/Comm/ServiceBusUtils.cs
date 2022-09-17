using Azure.Messaging.ServiceBus;
using Common.DTO.V2;
using Common.Statics;
using ConcurrentList;
using Newtonsoft.Json;
using SecuritasMachinaOffsiteAgent.BO;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Threading.Timer;

namespace Common.Utils.Comm
{
    public class ServiceBusUtils
    {
        private static SynchronizedCollection<ServiceBusMessage> serviceBusMessages = new SynchronizedCollection<ServiceBusMessage>();
        // the client that owns the connection and can be used to create senders and receivers
        private static ServiceBusClient? client;
        //static DateTime startTime = DateTime.Now;
        private static Timer aTimer;

        // the sender used to publish messages to the queue
        private static ServiceBusSender? sender;
        private static ServiceBusUtils? instance;

        public static ServiceBusUtils Instance
        {
            get
            {
                if (instance == null)
                {
                    //Console.WriteLine("instance = new ThreadUtilsV2();");
                    instance = new ServiceBusUtils();
                }
                return instance;
            }
        }
        private ServiceBusUtils()
        {

            if (client == null)
                client = new ServiceBusClient(RunTimeSettings.sbrootConnectionString);
            if (sender == null)
                sender = client.CreateSender("coordinator");

            if (aTimer == null)
            {
                Console.WriteLine(" Creating TImer ");
                aTimer = new Timer(new TimerCallback(TimerProc));
                lock (aTimer)
                    aTimer.Change(5 * 1000, Timeout.Infinite);
            }

        }
        
        public async Task postMsg2ControllerAsync(string? nameSpace, string? pAuthKey, string? messageType, string? json)
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
                    //Console.WriteLine(" Creating TImer ");
                    aTimer = new Timer(new TimerCallback(TimerProc));
                    lock (aTimer)
                        aTimer.Change(5 * 1000, Timeout.Infinite);
                }


                GenericMessage genericMessage = new GenericMessage();
                genericMessage.guid = pAuthKey;
                genericMessage.nameSpace = nameSpace;
                genericMessage.msgType = messageType;
                genericMessage.msg = json;
                genericMessage.timeStamp = new DateTimeOffset(DateTime.UtcNow).ToUniversalTime().ToUnixTimeMilliseconds();
                genericMessage.authKey = RunTimeSettings.authKey;
                
                serviceBusMessages.Add(new ServiceBusMessage(JsonConvert.SerializeObject(genericMessage)));
                //TimeSpan span2 = DateTime.Now.Subtract(startTime);

                if (serviceBusMessages.Count > 100)//Something very wrong happened
                {
                    ServiceBusMessage[] tmpArr = StaticUtils.ToArraySafe(serviceBusMessages);
                    await sender.SendMessagesAsync(tmpArr);
                    Console.WriteLine(serviceBusMessages.Count + " messages sent by Count ");
                    serviceBusMessages.Clear();
                    //startTime = DateTime.Now;
                    lock (aTimer)
                        if (aTimer != null)
                        {
                            //aTimer.Change(Timeout.Infinite, Timeout.Infinite);
                            aTimer.Change(5 * 1000, Timeout.Infinite);

                            //aTimer.Dispose();
                        }
                }
            }
            catch (Exception ex)
            {

                Console.WriteLine("postMsg2ControllerAsync " + ex.ToString());
            }
        }
        private static async void TimerProc(object state)
        {
            ServiceBusMessage[] tmpArr = StaticUtils.ToArraySafe(serviceBusMessages);
            if (tmpArr.Length > 0)
            {
                if (client == null)
                    client = new ServiceBusClient(RunTimeSettings.sbrootConnectionString);
                if (sender == null)
                    sender = client.CreateSender("coordinator");
                
                await sender.SendMessagesAsync(tmpArr);
                //Console.WriteLine(Thread.CurrentThread.ManagedThreadId + " " + serviceBusMessages.Count + " messages sent by timer ");
                serviceBusMessages.Clear();
            }

            //startTime = DateTime.Now;
            // The state object is the Timer object.
            var t = (Timer)state;

            lock (aTimer)
                aTimer.Change(5 * 1000, Timeout.Infinite);




        }
    }

}
