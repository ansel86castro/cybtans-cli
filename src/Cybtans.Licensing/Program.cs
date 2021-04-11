using System;
using System.Collections.Generic;
using System.Linq;

namespace Cybtans.Licensing
{
    class Program
    {
        static Dictionary<string, Module> _modules = new()
        {
            [EncriptorModule.Key] = new EncriptorModule(),
            [LicenseModule.Key] = new LicenseModule(),
        };

        static void Main(string[] args)
        {
            if (args == null || args.Length == 0)
            {               
                foreach (var item in _modules.Values)
                {
                    Console.WriteLine();
                    item.PrintHelp();
                }
                return;
            }
            
            if(!_modules.TryGetValue(args[0], out var module))
            {
                Console.WriteLine("Module {0} not supported", args[0]);
                return;
            }

            module.Execute(args.Skip(0).ToArray());
        }
    }
}
