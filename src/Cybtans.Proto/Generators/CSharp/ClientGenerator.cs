using Cybtans.Proto.AST;
using Cybtans.Proto.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Cybtans.Proto.Generators.CSharp
{
    public class ClientGenerator : FileGenerator<ClientGenerationOptions>
    {
        private readonly ServiceGenerator _serviceGenerator;
        private readonly TypeGenerator _typeGenerator;

        public string Namespace { get; }

        public ClientGenerator(ProtoFile proto, ClientGenerationOptions option, ServiceGenerator serviceGenerator, TypeGenerator typeGenerator) 
            : base(proto, option)
        {
            _serviceGenerator = serviceGenerator;
            _typeGenerator = typeGenerator;

            Namespace = option.Namespace ?? $"{proto.Option.Namespace ?? proto.Filename.Pascal()}.Clients";           
        }

        public override void GenerateCode()
        {
            if (!_serviceGenerator.Services.Any()) return;

            Directory.CreateDirectory(_option.OutputPath);

            foreach (var item in _serviceGenerator.Services)
            {
                GenerateClient(item.Value);                
            }            
        }

        public string GetClientName(ServiceDeclaration service)
        {            
            return $"I{service.Name.Pascal()}Client";
        }

        private void GenerateClient(ServiceGenInfo info)
        {
            var writer = CreateWriter(Namespace);

            writer.Usings.Append($"using Refit;").AppendLine();
            writer.Usings.Append($"using Cybtans.Refit;").AppendLine();
            writer.Usings.Append($"using System.Net.Http;").AppendLine();
            writer.Usings.Append($"using System.Threading.Tasks;").AppendLine();
            writer.Usings.Append($"using {_typeGenerator.Namespace};").AppendLine();

            var clsWriter = writer.Class;

            if (info.Service.Option.Description != null)
            {
                clsWriter.Append("/// <summary>").AppendLine();
                clsWriter.Append("/// ").Append(info.Service.Option.Description).AppendLine();
                clsWriter.Append("/// </summary>").AppendLine();
            }

            var clientName = GetClientName(info.Service);

            clsWriter.Append("[ApiClient]").AppendLine();
            clsWriter.Append($"public interface {clientName}").AppendLine();
            clsWriter.Append("{").AppendLine();
            clsWriter.Append('\t', 1);

            var bodyWriter = clsWriter.Block("BODY");

            var srv = info.Service;

            foreach (var rpc in srv.Rpcs)
            {
                var options = rpc.Option;
                var request = rpc.RequestType;
                var response = rpc.ResponseType;
                var rpcName = _serviceGenerator.GetRpcName(rpc);                
                string url = string.IsNullOrEmpty(_option.Prefix) ? 
                    $"/{srv.Option.Prefix}" 
                    : $"/{_option.Prefix}/{srv.Option.Prefix}";

                List<MessageFieldInfo>? path = null;
                if(options.Template != null)
                {
                    var template = options.Template;
                    path = request is MessageDeclaration ? _typeGenerator.GetMessageInfo(request).GetPathBinding(template) : null;
                    if(path != null)
                    {
                        foreach (var field in path)
                        {
                            template = template.Replace($"{{{field.Field.Name}}}", $"{{request.{field.Name}}}");
                        }
                    }
                    url += $"/{ template }";
                }                                

                bodyWriter.AppendLine();

                if (rpc.Option.Description != null)
                {
                    bodyWriter.Append("/// <summary>").AppendLine();
                    bodyWriter.Append("/// ").Append(rpc.Option.Description).AppendLine();
                    bodyWriter.Append("/// </summary>").AppendLine();
                }

                if (srv.Option.RequiredAuthorization || options.RequiredAuthorization)
                {
                    bodyWriter.Append("[Headers(\"Authorization: Bearer\")]").AppendLine();
                }

                string optional = "";

                switch (options.Method)
                {
                    case "GET":
                        bodyWriter.Append($"[Get(\"{url}\")]");
                        if((path == null || path.Count == 0) && !PrimitiveType.Void.Equals(request))
                        {
                            optional = " = null";
                        }
                        break;
                    case "POST":
                        bodyWriter.Append($"[Post(\"{url}\")]");
                        break;
                    case "PUT":
                        bodyWriter.Append($"[Put(\"{url}\")]");
                        break;
                    case "DELETE":
                        bodyWriter.Append($"[Delete(\"{url}\")]");
                        break;
                }

                bodyWriter.AppendLine();

                bodyWriter.Append($"{response.GetReturnTypeName()} {rpcName}({GetRequestBinding(options.Method)}{request.GetRequestTypeName("request")}{optional});").AppendLine();
            }

            clsWriter.AppendLine().Append("}").AppendLine();

            writer.Save(clientName);

        }              

        private void GenerateExtensions()
        {
            var writer = CreateWriter("Microsoft.Extensions.DependencyInjection");

            writer.Usings.Append("using Refit;").AppendLine();
            writer.Usings.Append("using Cybtans.Refit;").AppendLine();
            writer.Usings.Append("using Cybtans.Serialization;").AppendLine();
            writer.Usings.Append("using Microsoft.Extensions.Configuration;").AppendLine();
            writer.Usings.Append("using Microsoft.Extensions.DependencyInjection;").AppendLine();
            writer.Usings.Append("using System.Text;").AppendLine();
            writer.Usings.Append($"using {Namespace};").AppendLine()
                .AppendLine(2);

            StringBuilder namedSb = new StringBuilder();
            StringBuilder genericSb = new StringBuilder();

            foreach (var item in _serviceGenerator.Services)
            {              
                namedSb.Append(TemplateProcessor.Process(namedRegister, new
                {
                    PACKAGE = Proto.Package.ToString(),
                    NAME = item.Value.Name
                }));

                namedSb.AppendLine();
                
                genericSb.Append($"services.AddClient<I{item.Value.Name}>(baseUrl, configure);");
                genericSb.AppendLine();
            }

            writer.Class.AppendTemplate(setupExtension, new Dictionary<string, object>
            {
                ["NAME"] = Proto.Package.ToString(),
                ["NAMED_REGISTER"] = namedSb.ToString(),
                ["GENERIC_REGISTER"] = genericSb.ToString()
            });

            writer.Save($"{Proto.Package}ServiceCollectionExtensions");
        }

        private object GetRequestBinding(string method)
        {
            switch (method)
            {
                case "GET":
                case "DELETE":
                    return "";

                case "POST":
                case "PUT":
                case "PATCH":
                    return "[Body]";
                default:
                    throw new NotImplementedException();
            }            
        }

        string setupExtension = @"
public class @{NAME}Option
{
    public string BaseUrl { get; set; }
}

public static class @{NAME}Extensions
{
    @{NAMED_REGISTER}
    
    public static IServiceCollection Add@{NAME}Services(this IServiceCollection services, string baseUrl, Action<IHttpClientBuilder> configure = null)
    {
		@{GENERIC_REGISTER}
		return services;
	}

	private static IServiceCollection AddClient<T>(this IServiceCollection services, string baseUrl, Action<IHttpClientBuilder> configure)
		where T:class
    {
	    var httpClientBuilder = services.AddClient<T>(baseUrl);
		configure?.Invoke(httpClientBuilder);
		return services;
    }
}
";

        string namedRegister = @"
public static IHttpClientBuilder Add@{NAME}(this IServiceCollection services, IConfiguration configuration, RefitSettings settings = null)
{
    var option = configuration.GetSection(""@{PACKAGE}Option"").Get<@{PACKAGE}Option>();

    if(settings == null)
    {
        settings = new RefitSettings();
    }

    settings.ContentSerializer = new CybtansContentSerializer(settings.ContentSerializer);

    var builder = services.AddRefitClient<I@{NAME}>(settings);

    builder.ConfigureHttpClient(c =>
    {                
        c.BaseAddress = new Uri(option.BaseUrl);
        c.DefaultRequestHeaders.Add(""Accept"", $""{BinarySerializer.MEDIA_TYPE}; charset={Encoding.UTF8.WebName}"");
    });

    return builder;
}
";


    }
}
