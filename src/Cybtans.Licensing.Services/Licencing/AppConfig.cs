using System;
using System.IO;
using System.Reflection;

namespace Cybtans.Proto.Generator.Licencing
{
    public class AppConfig
    {
        public string Id { get; set; }

        public DateTime? StartExecutionTime { get; set; }
        public DateTime? LastExecutionTime { get; set; }

        public string LicenceId { get; set; }

       public string LicenseFilename { get; set; }
     
    }
}
