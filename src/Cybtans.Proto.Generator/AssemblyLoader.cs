using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft;
using Newtonsoft.Json;

namespace Cybtans.Proto.Generator
{
    public class AssemblyLoader
    {
        public static Assembly Load(string filename)
        {
            var dir = Path.GetDirectoryName(filename);
            var name = Path.GetFileNameWithoutExtension(filename);
            var depsFilename = Path.Combine(dir, $"{name}.deps.json");
            var depsJson = File.ReadAllText(depsFilename);

            var data = JsonConvert.DeserializeObject(depsJson);

            var assembly = Assembly.Load(File.ReadAllBytes(filename));

            return assembly;
        }
    }

   
}
