using Cybtans.Proto.Generator.Licencing;
using Cybtans.Services.Security;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cybtans.Licensing
{
    public class EncriptorModule : Module
    {
        public const string Key = "crypto";
        

        public EncriptorModule() : base(Key) { }

        public override void PrintHelp()
        {
            Console.WriteLine($"Module {Key} options:");
            Console.WriteLine("-f : The source filename [Required]");
            Console.WriteLine("-o : The output filename [Required]");
            Console.WriteLine($"Example: {Key} -f config.json -o .config");
        }

        protected override void Execute(ModuleOptions options)
        {
            options.ValidateRequired("-f");
            options.ValidateRequired("-o");

            LicenseService licenceService = new LicenseService();
            licenceService.EncriptFile(options["-f"], options["-o"]);
        }


    }
}
