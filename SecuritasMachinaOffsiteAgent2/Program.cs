// See https://aka.ms/new-console-template for more information
//Console.WriteLine("Hello, World!");
using SecuritasMachinaOffsiteAgent.BO;

Console.WriteLine("Starting Agent v2.1.6");

await new ListenerWorker().startAsync();
//Console.WriteLine("ending");
await Task.Run(() => Thread.Sleep(Timeout.Infinite));