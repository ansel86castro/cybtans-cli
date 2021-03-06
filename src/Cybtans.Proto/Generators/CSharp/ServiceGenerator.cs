using Cybtans.Proto.AST;
using Cybtans.Proto.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;

namespace Cybtans.Proto.Generators.CSharp
{
    public class ServiceGenerator : FileGenerator<ServiceGeneratorOptions>
    {
        private TypeGenerator _typeGenerator;

        public ServiceGenerator(ProtoFile proto, ServiceGeneratorOptions option, TypeGenerator typeGenerator) :base(proto, option)
        {
            this._typeGenerator = typeGenerator;
            Namespace = option.Namespace ?? $"{proto.Option.Namespace ?? proto.Filename.Pascal()}.Services";

            foreach (var item in Proto.Declarations)
            {
                if (item is ServiceDeclaration srv)
                {
                    var info = new ServiceGenInfo(srv, _option, Proto);             

                    Services.Add(srv, info);
                }
            }
        }

        public string Namespace { get; }

        public Dictionary<ServiceDeclaration, ServiceGenInfo> Services { get; } = new Dictionary<ServiceDeclaration, ServiceGenInfo>();

        public override void GenerateCode()
        {
            if (!Services.Any())
                return;

            Directory.CreateDirectory(_option.OutputPath);
            
            foreach (var item in Services)
            {
                var srv = item.Value;
                GenerateService(srv);

                if (srv.Service.Option.GrpcProxy)
                {                    
                    GenerateGrpsProxy(srv);
                }
            }        
        }     
        
        public string GetInterfaceName(ServiceDeclaration service)
        {
            if (_option.NameTemplate != null)
            {
                return $"I{TemplateProcessor.Process(_option.NameTemplate, new { Name = service.Name.Pascal() })}";
            }

            return $"I{service.Name.Pascal()}";
        }

        public string GetImplementationName(ServiceDeclaration service)
        {
            var name = service.Option.GrpcProxyName ?? service.Name.Pascal();
            if (_option.NameTemplate != null)
            {
                return TemplateProcessor.Process(_option.NameTemplate, new { Name = name });
            }

            return name;
        }


        private void GenerateService(ServiceGenInfo info)
        {
            var writer = CreateWriter(info.Namespace);
           
            writer.Usings.Append("using System.Threading.Tasks;").AppendLine();                       

            writer.Usings.AppendLine().Append($"using mds = global::{_typeGenerator.Namespace};").AppendLine();

            var clsWriter = writer.Class;

            if (info.Service.Option.Description != null)
            {
                clsWriter.Append("/// <summary>").AppendLine();
                clsWriter.Append("/// ").Append(info.Service.Option.Description).AppendLine();
                clsWriter.Append("/// </summary>").AppendLine();                
            }

            clsWriter.Append("public");

            if (_option.PartialClass)
            {
                clsWriter.Append(" partial");
            }

            var typeName = GetInterfaceName(info.Service);

            clsWriter.Append($" interface {typeName} ").AppendLine();                 
                        
            clsWriter.Append("{").AppendLine();
            clsWriter.Append('\t', 1);

            var bodyWriter = clsWriter.Block("BODY");

            foreach (var rpc in info.Service.Rpcs)
            {
                var returnInfo = rpc.ResponseType;
                var requestInfo = rpc.RequestType;               

                bodyWriter.AppendLine();
                if (rpc.Option.Description != null)
                {
                    bodyWriter.Append("/// <summary>").AppendLine();
                    bodyWriter.Append("/// ").Append(rpc.Option.Description).AppendLine();
                    bodyWriter.Append("/// </summary>").AppendLine();
                }

                bodyWriter.Append($"{returnInfo.GetFullReturnTypeName()} { GetRpcName(rpc)}({requestInfo.GetFullRequestTypeName("request")});");
                bodyWriter.AppendLine();
                bodyWriter.AppendLine();
            }

            clsWriter.Append("}").AppendLine();

            writer.Save(typeName);
        }

