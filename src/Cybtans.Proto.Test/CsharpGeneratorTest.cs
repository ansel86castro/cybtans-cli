using Cybtans.Proto.AST;
using Cybtans.Proto.Generators.CSharp;
using Cybtans.Proto.Options;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;

namespace Cybtans.Proto.Test
{
    public class CsharpGeneratorTest
    {
        [Fact]
        public void LoadProtoFromFileNoImports()
        {
            var fileResolverFactory = new SearchPathFileResolverFactory(new string[] { "Proto" });

            Proto3Generator generator = new Proto3Generator(fileResolverFactory);
            var (ast, scope) = generator.LoadFromFile("Protos/Service1.proto");

            AssertAST(ast);
        }

        [Theory]
        [InlineData("Protos/Service1.proto", "CSharp/Service1")]
        [InlineData("Protos/Catalog.proto", "CSharp/Catalog")]
        [InlineData("Protos/Customers.proto", "CSharp/Customer")]        
        public void GenerateCode(string filename, string output)
        {
            var fileResolverFactory = new SearchPathFileResolverFactory(new string[] { "Proto" });

            Proto3Generator generator = new Proto3Generator(fileResolverFactory);
            var (ast, scope) = generator.LoadFromFile(filename);
            Assert.NotNull(ast);

            MicroserviceGenerator microserviceGenerator = new MicroserviceGenerator(new GenerationOptions
            {
                ModelOptions = new ModelGeneratorOptions
                {
                    OutputPath = output,
                },
                ServiceOptions = new ServiceGeneratorOptions
                {
                    OutputPath = output
                },
                ControllerOptions = new WebApiControllerGeneratorOption
                {
                    OutputPath = output
                },
                ClientOptions = new  ClientGenerationOptions
                {
                     OutputPath = output
                }
            });

            microserviceGenerator.GenerateCode(ast);
        }


        [Theory]
        [InlineData("Protos/Main.proto", "CSharp/Main")]
        public void GenerateMultipleImports(string filename, string output)
        {
            var fileResolverFactory = new SearchPathFileResolverFactory(new string[] { "Proto" });

            Proto3Generator generator = new Proto3Generator(fileResolverFactory);
            var (ast, scope) = generator.LoadFromFile(filename);
            Assert.NotNull(ast);

            MicroserviceGenerator microserviceGenerator = new MicroserviceGenerator(new GenerationOptions
            {
                ModelOptions = new ModelGeneratorOptions
                {
                    OutputPath = output,
                },
                ServiceOptions = new ServiceGeneratorOptions
                {
                    OutputPath = output
                },
                ControllerOptions = new WebApiControllerGeneratorOption
                {
                    OutputPath = output
                },
                ClientOptions = new ClientGenerationOptions()
                {
                    OutputPath = output,
                }
            });

            microserviceGenerator.GenerateCode(ast);
        }

        [Theory]     
        [InlineData("Protos/Compatibility.proto", "CSharp/CompatibilityAccesor", true)]
        [InlineData("Protos/Compatibility.proto", "CSharp/Compatibility", false)]
        public void GenerateCodeNoCybtans(string filename, string output, bool accesor)
        {
            var fileResolverFactory = new SearchPathFileResolverFactory(new string[] { "Proto" });

            Proto3Generator generator = new Proto3Generator(fileResolverFactory);
            var (ast, scope) = generator.LoadFromFile(filename);
            Assert.NotNull(ast);

            MicroserviceGenerator microserviceGenerator = new MicroserviceGenerator(new GenerationOptions
            {
                ModelOptions = new ModelGeneratorOptions
                {
                    OutputPath = output,
                    GenerateAccesor = accesor,                     
                },
                ServiceOptions = new ServiceGeneratorOptions
                {
                    OutputPath = output,                     
                },
                ControllerOptions = new WebApiControllerGeneratorOption
                {
                    OutputPath = output
                },
                ClientOptions = new ClientGenerationOptions
                {
                    OutputPath = output
                }
            });

            microserviceGenerator.GenerateCode(ast);
        }

        [Theory]
        [InlineData("Protos/greet.proto", "CSharp/greet")]        
        public void GenerateGrpcProxy(string filename, string output)
        {
            var fileResolverFactory = new SearchPathFileResolverFactory(new string[] { "Proto" });

            Proto3Generator generator = new Proto3Generator(fileResolverFactory);
            var (ast, scope) = generator.LoadFromFile(filename);
            Assert.NotNull(ast);

            MicroserviceGenerator microserviceGenerator = new MicroserviceGenerator(new GenerationOptions
            {
                ModelOptions = new ModelGeneratorOptions
                {
                    OutputPath = output,                    
                },
                ServiceOptions = new ServiceGeneratorOptions
                {
                    OutputPath = output,
                    NameTemplate = "@{Name}Repository",
                    Namespace = "Cybtans.Tests.Grpc.Data"
                },
                ControllerOptions = new WebApiControllerGeneratorOption
                {
                    OutputPath = output
                },
                ClientOptions = new ClientGenerationOptions
                {
                    OutputPath = output
                },
                ApiGatewayOptions = new ApiGateWayGeneratorOption
                {
                    OutputPath = Path.Combine(output, "Gateway")
                }
            });

            microserviceGenerator.GenerateCode(ast);
        }


        private static void AssertAST(ProtoFile ast)
        {
            Assert.NotNull(ast);
            Assert.NotNull(ast.Option.Namespace);
            Assert.NotNull(ast.Declarations);
            Assert.Equal(9, ast.Declarations.Count);
            Assert.NotNull(ast.Package);
            Assert.Equal("Service1", ast.Package.ToString());
            Assert.Equal(7, ast.Declarations.Where(x => x is MessageDeclaration).Count());
            Assert.Single(ast.Declarations.Where(x => x is ServiceDeclaration));

            foreach (var msg in ast.Declarations.Where(x => x is MessageDeclaration).Select(x => (MessageDeclaration)x))
            {
                Assert.NotNull(msg.Option);
                Assert.NotEmpty(msg.Fields);
                foreach (var field in msg.Fields)
                {
                    Assert.NotNull(field.Name);
                    Assert.NotNull(field.Option);
                    Assert.True(field.Number > 0);

                    Assert.NotNull(field.Type);
                    Assert.NotNull(field.Type.Name);
                    Assert.NotNull(field.Type.TypeDeclaration);

                    if (field.Name == "preferences")
                    {
                        Assert.True(field.Type.IsArray);
                    }
                }
            }

            foreach (var srv in ast.Declarations.Where(x => x is ServiceDeclaration).Select(x => (ServiceDeclaration)x))
            {
                Assert.NotNull(srv.Option);
                Assert.NotNull(srv);
                Assert.NotEmpty(srv.Rpcs);

                var serviceOptions = srv.Option as ServiceOptions;

                Assert.NotNull(serviceOptions);
                Assert.Equal("api/service1", serviceOptions.Prefix);

                foreach (var rpc in srv.Rpcs)
                {
                    Assert.NotNull(rpc.Request);
                    Assert.NotNull(rpc.Response);

                    Assert.NotNull(rpc.RequestType);
                    Assert.NotNull(rpc.ResponseType);

                    var rpcOptions = rpc.Option as RpcOptions;
                    Assert.NotNull(rpcOptions);
                    Assert.NotNull(rpcOptions.Method);
                }
            }
        }
    }
}
