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
        
        public static string SBConnectionString = "";
        public static string? topicNamecustomerGuid;
        internal static dynamic passPhrase;
        internal static string topiccustomerGuid;

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