        public string GetRpcName(RpcDeclaration rpc)
        {
            return rpc.Name.Pascal();
        }  
        
      
        private void GenerateGrpsProxy(ServiceGenInfo info)
        {
            var ns = _option.ImplementationNamespace  ?? $"{Proto.Option.Namespace}.Services";

            var writer = CreateWriter(ns);
         
            writer.Usings.Append("using System.Threading.Tasks;").AppendLine();                             
            writer.Usings.Append("using Grpc.Core;").AppendLine();
            writer.Usings.Append("using Microsoft.Extensions.Logging;").AppendLine();                       

            if (_option.ImplementationNamespace != _option.Namespace)
            {
                writer.Usings.Append($"using {info.Namespace};").AppendLine();
            }

            var proxyName = GetImplementationName(info.Service);
            var interfaceName = GetInterfaceName(info.Service);

            var clsWriter = writer.Class;

            writer.Usings.Append($"using mds = global::{_typeGenerator.Namespace};").AppendLine();

            if (_option.AutoRegisterImplementation)
            {
                writer.Usings.Append("using Cybtans.Services;").AppendLine();
                clsWriter.Append($"[RegisterDependency(typeof({ interfaceName}))]").AppendLine();
            }
            
            clsWriter.Append($"public partial class {proxyName} : {interfaceName}").AppendLine();

            clsWriter.Append("{").AppendLine();
            clsWriter.Append('\t', 1);

            var bodyWriter = clsWriter.Block("BODY");

            var grpcClientType = $"{Proto.Option.CSharpNamespace}.{info.Name}.{info.Name}Client";

            bodyWriter.Append($"private readonly global::{grpcClientType}  _client;").AppendLine()
                      .Append($"private readonly ILogger<{proxyName}> _logger;").AppendLine();

            List<(string name, string type)> parameters = null;
            if (info.Service.Option.ConstructorOptions?.HasAdditionalParameters ?? false)
            {
                parameters = info.Service.Option.ConstructorOptions.GetParameters();
                foreach (var item in parameters)
                {
                    bodyWriter.Append($"private readonly global::{item.type}  _{item.name};").AppendLine();
                }               
            }

            bodyWriter.AppendLine();

            #region Constructor

            if (info.Service.Option.ConstructorOptions?.Visibility != null)
            {
                bodyWriter.Append(info.Service.Option.ConstructorOptions.Visibility);
            }
            else
            {
                bodyWriter.Append("public");
            }

            bodyWriter.Append($" {proxyName}(global::{grpcClientType} client, ILogger<{proxyName}> logger");
            if (parameters != null)
            {
                foreach (var item in parameters)
                {
                    bodyWriter.Append($", global::{item.type} {item.name}");
                }
            }
            bodyWriter.Append(")").AppendLine();

            bodyWriter.Append("{").AppendLine();
            bodyWriter.Append('\t', 1).Append("_client = client;").AppendLine();         
            bodyWriter.Append('\t', 1).Append("_logger = logger;").AppendLine();

            if (parameters!=null)
            {                
                foreach (var item in parameters)
                {
                    bodyWriter.Append('\t', 1).Append($"_{item.name} = {item.name};").AppendLine();
                }                
            }

            bodyWriter.Append("}").AppendLine();

            #endregion

            foreach (var rpc in info.Service.Rpcs)
            {
                var returnInfo = rpc.ResponseType;
                var requestInfo = rpc.RequestType;
                var rpcName = GetRpcName(rpc);                                  

                bodyWriter.AppendLine();

                bodyWriter.Append($"public ");

                if (rpc.Option.GrpcPartial)
                {
                    bodyWriter.Append("partial ");
                }
                else
                {
                    bodyWriter.Append("async ");
                }

                bodyWriter.Append($"{returnInfo.GetFullReturnTypeName()} { GetRpcName(rpc)}({requestInfo.GetFullRequestTypeName("request")})");
                if (rpc.Option.GrpcPartial)
                {
                    bodyWriter.Append(";").AppendLine();
                    continue;
                }

                bodyWriter.AppendLine();
                bodyWriter.Append("{").AppendLine().Append('\t', 1);

                var methodWriter = bodyWriter.Block($"METHODBODY_{rpc.Name}");

                methodWriter.Append("try").AppendLine()
                    .Append("{").AppendLine();

                var response =!PrimitiveType.Void.Equals(returnInfo) ? "var response = " : "" ;
                methodWriter.Append('\t', 1).Append($"{ response}await _client.{rpcName}Async({(!PrimitiveType.Void.Equals(requestInfo) ? $"request.ToProtobufModel()" : "")});").AppendLine();
                if (!PrimitiveType.Void.Equals(returnInfo))
                {
                    methodWriter.Append('\t', 1).Append($"return response.ToPocoModel();").AppendLine();
                }

                methodWriter.Append("}").AppendLine();
                methodWriter.Append("catch(RpcException ex)").AppendLine()
                    .Append("{").AppendLine();

                methodWriter.Append('\t', 1).Append($"_logger.LogError(ex, \"Failed grpc call {grpcClientType}.{rpc.Name}\");").AppendLine();
                methodWriter.Append('\t', 1).Append("throw;").AppendLine();

                methodWriter.Append("}").AppendLine();

                bodyWriter.Append("}").AppendLine();
            }

            clsWriter.Append("}").AppendLine();

            writer.SaveTo(_option.ImplementationOutput, proxyName);
        }

      
    }
}
