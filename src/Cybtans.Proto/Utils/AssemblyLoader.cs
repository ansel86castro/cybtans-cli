using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cybtans.Proto.Utils
{
    public class AssemblyLoader
    {
        Dictionary<string, PackageInfo> _packageInfos = new Dictionary<string, PackageInfo>();
        private string _dir;
        private string _nugetDir;
        private string _filename;

        public AssemblyLoader(string filename)
        {
            _filename = filename;
            _nugetDir = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
            if(_nugetDir == null)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _nugetDir = $"{Environment.GetEnvironmentVariable("userprofile")}\\.nuget\\packages";
                }
                else
                {
                    _nugetDir = "/.nuget/packages";
                }                               
            }

            _dir = Path.GetDirectoryName(filename);
            var name = Path.GetFileNameWithoutExtension(filename);
            var depsFilename = Path.Combine(_dir, $"{name}.deps.json");
            var depsJson = File.ReadAllText(depsFilename);

            dynamic data = (JObject)JsonConvert.DeserializeObject(depsJson);
            var runtime = data.runtimeTarget.name.ToString();
            var packages = (JObject)((JObject)data.targets).Properties().Where(x=>x.Name == runtime).FirstOrDefault()?.Value;            
            foreach (var pk in packages.Properties())
            {
                var split = pk.Name.Split("/");
                PackageInfo packageInfo = new PackageInfo
                {
                    Name = split[0],
                    Version = split[1],
                    FullName = pk.Name
                };

                var pkValue = (JObject)pk.Value;
                if (pkValue.ContainsKey("runtime"))
                {
                    foreach (var p in ((JObject)pkValue["runtime"]).Properties())
                    {
                        var value = (JObject)p.Value;
                        if (value.ContainsKey("assemblyVersion"))
                        {
                            packageInfo.Runtimes[value["assemblyVersion"].ToString()] = p.Name;
                        }
                        else
                        {
                            packageInfo.Runtimes[packageInfo.Version] = p.Name;
                        }
                    }

                    _packageInfos.Add(packageInfo.Name, packageInfo);
                }
            }

            var libraries = (JObject)data.libraries;
            foreach (var p in libraries.Properties())
            {
                var value = (JObject)p.Value;
                var split = p.Name.Split("/");
                if (_packageInfos.TryGetValue(split[0], out var info))
                {
                    if (value["type"].ToString() == "package")
                    {
                        info.Path = value["path"].ToString();
                    }
                }
            }
        }

        public IEnumerable<Type> LoadTypes()
        {
            AppDomain.CurrentDomain.AssemblyResolve += LoadAssemblyDelegate;

            var assembly = Assembly.Load(File.ReadAllBytes(_filename));
            var types =  assembly.ExportedTypes.ToList();

            AppDomain.CurrentDomain.AssemblyResolve -= LoadAssemblyDelegate;

            return types;
        }

        Assembly LoadAssemblyDelegate(object sender, ResolveEventArgs args)
        {
            var split = args.Name.Split(",");            
            var name = split[0];
            var version = split[1].Split("=")[1];

            if(!_packageInfos.TryGetValue($"{name}", out var pi) || !pi.Runtimes.ContainsKey(version))
            {
                string assemplyPath = Path.Combine(_dir, name + ".dll");
                if (!File.Exists(assemplyPath))
                    return null;

                return Assembly.Load(File.ReadAllBytes(assemplyPath));
            }

            var runtime = pi.Runtimes[version];
            return Assembly.Load(File.ReadAllBytes(Path.Combine(_nugetDir, pi.Path ,runtime)));
        }
    }

    public class PackageInfo
    {
        public string Name { get; set; }

        public string Version { get; set; }

        public string FullName { get; set; }

        public string Path { get; set; }

        public Dictionary<string, string> Runtimes { get; set; } = new Dictionary<string, string>();        
    }


}
