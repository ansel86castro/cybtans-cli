#nullable enable

using Cybtans.Proto.AST;

namespace Cybtans.Proto.Options
{
    public class FileOptions : ProtobufOption
    {
        string? _namespace;
        string? _csharpNamespace;

        public FileOptions() : base(OptionsType.File) { }

        [Field("namespace")]
        public string? Namespace { get => _namespace ?? _csharpNamespace; set => _namespace = value; }


        [Field("csharp_namespace")]
        public string? CSharpNamespace { get => _csharpNamespace ?? _namespace; set => _csharpNamespace = value; }

    }
}
