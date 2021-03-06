using System.Collections.Generic;
using System.IO;
using System.Net.Http.Headers;

namespace Cybtans.Proto.Generator
{
    public class CybtansConfig
    {
        public string Path { get; private set; }

        public string Service { get; set; }       

        public List<GenerationStep> Steps { get; set; }

        public static IEnumerable<CybtansConfig> SearchConfigs(string path)
        {
            return SearchConfigs(new DirectoryInfo(path));
        }

        public static IEnumerable<CybtansConfig> SearchConfigs(DirectoryInfo di)
        {           
            foreach (var file in di.EnumerateFiles())
            {
                if(file.Name == "cybtans.json")
                {
                    var json = File.ReadAllText(file.FullName);
                    var config = System.Text.Json.JsonSerializer.Deserialize<CybtansConfig>(json);
                    config.Path = di.FullName;

                    yield return config;
                }
            }

            foreach (var dir in di.EnumerateDirectories())
            {
                foreach (var item in SearchConfigs(dir))
                {
                    yield return item;
                }
            }
        }
    }

    public class GenerationStep
    {
        public string Type { get; set; }

        public string Output { get; set; }

        #region Proto Generator

        public string ProtoFile { get; set; }

        public string SearchPath { get; set; }

        public bool ClearOutput { get; set; } = true;

        public StepClientOptions Typecript { get; set; }        

        public CSharpModelGenerationOption Models { get; set; }

        public CSharpServiceGenerationOption Services { get; set; }

        public CSharpClientStepOptions CSharpClients { get; set; }

        public CSharpControllerGenerationOption Controllers { get; set; }

        public CSharpGatewayGenerationOptions ApiGateway { get; set; }

        public List<StepClientOptions> Clients { get; set; } = new List<StepClientOptions>();

        #endregion

        #region Message Generator
        public string AssemblyFile { get; set; }

        public string[] Imports { get;  set; }

        public GrpcCompatibility Grpc { get; set; } = new GrpcCompatibility();       
        
        public string NameTemplate { get; set; }

        public bool  GenerateGraphQLQuery { get; set; }

        public string AutoMapperOutput { get; set; }

        public bool GenerateMappings { get; set; }
        public bool GenerateAutoMapperProfile { get; set; }

        #endregion
    }

    
    public class StepOption
    {
        public string Output { get; set; }       
        
        public bool ClearOutput { get; set; }
    }

    public class CSharpStepOption : StepOption
    {
        public string Namespace { get; set; }

        public bool Generate { get; set; } = true;
    }
   

    public class CSharpModelGenerationOption : CSharpStepOption
    {
        public bool UseCytansSerialization { get; set; } = true;

        public bool UseRecords { get; set; }
    }

    public class CSharpServiceGenerationOption: CSharpStepOption
    {
        public string NameTemplate { get; set; }

        public GrpcProxy Grpc { get; set; }

        public GraphQLStepOptions GraphQL { get; set; }


        public class GrpcProxy: CSharpStepOption
        {
            public bool AutoRegister { get; set; }
         
        }
    }

    public class CSharpControllerGenerationOption : CSharpStepOption
    {
        public bool UseActionInterceptor { get; set; }

        public string InterceptorType { get; set; }
    }

    public class CSharpGatewayGenerationOptions: CSharpControllerGenerationOption
    {
        public GraphQLStepOptions GraphQL { get; set; }

    }

    public class CSharpClientStepOptions: CSharpStepOption
    {
        public string Prefix { get; set; }
    }

    public class GraphQLStepOptions : CSharpStepOption
    {
        public bool Explicit { get; set; }

        public string QueryName { get; set; }
        public bool HandleRequest { get; set; }

        public string InterceptorType { get; set; }

        public bool UseInterceptor
        {
            get { return InterceptorType != null; }
            set
            {
                InterceptorType = value ? "global::Cybtans.AspNetCore.Interceptors.IMessageInterceptor" : null;
            }
        }
    }

    public class StepClientOptions : StepOption
    {        
        public string Framework { get; set; }

        public string Prefix { get; set; }

        public Dictionary<string, string> Options { get; set; } = new Dictionary<string, string>();
    }

    public class GrpcCompatibility
    {
        public bool Enable { get; set; }

        public string MappingOutput { get; set; }

        public string MappingNamespace { get; set; }

        public string GrpcNamespace { get; set; }

    }


}
