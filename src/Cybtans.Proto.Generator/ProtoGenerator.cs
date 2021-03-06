using Cybtans.Proto.AST;
using Cybtans.Proto.Generators.CSharp;
using Cybtans.Proto.Generators.Typescript;
using Cybtans.Proto.Utils;
using System;
using System.IO;

namespace Cybtans.Proto.Generator
{
    public class ProtoGenerator : IGenerator
    {
        public bool CanGenerate(string value)
        {
            return value == "proto" || value == "p";
        }

        public bool Generate(string[] args)
        {
            if (args == null || args.Length == 0 || !CanGenerate(args[0]))
                return false;
            
            GenerateProto(args);
            return true;
        }

        public bool Generate(CybtansConfig config, GenerationStep step)
        {
            if (!CanGenerate(step.Type))
                return false;          

            var protoFile = Path.Combine(config.Path, step.ProtoFile);

            if (!string.IsNullOrEmpty(step.SearchPath))
            {
                if (!Path.IsPathFullyQualified(step.SearchPath))
                {
                    step.SearchPath = Path.Combine(config.Path, step.SearchPath);
                }
            }
            else if(config.Path != null)
            {
                step.SearchPath = config.Path;
            }
            else
            {
                step.SearchPath = Environment.CurrentDirectory;
            }
            

            var fileResolverFactory = new SearchPathFileResolverFactory(step.SearchPath.Split(",", StringSplitOptions.RemoveEmptyEntries| StringSplitOptions.TrimEntries));

            Console.WriteLine($"Compiling {protoFile}");

            Proto3Generator generator = new Proto3Generator(fileResolverFactory);
            var (ast, scope) = generator.LoadFromFile(protoFile);

            var options = new GenerationOptions()
            {
                ClearOutputs = step.ClearOutput,
                ModelOptions = ast.HaveMessages ?  new ModelGeneratorOptions()
                {
                    OutputPath = Path.Combine(config.Path, step.Models?.Output ?? $"{step.Output}/{config.Service}.Models"),
                    Namespace = step.Models?.Namespace,
                    GenerateAccesor = step.Models?.UseCytansSerialization ?? true,
                    useRecords = step.Models?.UseRecords ?? false
                } : null,

                ServiceOptions = ast.HaveServices ? new ServiceGeneratorOptions()
                {
                    OutputPath = Path.Combine(config.Path, step.Services?.Output ?? $"{step.Output}/{config.Service}.Services/Generated"),
                    Namespace = step.Services?.Namespace,
                    NameTemplate = step.Services?.NameTemplate,
                    AutoRegisterImplementation = step.Services?.Grpc?.AutoRegister ?? false,
                    ImplementationNamespace = step.Services?.Grpc?.Namespace,
                    ImplementationOutput = Path.Combine(config.Path, step.Services?.Grpc?.Output ?? $"{step.Output}/{config.Service}.Services/Generated")                   
                } : null,                               
            };

            if(options.ServiceOptions != null)
            {
                if (step.Services?.GraphQL != null && step.Services.GraphQL.Generate)
                {
                    options.ServiceOptions.GraphQLOptions = new GraphQLGeneratorOptions
                    {
                        OutputPath = Path.Combine(config.Path, step.Services.GraphQL?.Output ?? $"{step.Output}/{config.Service}.WebApi/Generated/GraphQL"),
                        Namespace = step.Services.GraphQL.Namespace ?? $"{config.Service}.GraphQL",
                        QueryName = step.Services.GraphQL.QueryName ?? $"{ast.Filename.Pascal()}QueryDefinitions",
                        Explicit = step.Services.GraphQL.Explicit,
                        HandleRequest = step.Services.GraphQL.HandleRequest || (step.Controllers?.UseActionInterceptor ?? false),
                        InterceptorType = step.Services.GraphQL.InterceptorType ?? (step.Controllers?.InterceptorType)
                    };
                }

                options.ControllerOptions = new WebApiControllerGeneratorOption()
                {
                    OutputPath = Path.Combine(config.Path, step.Controllers?.Output ?? $"{step.Output}/{config.Service}.WebApi/Generated/Controllers"),
                    Namespace = step.Controllers?.Namespace,
                    UseActionInterceptor = step.Controllers?.UseActionInterceptor ?? false,
                    InterceptorType = step.Controllers?.InterceptorType
                };

                if (step.CSharpClients?.Generate ?? false)
                {
                    options.ClientOptions = new ClientGenerationOptions()
                    {
                        OutputPath = Path.Combine(config.Path, step.CSharpClients?.Output ?? $"{step.Output}/{config.Service}.Clients"),
                        Namespace = step.CSharpClients?.Namespace,
                        Prefix = step.CSharpClients?.Prefix
                    };
                }
            }

            if (step.ApiGateway != null)
            {
                options.ApiGatewayOptions = new ApiGateWayGeneratorOption
                {
                    OutputPath = Path.Combine(config.Path, step.ApiGateway?.Output),
                    Namespace = step.ApiGateway?.Namespace ?? $"{config.Service}.Controllers",
                    UseActionInterceptor = step.ApiGateway?.UseActionInterceptor ?? false,
                    InterceptorType = step.ApiGateway?.InterceptorType
                };

                if(step.ApiGateway?.GraphQL != null && step.ApiGateway.Generate)
                {
                    var gateway = step.ApiGateway;
                    options.ApiGatewayOptions.GraphQLOptions =  new GraphQLGeneratorOptions
                    {
                        OutputPath = Path.Combine(config.Path, gateway.GraphQL?.Output ?? $"{step.ApiGateway?.Output}"),
                        Namespace = gateway.GraphQL.Namespace ?? $"{options.ApiGatewayOptions.Namespace}.GraphQL",
                        QueryName = gateway.GraphQL.QueryName ?? $"GraphQLQueryDefinitions",
                        Explicit = gateway.GraphQL.Explicit
                    };
                }
            }         

            MicroserviceGenerator microserviceGenerator = new MicroserviceGenerator(options);            
            microserviceGenerator.GenerateCode(ast, scope);            
            try
            {
                Console.ForegroundColor = ConsoleColor.Green;

                Console.WriteLine("CSharp generated succesfully");

                if (step.Typecript != null)
                {

                    GenerateTypecriptCode(ast, Path.Combine(config.Path, step.Typecript.Output), step.Typecript.Framework);

                    Console.WriteLine($"Typescript generated succesfully");
                }

                if (step.Clients != null)
                {
                    foreach (var option in step.Clients)
                    {
                        GenerateClient(ast, config, step, option);
                        Console.WriteLine($"{option.Framework} client generated succesfully");
                    }
                }
            }

            finally
            {
                Console.ResetColor();
            }

            return true;
        }

       
        public void PrintHelp()
        {
            Console.WriteLine("Proto Generator options:");
            Console.WriteLine("Example: cybtans-cli proto -n Service1 -o ./Services/Service1 -f ./Protos/Service1.proto");
            Console.WriteLine("p|proto : Generate code from a proto file");
            Console.WriteLine("-n : Service Name");
            Console.WriteLine("-o : Output Directory");
            Console.WriteLine("-f : Proto filename");
            Console.WriteLine("-search-path : Search path for imports");
            Console.WriteLine("-models-o : Models output directory");
            Console.WriteLine("-models-ns : Models namespace");
            Console.WriteLine("-services-o : Services output directory");
            Console.WriteLine("-services-ns : Services namespace");
            Console.WriteLine("-controllers-o : Controllers output directory");
            Console.WriteLine("-controllers-ns : Controllers namespace");
            Console.WriteLine("-cs-clients-o : CSharp clients output directory");
            Console.WriteLine("-cs-clients-ns : CSharp clients namespace");
            Console.WriteLine("-ts-o : Typescript code output directory");
            Console.WriteLine("-ts-fr : Typescript framework, for axample -ts-fr angular");
            Console.WriteLine("-o-gateway : Generate Api Gateway");
        }

