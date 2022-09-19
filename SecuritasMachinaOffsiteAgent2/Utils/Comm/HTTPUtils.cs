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

        public async Task writeToLogAsync(string? pAuthKey, string? messageType, string? json)
        {

            await ServiceBusUtils.Instance.postMsg2ControllerAsync("agent/logs", pAuthKey, messageType, json);
        }
        public void writeBackupHistory(string? guid, string? backupFile, string newFileName, long fileLength, long startTimeStamp)
        {


            BackupHistoryDTO backupHistoryDTO = new BackupHistoryDTO();
            backupHistoryDTO.startTimeStamp = startTimeStamp;
            backupHistoryDTO.backupFile = backupFile;
            backupHistoryDTO.fileLength = fileLength;
            backupHistoryDTO.newFileName = newFileName;
            backupHistoryDTO.startTimeStamp = startTimeStamp;
            ServiceBusUtils.Instance.postMsg2ControllerAsync("agent/backupHistory", RunTimeSettings.customerAgentAuthKey, "writeBackupHistory", JsonConvert.SerializeObject(backupHistoryDTO));
        }
        public void touchRepoLastBackup(string? guid, RepoDTO? repoDTO)
        {

            ServiceBusUtils.Instance.postMsg2ControllerAsync("agent/backupHistory", RunTimeSettings.customerAgentAuthKey, "updateRepoBackupStatus", JsonConvert.SerializeObject(repoDTO));
        }
        public void putCache(string topiccustomerGuid, string messageType, string json)
        {

            ServiceBusUtils.Instance.postMsg2ControllerAsync("agent/putCache", topiccustomerGuid, messageType, json);
        }

        internal string getCache(string? customerAuthKey, string v)
        {
            string ret = null;
            try
            {
                string url = "api/v3/getCache/" + Uri.EscapeDataString(v);
                //_client.DefaultRequestHeaders.Add("AuthToken", customerAuthKey);
                HttpResponseMessage response = _client.GetAsync(url).Result;
                response.EnsureSuccessStatusCode();
                ret = response.Content.ReadAsStringAsync().Result;
            }
            catch (Exception ignore) { }
            return ret;
        }

        internal List<RepoDTO> getRepoList(string customerAuthKey)
        {
            try
            {
                string url = "api/v3/repos/list";

                HttpResponseMessage response = _client.GetAsync(url).Result;
                response.EnsureSuccessStatusCode();
                return JsonConvert.DeserializeObject<List<RepoDTO>>(response.Content.ReadAsStringAsync().Result) ;
            }
            catch (Exception ignore) { }
            return new List<RepoDTO>();
        }

        internal void touchRepoLastSync(string? customerAgentAuthKey, RepoDTO repo)
        {
            ServiceBusUtils.Instance.postMsg2ControllerAsync("agent/backupHistory", RunTimeSettings.customerAgentAuthKey, "touchRepoLastSync", JsonConvert.SerializeObject(repo));
        }

        internal List<JobDTO> getWorkerList(string? customerAgentAuthKey)
        {
            try
            {
                string url = "api/v3/job/list";

                HttpResponseMessage response = _client.GetAsync(url).Result;
                response.EnsureSuccessStatusCode();
                
                return JsonConvert.DeserializeObject<List<JobDTO>>(response.Content.ReadAsStringAsync().Result);
            }
            catch (Exception ignore) { }
            return new List<JobDTO>();
        }
    }
}

