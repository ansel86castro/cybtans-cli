namespace Cybtans.Proto.Generators.Typescript
{
    public class TsOutputOption: CodeGenerationOption
    {
        public const string FRAMEWORK_ANGULAR = "angular";

        public string Framework { get; set; }

        public string Filename { get; set; }        
    }

    public class TsClientOptions : TsOutputOption
    {
        public string Prefix { get; set; }
    }
}
