using Cybtans.Proto.AST;
using Cybtans.Proto.Utils;

namespace Cybtans.Proto.Generators.CSharp
{
    public class GenerationOptions
    {        
        public ModelGeneratorOptions ModelOptions { get; set; }

        public ServiceGeneratorOptions ServiceOptions { get; set; }

        public WebApiControllerGeneratorOption ControllerOptions { get; set; }

        public ClientGenerationOptions ClientOptions { get; set; }

        public ApiGateWayGeneratorOption ApiGatewayOptions { get; set; }

        public GraphQLGeneratorOptions GraphQLOptions { get; set; }
    }

    public class ModelGeneratorOptions: TypeGeneratorOption
    {
        public bool GenerateAccesor { get; set; } = true;
    }

    public class ServiceGeneratorOptions: TypeGeneratorOption
    {
        string _implementationNs;
        private string _implementationOutput;

        public string? NameTemplate { get; set; }

        public bool AutoRegisterImplementation { get; set; }

        public string ImplementationNamespace
        {
            get
            {
                return _implementationNs ?? Namespace;
            }
            set
            {
                _implementationNs = value;
            }
        }
        
        public string ImplementationOutput
        {
            get
            {
                return _implementationOutput ?? OutputPath;
            }
            set
            {
                _implementationOutput = value;
            }
        }

        public string GetInterfaceName(ServiceDeclaration service)
        {
            if (NameTemplate != null)
            {
                return $"I{TemplateProcessor.Process(NameTemplate, new { Name = service.Name.Pascal() })}";
            }

            return $"I{service.Name.Pascal()}";
        }
    }

    public class WebApiControllerGeneratorOption: TypeGeneratorOption
    {
        public bool UseActionInterceptor { get; set; } = true;
    }

    public class ApiGateWayGeneratorOption : WebApiControllerGeneratorOption
    {

    }

    public class ClientGenerationOptions : TypeGeneratorOption
    {
        public string Prefix { get; set; }
    }

    public class GraphQLGeneratorOptions : TypeGeneratorOption
    {
        public string QueryName { get; set; }

        public bool Explicit { get; set; }
    }
}
