using Common.Statics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Common.Utils.Comm
{
    public class HTTPUtils
    {
        private static HttpClient client = null;
        public static void populateRuntime(string pCustomerGuid)
        {
            if (client == null)
            {
                HttpClientHandler handler = new HttpClientHandler()
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                };
                client = new HttpClient(handler);
            }
            client.BaseAddress = new Uri("https://securitasmachinacoordinater.azurewebsites.net");
            HttpResponseMessage response = client.GetAsync("/api/v2/config/" + pCustomerGuid).Result;
            response.EnsureSuccessStatusCode();
            string result = response.Content.ReadAsStringAsync().Result;
            dynamic stuff = JsonConvert.DeserializeObject(result);
            RunTimeSettings.passPhrase = stuff.passPhrase;
            RunTimeSettings.SBConnectionString = stuff.ServiceBusEndPoint;
            RunTimeSettings.topicNameCustomerGuid = stuff.topicName;
            RunTimeSettings.topicCustomerGuid = pCustomerGuid;


        }

        public static void writeToLog(string? guid, string? messageType, string? json)
        {
            if (json == null)
                json = "empty msg";
            string serializedJson = JsonConvert.SerializeObject(json);
            Debug.WriteLine($"writeToLog: guid:{guid} messageType:{messageType} json:{json}");
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(RunTimeSettings.WebListenerURL + "api/v3/putLog/" + RunTimeSettings.topicCustomerGuid + "/" + messageType);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Accept = "application/json";
            request.ContentLength = serializedJson.Length;
            using (Stream webStream = request.GetRequestStream())
            using (StreamWriter requestWriter = new StreamWriter(webStream, System.Text.Encoding.ASCII))
            {
                requestWriter.Write(serializedJson);
            }

            try
            {
                WebResponse webResponse = request.GetResponse();
                using (Stream webStream = webResponse.GetResponseStream() ?? Stream.Null)
                using (StreamReader responseReader = new StreamReader(webStream))
                {
                    string response = responseReader.ReadToEnd();
                    Console.Out.WriteLine(response);
                }
            }
            catch (Exception e)
            {
                Console.Out.WriteLine("-----------------");
                Console.Out.WriteLine(e.Message);
                //HTTPUtils.writeToLog(guid, "ERROR", e.ToString());
            }
        }
        public static void writeBackupHistory(string? guid, string? backupFile, string newFileName, long fileLength,long startTimeStamp)
        {

            //string serializedJson = JsonConvert.SerializeObject(json);
            //Debug.WriteLine($"writeToLog: guid:{guid} backupFile:{backupFile} json:{json}");
            string url = RunTimeSettings.WebListenerURL + "api/v3/postBackupHistory/" + RunTimeSettings.topicCustomerGuid + "/" + Uri.EscapeUriString(backupFile) + "/" + Uri.EscapeUriString(newFileName) + "/" + fileLength+"/"+ startTimeStamp;
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.AutomaticDecompression = DecompressionMethods.GZip;

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    string html = reader.ReadToEnd();
                    Console.Out.WriteLine(html);
                }
                
            }
            catch (Exception e)
            {
                Console.Out.WriteLine("-----------------");
                Console.Out.WriteLine(e.Message);
                //HTTPUtils.writeToLog(guid, "ERROR", e.ToString());
            }
        }
        public static void putCache(string topicCustomerGuid, string messageType, string json)
        {
            Console.Out.WriteLine("putCache:" + " messageType:" + messageType + " json:" + json);
            string payload = Uri.EscapeUriString(messageType);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(RunTimeSettings.WebListenerURL + "api/v3/putCache/" + payload);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Accept = "application/json";
            request.ContentLength = json.Length;
            using (Stream webStream = request.GetRequestStream())
            using (StreamWriter requestWriter = new StreamWriter(webStream, System.Text.Encoding.ASCII))
            {
                requestWriter.Write(json);
            }

            try
            {
                WebResponse webResponse = request.GetResponse();
                using (Stream webStream = webResponse.GetResponseStream() ?? Stream.Null)
                using (StreamReader responseReader = new StreamReader(webStream))
                {
                    string response = responseReader.ReadToEnd();
                    Console.Out.WriteLine(response);
                }
            }
            catch (Exception e)
            {
                Console.Out.WriteLine("-----------------");
                Console.Out.WriteLine(e.Message);
                HTTPUtils.writeToLog(topicCustomerGuid, "ERROR", e.ToString());
            }
        }
    }
}

