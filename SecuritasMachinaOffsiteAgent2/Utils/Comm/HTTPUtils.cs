using Common.DTO.V2;
using Common.Statics;
using Newtonsoft.Json;
using SecuritasMachinaOffsiteAgent.BO;
using SecuritasMachinaOffsiteAgent.DTO.V2;
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
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                };
                _client = new HttpClient(handler);
            }
            _client.BaseAddress = new Uri(RunTimeSettings.WebListenerURL);
        }
        public void populateRuntime(string pAuthToken)
        {
            long loopCount = 0;
            while (loopCount < 100)
            {
                loopCount++;
                try
                {
                    RunTimeSettings.AppVersion = VersionUtil.getAppVersion();
                    string url = "api/v3/config/" + Uri.EscapeDataString(RunTimeSettings.AppVersion);
                    _client.DefaultRequestHeaders.Add("AuthToken", pAuthToken);
                    HttpResponseMessage response = _client.GetAsync(url).Result;
                    response.EnsureSuccessStatusCode();
                    string result = response.Content.ReadAsStringAsync().Result;
                    dynamic stuff = JsonConvert.DeserializeObject(result);

                    RunTimeSettings.SBConnectionString = stuff.serviceBusEndPoint;
                    RunTimeSettings.sbrootConnectionString = stuff.sbrootConnectionString;

                    //RunTimeSettings.topicNamecustomerGuid = stuff.topicName;
                    //RunTimeSettings.topicCustomerGuid = pcustomerGuid;
                    RunTimeSettings.serviceBusTopic = stuff.serviceBusTopic;
                    RunTimeSettings.PollBaseTime = stuff.PollBaseTime == null ? (int)1 : (int)stuff.PollBaseTime;
                    RunTimeSettings.clientSubscriptionName = stuff.clientSubscriptionName;
                    if (RunTimeSettings.envPassPhrase == null && RunTimeSettings.googleStorageBucketName == null)
                    {
                        RunTimeSettings.azureBlobEndpoint = stuff.azureBlobEndpoint;
                        RunTimeSettings.azureSourceBlobContainerName = stuff.azureSourceBlobContainerName;
                        RunTimeSettings.azureBlobRestoreContainerName = stuff.azureBlobRestoreContainerName;
                        RunTimeSettings.googleStorageBucketName = stuff.googleStorageBucketName;
                        RunTimeSettings.encryptionPassPhrase = stuff.encryptionPassPhrase;
                        RunTimeSettings.RetentionDays = stuff.retentionDays == null ? (int)45 : (int)stuff.retentionDays;
                        RunTimeSettings.MaxThreads = stuff.maxThreads == null ? (int)5 : (int)stuff.maxThreads;

                    }
                    break;
                }
                catch (Exception ex)
                {
                    Console.Out.WriteLine($"Error connecting to Node, Attempt #{loopCount} OF 100, retrying.." + ex.ToString());
                    Thread.Sleep(30 * 1000);
                }
            }
        }

        public void writeToLog(string? pAuthKey, string? messageType, string? json)
        {
           
            ServiceBusUtils.postMsg2ControllerAsync("agent/logs", pAuthKey, messageType, json);
        }
        public void writeBackupHistory(string? guid, string? backupFile, string newFileName, long fileLength, long startTimeStamp)
        {

            //string serializedJson = JsonConvert.SerializeObject(json);
            //Debug.WriteLine($"writeToLog: guid:{guid} backupFile:{backupFile} json:{json}");
            /* string url = RunTimeSettings.WebListenerURL + "api/v3/postBackupHistory/" + RunTimeSettings.topicCustomerGuid + "/" + Uri.EscapeUriString(backupFile) + "/" + Uri.EscapeUriString(newFileName) + "/" + fileLength + "/" + startTimeStamp;
             try
             {
                 HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);Uri
                 request.AutomaticDecompression = DecompressionMethods.GZip;
                 request.Headers.Add("AuthToken", RunTimeSettings.authKey);
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
            */
            BackupHistoryDTO backupHistoryDTO = new BackupHistoryDTO();
            backupHistoryDTO.startTimeStamp = startTimeStamp;
            backupHistoryDTO.backupFile = backupFile;
            backupHistoryDTO.fileLength = fileLength;
            backupHistoryDTO.newFileName = newFileName;
            backupHistoryDTO.startTimeStamp = startTimeStamp;
            ServiceBusUtils.postMsg2ControllerAsync("agent/backupHistory", RunTimeSettings.customerAuthKey, "writeBackupHistory", JsonConvert.SerializeObject(backupHistoryDTO));
        }
        public void putCache(string topiccustomerGuid, string messageType, string json)
        {

            ServiceBusUtils.postMsg2ControllerAsync("agent/putCache", topiccustomerGuid, messageType, json);
        }


    }
}