        private static void GenerateProto(string[] args)
        {
            var options = new GenerationOptions()
            {
                ModelOptions = new ModelGeneratorOptions(),
                ServiceOptions = new ServiceGeneratorOptions(),
                ControllerOptions = new WebApiControllerGeneratorOption(),
                ClientOptions = new ClientGenerationOptions()
            };

            string protoFile = null;
            string searchPath = null;
            string name = null;
            string output = null;
            string tsOutput = null;
            string tsFramework = null;

            for (int i = 1; i < args.Length; i++)
            {
                var arg = args[i];
                var value = arg;
                if (arg.StartsWith("-"))
                {
                    i++;
                    if (i >= args.Length)
                    {
                        Console.WriteLine("Invalid options");
                        return;
                    }

                    value = args[i];
                }

                switch (arg)
                {
                    case "-n":
                        name = value;
                        break;
                    case "-o":
                        output = value;
                        break;
                    case "-models-o":
                        options.ModelOptions.OutputPath = value;
                        break;
                    case "-models-ns":
                        options.ModelOptions.Namespace = value;
                        break;
                    case "-services-o":
                        options.ServiceOptions.OutputPath = value;
                        break;
                    case "-services-ns":
                        options.ServiceOptions.Namespace = value;
                        break;
                    case "-controllers-o":
                        options.ControllerOptions.OutputPath = value;
                        break;
                    case "-controllers-ns":
                        options.ControllerOptions.Namespace = value;
                        break;
                    case "-cs-clients-o":
                        options.ClientOptions.OutputPath = value;
                        break;
                    case "-cs-clients-ns":
                        options.ClientOptions.Namespace = value;
                        break;
                    case "-ts-o":
                        tsOutput = value;
                        break;
                    case "-ts-fr":
                        tsFramework = value;
                        break;
                    case "-f":
                        protoFile = value;
                        break;
                    case "-search-path":
                        searchPath = value;
                        break;
                    case "-o-gateway":
                        options.ApiGatewayOptions = new ApiGateWayGeneratorOption { OutputPath = value };
                        break;
                    default:
                        Console.WriteLine("Invalid Option");
                        break;
                }
            }

            if (searchPath == null)
            {
                searchPath = Path.GetDirectoryName(protoFile);
            }

            if (string.IsNullOrEmpty(searchPath))
            {
                searchPath = Environment.CurrentDirectory;
            }

            if (protoFile == null)
            {
                Console.WriteLine("Missing proto file");
                return;
            }

            var fileResolverFactory = new SearchPathFileResolverFactory(new string[] { searchPath });

            Console.WriteLine($"Compiling {protoFile}");

            Proto3Generator generator = new Proto3Generator(fileResolverFactory);
            var (ast, scope) = generator.LoadFromFile(protoFile);

            if (name != null && output != null)
            {
                options.ModelOptions.OutputPath = $"{output}/{name}.Models";
                options.ServiceOptions.OutputPath = $"{output}/{name}.Services/Generated";
                options.ControllerOptions.OutputPath = $"{output}/{name}.WebApi/Controllers/Generated";
                options.ControllerOptions.Namespace = "WebApi.Controllers";
                options.ClientOptions.OutputPath = $"{output}/{name}.Clients";

                if(options.ApiGatewayOptions != null)
                    options.ApiGatewayOptions.Namespace = $"Gateway.Controllers.{name.Pascal()}";
            }

            if (options.ModelOptions.OutputPath != null)
            {
                MicroserviceGenerator microserviceGenerator = new MicroserviceGenerator(options);

                Console.WriteLine($"Generating csharp code from {protoFile}");

                microserviceGenerator.GenerateCode(ast, scope);

                Console.WriteLine("Csharp code generated succesfully");
            }

            if(tsOutput != null)
            {
                Console.WriteLine($"Generating typescript code from {protoFile}");

                GenerateTypecriptCode(ast, tsOutput, tsFramework);

                Console.WriteLine($"Typescript code generated succesfully in {tsOutput}");
            }
        }

