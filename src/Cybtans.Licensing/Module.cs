using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Cybtans.Licensing
{
    public abstract class Module
    {
        public Module(string name)
        {
            Name = name;
        }

        public string Name { get; protected set; }

        public void Execute(string[] args)
        {
            var options = CreateOptions(args);
            Execute(options);
        }

        public virtual void PrintHelp() { }
        protected abstract void Execute(ModuleOptions options);
        private ModuleOptions CreateOptions(string[] args)
        {
            var options = new ModuleOptions();

            for (int i = 1; i < args.Length; i++)
            {
                var arg = args[i];
                var value = arg;
                bool isOption = false;
                if (arg.StartsWith("-"))
                {
                    isOption = true;
                    i++;
                    if (i >= args.Length)
                    {
                         throw new InvalidOperationException("Invalid options");                        
                    }

                    value = args[i];
                }

                if (isOption)
                {
                    options[arg] = value;
                }
                else
                {
                    options.PositionalOptions.Add(arg);
                }             
            }

            return options;
        }
    }

    public class ModuleOptions
    {
        Dictionary<string, string> _keyOptions = new Dictionary<string, string>();
        List<string> _positionalOptions  = new List<string>();

        public bool HasKey(string key) => _keyOptions.ContainsKey(key);

        public ICollection<string> PositionalOptions => _positionalOptions;

        public IDictionary<string, string> KeyedOptions => _keyOptions;

        public string this[string key]
        {
            get
            {
                _keyOptions.TryGetValue(key, out var value);
                return value;
            }

            set
            {
                _keyOptions[key] = value;
            }
        }

        public string this[int index]
        {
            get
            {
                return _positionalOptions[index];
            }
            set
            {
                _positionalOptions[index] = null;
            }
        }

        public void ValidateRequired(string key)
        {
            if (!_keyOptions.ContainsKey(key))
            {
                throw new InvalidOperationException($"{key} is required");
            }
        }
    }
}
