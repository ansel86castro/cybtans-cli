using Cybtans.Proto.Generator.Licencing;
using System;
using System.IO;

namespace Cybtans.Licensing
{
    public class LicenseModule : Module
    {
        public const string Key = "lic";
        const string privateKey = "<RSAKeyValue><Modulus>r6gKCMfR1bxBjfI8aMNloM+DLWA6k41I+6A8LxHz4Kx7oTh4MSIHR/5CiRoUW83KhBC9X9a9yEMqnmLfjkwbRQ==</Modulus><Exponent>AQAB</Exponent><P>zIVkytbM0KkiOQNENrOwMy4aiJTZxhAAEbmigVtKa3c=</P><Q>2961qaeHqKo5pKSOVTV1EBKZPTigv8CANrDr762iZiM=</Q><DP>jzsISIm/7I3Wmsjv18NlraInEOn1zaDc+4zfAuq1Jqc=</DP><DQ>uN8lPnaIAZDIESDwMMNXH+GTib+Qokq5Q8M1Tv1ffKM=</DQ><InverseQ>AoF/4NXuHOsgA0gZV0iiXk48GbLM4tCKfXTZ5kHtjUg=</InverseQ><D>hUKPcj/dsi/i78QhDj2GwSwM0YTai4w5HSRf5DnTcdNG9Gvr1F9bXYdBzyLMome2TmWeMHwjvaQspbN4sVgr9Q==</D></RSAKeyValue>";

        public LicenseModule(): base(Key)
        {

        }

        public override void PrintHelp()
        {
            Console.WriteLine($"Module {Key} options:");
            Console.WriteLine("-date : The date [Required]");
            Console.WriteLine("-duration : The duration in days [Required]");
            Console.WriteLine("-o : The output file [Required]");
            Console.WriteLine($"Example: {Key} create -date 2021-04-10 -duration 30 -o license.lic");
        }

        protected override void Execute(ModuleOptions options)
        {
            var action = options[0];
            if(action == "create")
            {
                options.ValidateRequired("-date");
                options.ValidateRequired("-duration");

                var date = DateTime.ParseExact(options["-date"], "yyyy-MM-dd", null);
                var licenseService = new LicenseService();

               var license = licenseService.CreateLicense(date, int.Parse(options["-duration"]), privateKey);
                File.WriteAllText(options["-o"], license);
            }
        }
    }
}