        private static void GenerateTypecriptCode(ProtoFile ast, string output, string framework)
        {
            TypescriptGenerator tsGenerator = new TypescriptGenerator(new TypescriptOptions
            {
                ModelOptions = new TsOutputOption
                {
                    OutputPath = output,
                },
                ClientOptions = new TsClientOptions
                {
                    OutputPath = output,
                    Framework = framework
                }
            });

            tsGenerator.GenerateCode(ast);
        }

        private void GenerateClient(ProtoFile ast, CybtansConfig config, GenerationStep step, StepClientOptions option)
        {
            var output = Path.Combine(config.Path, option.Output);
            switch (option.Framework)
            {
                case "ts":
                case "react":
                case "typescript":
                    {
                        TypescriptGenerator tsGenerator = new TypescriptGenerator(new TypescriptOptions
                        {
                            ModelOptions = new TsOutputOption { OutputPath = output },
                            ClientOptions = new TsClientOptions
                            {
                                OutputPath = output,
                                Framework = "",
                                Prefix = option.Prefix
                            },
                            Options = option.Options
                        });

                        tsGenerator.GenerateCode(ast);
                    }
                    break;

                case "ng":
                case "angular":
                    {
                        TypescriptGenerator tsGenerator = new TypescriptGenerator(new TypescriptOptions
                        {
                            ModelOptions = new TsOutputOption { OutputPath = output },
                            ClientOptions = new TsClientOptions
                            {
                                OutputPath = output,
                                Framework = TsOutputOption.FRAMEWORK_ANGULAR,
                                Prefix = option.Prefix
                            },
                            Options = option.Options
                        });

                        tsGenerator.GenerateCode(ast);
                    }
                    break;
            }
        }

    }
}
