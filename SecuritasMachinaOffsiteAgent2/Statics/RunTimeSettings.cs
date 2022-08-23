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
        
        public static string? SBConnectionString = "";
        public static string? topicNamecustomerGuid;
        public static string? passPhrase;
        public static string? topicCustomerGuid;
        public static string? authKey;
        public static string? clientSubscriptionName;
        public static string? controllerTopicName;

        public static string WebListenerURL
        {
            get
            {
                if (Debugger.IsAttached) { return "https://localhost:7074/"; }
                else
                {
                    return "https://securitasmachinacoordinater.azurewebsites.net/";

                }
            }
            set { WebListenerURL = value; }
        }
    }
}
