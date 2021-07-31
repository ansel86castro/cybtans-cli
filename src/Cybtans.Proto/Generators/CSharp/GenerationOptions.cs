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

        public GraphQLGeneratorOptions GraphQLOptions { get; set; }

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
        private string _interceptorType;

        public bool UseActionInterceptor { get; set; } = true;

        public string InterceptorType
        {
            get => _interceptorType;
            set
            {
                UseActionInterceptor = UseActionInterceptor || !string.IsNullOrEmpty(value);
                _interceptorType = value;
            }
        }

        public string GetInterceptorType()
        {
            return InterceptorType ?? "global::Cybtans.AspNetCore.Interceptors.IMessageInterceptor";
        }
    }

    public class ApiGateWayGeneratorOption : WebApiControllerGeneratorOption
    {
        public GraphQLGeneratorOptions GraphQLOptions { get; set; }
    }

    public class ClientGenerationOptions : TypeGeneratorOption
    {
        public string Prefix { get; set; }

        public string GetClientName(ServiceDeclaration service, ProtoFile proto)
        {
            var ns = Namespace ?? $"{proto.Option.Namespace ?? proto.Filename.Pascal()}.Clients";
            return $"global::{ns}.I{service.Name.Pascal()}Client";
        }
    }

    public class GraphQLGeneratorOptions : TypeGeneratorOption
    {
        public string QueryName { get; set; }

        public bool Explicit { get; set; }

        public bool HandleRequest { get; set; }

        public string InterceptorType { get; set; }

        public string GetInterceptorType()
        {
            return InterceptorType ?? "global::Cybtans.AspNetCore.Interceptors.IMessageInterceptor";
        }
    }
    
}
