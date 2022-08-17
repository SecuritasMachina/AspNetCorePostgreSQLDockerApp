﻿using Common.Statics;
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
        public static void SendMessage(string messageType,string json)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(RunTimeSettings.WebListenerURL+ "/v3/putCache/"+ messageType);
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
            }
        }
    }
}
