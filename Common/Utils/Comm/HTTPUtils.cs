using Common.Statics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Common.Utils.Comm
{
    public class HTTPUtils
    {
        //private static HttpClient client = null;
        public static string ProvisionUser( object model)
        {
            
            try
            {
                string serializedJson = JsonConvert.SerializeObject(model);

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(RunTimeSettings.WebListenerURL + "api/v3/provisionUser");
                request.Method = "POST";
                request.ContentType = "application/json";
               // request.Accept = "application/json";
                request.ContentLength = serializedJson.Length;
                using (Stream webStream = request.GetRequestStream())
                using (StreamWriter requestWriter = new StreamWriter(webStream, System.Text.Encoding.ASCII))
                {
                    requestWriter.Write(serializedJson);
                }


                WebResponse webResponse = request.GetResponse();
                using (Stream webStream = webResponse.GetResponseStream() ?? Stream.Null)
                using (StreamReader responseReader = new StreamReader(webStream))
                {
                    string response = responseReader.ReadToEnd();
                    response = response.Replace("\"", "");
                    //_logger.LogDebug("model.newGuid " + response);
                    return response;
                    //Console.Out.WriteLine(response);
                }
            }
            catch (Exception e)
            {
                Console.Out.WriteLine("-----------------");
                Console.Out.WriteLine(e.Message);
                //_logger.LogError(e.ToString());
                //HTTPUtils.writeToLog(guid, "ERROR", e.ToString());
            }
            return null;
        }
    }
}
