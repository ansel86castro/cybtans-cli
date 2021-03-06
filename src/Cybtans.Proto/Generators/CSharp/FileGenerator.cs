#nullable enable

using Cybtans.Proto.AST;
using System;
using System.Collections.Generic;
using System.IO;

namespace Cybtans.Proto.Generators.CSharp
{
   
    public abstract class FileGenerator<T> 
        where T: CodeGenerationOption
    {
        protected T _option;
        private ProtoFile _proto;

        protected FileGenerator(ProtoFile proto, T option)
        {
            _option = option;
            _proto = proto;
        }

        public ProtoFile Proto => _proto;

        public virtual void GenerateCode()
        {
            Directory.CreateDirectory(_option.OutputPath);

            foreach (var item in _proto.ImportedFiles)
            {
                GenerateCode(item);
            }

            GenerateCode(_proto);
        }

        protected virtual void GenerateCode(ProtoFile proto) { }

        public string OuputPath => _option.OutputPath ?? Environment.CurrentDirectory;

        public CsFileWriter CreateWriter(string ns)
        {
            return new CsFileWriter(ns, OuputPath);
        }

        protected void CreateFile(string filename, CodeWriter code)
        {
            File.WriteAllText(filename, code.ToString());
        }
    }

   

}
