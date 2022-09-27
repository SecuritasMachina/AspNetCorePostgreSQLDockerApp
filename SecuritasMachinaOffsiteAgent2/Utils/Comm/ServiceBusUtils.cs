using Azure.Messaging.ServiceBus;
using Common.DTO.V2;
using Common.Statics;
using Confluent.Kafka;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Timer = System.Threading.Timer;

namespace Common.Utils.Comm
{
    public class ServiceBusUtils
    {
        private static SynchronizedCollection<Message<string, string>> serviceBusMessages = new SynchronizedCollection<Message<string, string>>();
        // the client that owns the connection and can be used to create senders and receivers
        // private static ServiceBusClient? client;
        //static DateTime startTime = DateTime.Now;
        private static Timer aTimer;

        // the sender used to publish messages to the queue
        //private static ServiceBusSender? sender;
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

            if (aTimer == null)
            {
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
                lock (aTimer)
                    if (aTimer == null)
                    {
                        //Console.WriteLine(" Creating TImer ");
                        aTimer = new Timer(new TimerCallback(TimerProc));

                        aTimer.Change(5 * 1000, Timeout.Infinite);
                    }


                GenericMessage genericMessage = new GenericMessage();
                genericMessage.guid = pAuthKey;
                genericMessage.nameSpace = nameSpace;
                genericMessage.msgType = messageType;
                genericMessage.msg = json;
                genericMessage.timeStamp = new DateTimeOffset(DateTime.UtcNow).ToUniversalTime().ToUnixTimeMilliseconds();
                genericMessage.authKey = RunTimeSettings.authKey;
                string key = "Key";
                lock (serviceBusMessages)
                    serviceBusMessages.Add(new Message<string, string> { Key = key, Value = JsonConvert.SerializeObject(genericMessage) });
                //TimeSpan span2 = DateTime.Now.Subtract(startTime);

                if (serviceBusMessages.Count > 100)//Something very wrong happened
                {
                    await sendMsgs();
                    lock (aTimer)
                        if (aTimer != null)
                            aTimer.Change(5 * 1000, Timeout.Infinite);
                }
            }
            catch (Exception ex)
            {

                Console.WriteLine("postMsg2ControllerAsync " + ex.ToString());
            }
        }

        private static Task sendMsgs()
        {
            ClientConfig config = new ClientConfig();
            config.BootstrapServers = "172.29.27.15:9093";
            string topic = "coordinator";
            Message<string, string>[] tmpArr = StaticUtils.ToArraySafe(serviceBusMessages);
            using (var producer = new ProducerBuilder<string, string>(config).Build())
            {
                int numProduced = 0;
                foreach (Message<string, string> t1 in tmpArr)
                {
                    string key = t1.Key;
                    string val = t1.Value;
                    Console.WriteLine($"Producing record: {key} {val}");

                    producer.Produce(topic, new Message<string, string> { Key = key, Value = val },
                        (deliveryReport) =>
                        {
                            if (deliveryReport.Error.Code != ErrorCode.NoError)
                            {
                                Console.WriteLine($"Failed to deliver message: {deliveryReport.Error.Reason}");
                            }
                            else
                            {
                                Console.WriteLine($"Produced message to: {deliveryReport.TopicPartitionOffset}");
                                numProduced += 1;
                            }
                        });
                }

                producer.Flush(TimeSpan.FromSeconds(10));
                lock (serviceBusMessages)
                    serviceBusMessages.Clear();
                Console.WriteLine($"{numProduced} messages were produced to topic {topic}");
            }
            return null;
        }

        private static async void TimerProc(object state)
        {
            await sendMsgs();
            lock (aTimer)
                aTimer.Change(5 * 1000, Timeout.Infinite);
        }
    }

}
