// See https://aka.ms/new-console-template for more information

using SecuritasMachinaOffsiteAgent.BO;

        Console.WriteLine("Starting v2.1.2");

        new ListenerWorker().startAsync();
        Console.WriteLine("ending");
   