// See https://aka.ms/new-console-template for more information
//Console.WriteLine("Hello, World!");
using SecuritasMachinaOffsiteAgent.BO;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

Console.WriteLine("Starting Agent v2.1.6");
ServicePointManager.ServerCertificateValidationCallback =
               delegate (object sender, X509Certificate certificate, X509Chain
    chain, SslPolicyErrors sslPolicyErrors)
               {
                   return true;
               };
await new ListenerWorker().startAsync();
//Console.WriteLine("ending");
await Task.Run(() => Thread.Sleep(Timeout.Infinite));