using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Statics
{
    public class RunTimeSettings
    {
        public static string? sbrootConnectionString = "";
        public static string? SBConnectionString = "";
       // public static string? topicNamecustomerGuid;
        public static string? passPhrase;
        public static string? customerAuthKey;
        public static string? authKey;
        public static string? clientSubscriptionName;
        public static string? controllerTopicName;
        public static int PollBaseTime=1;
        public static string AppVersion;
        public static string mountedDir = "/mnt/offsite/";
        internal static string? azureBlobEndpoint;
        internal static string? envPassPhrase;
        internal static string? azureBlobContainerName;
        internal static string? azureBlobRestoreContainerName;
        internal static int RetentionDays;
        internal static string serviceBusTopic;

        public static string WebListenerURL
        {
            get
            {
                if (Debugger.IsAttached) { 
                    return "http://localhost:5002/";
                }
                else
                {
                    return "https://cloudbackup.securitasmachina.com/";
                   // return "https://securitasmachinacoordinater.azurewebsites.net/";

                }
            }
            set { WebListenerURL = value; }
        }
    }
}
