using Common.Statics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

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
            HttpResponseMessage response = client.GetAsync("/v2/config/" + pCustomerGuid).Result;
            response.EnsureSuccessStatusCode();
            string result = response.Content.ReadAsStringAsync().Result;
            dynamic stuff = JsonConvert.DeserializeObject(result);
            RunTimeSettings.passPhrase = stuff.passPhrase;
            RunTimeSettings.SBConnectionString = stuff.ServiceBusEndPoint;
            RunTimeSettings.topicName = stuff.topicName;


        }
    }
}
