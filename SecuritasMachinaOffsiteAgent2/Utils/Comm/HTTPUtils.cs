using Common.DTO.V2;
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
        private static HTTPUtils? instance;
        private static HttpClient _client = null;
        public static HTTPUtils Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new HTTPUtils();
                }
                return instance;
            }
        }
        private HTTPUtils()
        {

            if (_client == null)
            {
                HttpClientHandler handler = new HttpClientHandler()
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                };
                _client = new HttpClient(handler);
            }
            _client.BaseAddress = new Uri(RunTimeSettings.WebListenerURL);
        }
        public void populateRuntime(string pcustomerGuid)
        {

            HttpResponseMessage response = _client.GetAsync("api/v2/config/" + pcustomerGuid).Result;
            response.EnsureSuccessStatusCode();
            string result = response.Content.ReadAsStringAsync().Result;
            dynamic stuff = JsonConvert.DeserializeObject(result);
             
            RunTimeSettings.SBConnectionString = stuff.serviceBusEndPoint;
            RunTimeSettings.topicNamecustomerGuid = stuff.topicName;
            RunTimeSettings.topicCustomerGuid = pcustomerGuid;
            RunTimeSettings.authKey=stuff.authKey;
            RunTimeSettings.controllerTopicName = stuff.controllerTopicName;
            RunTimeSettings.clientSubscriptionName = stuff.clientSubscriptionName;


        }

        public void writeToLog(string? guid, string? messageType, string? json)
        {
            if (json == null)
                json = "empty msg";
            string serializedJson = JsonConvert.SerializeObject(json);
            Console.WriteLine($"writeToLog: guid:{guid} messageType:{messageType} json:{json}");
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(RunTimeSettings.WebListenerURL + "api/v3/putLog/" + RunTimeSettings.topicCustomerGuid + "/" + messageType);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Accept = "application/json";
            request.ContentLength = serializedJson.Length;
            request.Headers.Add("AuthKey", RunTimeSettings.authKey);
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
                //HTTPUtils.instance.writeToLog(guid, "ERROR", e.ToString());
            }
        }
        public void writeBackupHistory(string? guid, string? backupFile, string newFileName, long fileLength, long startTimeStamp)
        {

            //string serializedJson = JsonConvert.SerializeObject(json);
            //Debug.WriteLine($"writeToLog: guid:{guid} backupFile:{backupFile} json:{json}");
            string url = RunTimeSettings.WebListenerURL + "api/v3/postBackupHistory/" + RunTimeSettings.topicCustomerGuid + "/" + Uri.EscapeUriString(backupFile) + "/" + Uri.EscapeUriString(newFileName) + "/" + fileLength + "/" + startTimeStamp;
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.AutomaticDecompression = DecompressionMethods.GZip;
                request.Headers.Add("AuthKey", RunTimeSettings.authKey);
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    string html = reader.ReadToEnd();
                    //Console.Out.WriteLine(html);
                }

            }
            catch (Exception e)
            {
                Console.Out.WriteLine("-----------------");
                Console.Out.WriteLine(e.Message);
                writeToLog(RunTimeSettings.topicCustomerGuid, "ERROR", e.ToString());
                //HTTPUtils.instance.writeToLog(guid, "ERROR", e.ToString());
            }
        }
        public void putCache(string topiccustomerGuid, string messageType, string json)
        {
            //Console.Out.WriteLine("putCache:" + " messageType:" + messageType + " json:" + json);
            try
            {
                string payload = Uri.EscapeUriString(messageType);
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(RunTimeSettings.WebListenerURL + "api/v3/putCache/" + payload);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Accept = "application/json";
                request.ContentLength = json.Length;
                request.Headers.Add("AuthKey", RunTimeSettings.authKey);

                using (Stream webStream = request.GetRequestStream())
                using (StreamWriter requestWriter = new StreamWriter(webStream, System.Text.Encoding.ASCII))
                {
                    requestWriter.Write(json);
                }


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
                writeToLog(topiccustomerGuid, "ERROR", e.ToString() + "Parameters: " + $"{topiccustomerGuid},  {messageType},  {json}");
            }
        }

        public void sendAgentDir(string topiccustomerGuid, DirListingDTO dirListingDTO1)
        {
            try
            {
                var stringContent = new StringContent(JsonConvert.SerializeObject(dirListingDTO1), Encoding.UTF8, "application/json");
                _client.DefaultRequestHeaders.Add("AuthKey", RunTimeSettings.authKey);
                HttpResponseMessage response = _client.PutAsync("/api/v3/agentDir/" + topiccustomerGuid, stringContent).Result;
                response.EnsureSuccessStatusCode();
                string result = response.Content.ReadAsStringAsync().Result;
                dynamic stuff = JsonConvert.DeserializeObject(result);
            }
            catch (Exception ignore) { }
            // RunTimeSettings.passPhrase = stuff.passPhrase;
        }
    }
}

