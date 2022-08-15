// See https://aka.ms/new-console-template for more information
//Console.WriteLine("Hello, World!");
using SecuritasMachinaOffsiteAgent.BO;

Console.WriteLine("Starting v2.1.4");

new ListenerWorker().startAsync();
//Console.WriteLine("ending");
await Task.Run(() => Thread.Sleep(Timeout.Infinite));