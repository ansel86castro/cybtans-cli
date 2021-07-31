using Cybtans.Proto.AST;
using Cybtans.Proto.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Cybtans.Proto.Generators.CSharp
{
    public abstract class GraphQLGenerator : SingleFileGenerator<GraphQLGeneratorOptions>
    {        
        private readonly ModelGeneratorOptions _modelOptions;

        public GraphQLGenerator(ProtoFile entryPoint, IEnumerable<ProtoFile> protos, GraphQLGeneratorOptions option, ModelGeneratorOptions modelOptions)
            : base(entryPoint, protos, option, option.Namespace ?? $"{entryPoint.Option.Namespace ?? entryPoint.Filename.Pascal()}.GraphQL")
        {            
            _modelOptions = modelOptions;
        }

        public override void GenerateCode()
        {
            if (!Proto.HaveServices)
                return;

            base.GenerateCode();
        }

        public override void OnGenerationBegin(CsFileWriter writer)
        {
            writer.Usings.Append("using System;").AppendLine();
            writer.Usings.Append("using GraphQL;").AppendLine();
            writer.Usings.Append("using GraphQL.Types;").AppendLine();
            writer.Usings.Append("using Microsoft.Extensions.DependencyInjection;").AppendLine();
            writer.Usings.Append("using Microsoft.AspNetCore.Authorization;").AppendLine();
            writer.Usings.Append("using Microsoft.AspNetCore.Http;").AppendLine();

            var modelNs = _modelOptions.Namespace ?? $"{Proto.Option.Namespace ?? Proto.Filename.Pascal()}.Models";
            writer.Usings.Append($"using {modelNs};").AppendLine();            
        }

        public override void OnGenerationEnd(CsFileWriter writer)
        {
            var clsWriter = new CodeWriter();

            clsWriter.Append($"public partial class {_option.QueryName} : ObjectGraphType").AppendLine();
            clsWriter.Append("{").AppendLine();

            Dictionary<string, CodeWriter> slots = new Dictionary<string, CodeWriter>();
            foreach (var proto in Protos)
            {
                foreach (var service in proto.Declarations.OfType<ServiceDeclaration>())
                {
                    var serviceWriter = new CodeWriter();

                    GenerateQueryGraph(service, serviceWriter);

                    slots[service.Name] = serviceWriter;
                }
            }

            AddFields(clsWriter, slots);
          
            AddBlock("__Query__", clsWriter.ToString());
        }

        protected override void GenerateCode(ProtoFile proto)
        {
            HashSet<ITypeDeclaration> declarations = new HashSet<ITypeDeclaration>();

            foreach (var service in proto.Declarations.OfType<ServiceDeclaration>())
            {
                foreach (var rpc in service.Rpcs)
                {
                    if (!CanGenerateRpc(rpc)) continue;

                    if (rpc.ResponseType is MessageDeclaration msg)
                    {
                        msg.AddTypes(declarations);
                    }
                }
            }

            foreach (var item in declarations)
            {
                if (item is EnumDeclaration e)
                {
                    var writer = new CodeWriter();
                    GenerateEnumGraph(e, writer);
                    AddBlock(GetGraphTypeName(e), writer.ToString());
                }

                else if (item is MessageDeclaration msg)
                {
                    var writer = new CodeWriter();
                    GenerateMessageGraph(msg, writer);
                    AddBlock(GetGraphTypeName(msg), writer.ToString());
                }
            }
        }

        private bool CanGenerateRpc(RpcDeclaration rpc)
        {
            return rpc.Option?.Method == "GET" && !PrimitiveType.Void.Equals(rpc.ResponseType);
        }

        protected override void SaveFile(CsFileWriter writer)
        {
            writer.Save(_option.QueryName);
        }

        private void GenerateEnumGraph(EnumDeclaration e, CodeWriter clsWriter)
        {
            var enumName = e.GetFullTypeName();
            clsWriter.Append($"public class {GetGraphTypeName(e)} : EnumerationGraphType<{enumName}>").AppendLine();
            clsWriter.Append("{").AppendLine();
            clsWriter.Append("}").AppendLine(2);
        }

        private void GenerateMessageGraph(MessageDeclaration msg, CodeWriter clsWriter)
        {
            var name = msg.GetFullTypeName();
            var graphType = GetGraphTypeName(msg);
            clsWriter.Append($"public class {graphType} : ObjectGraphType<{name}>").AppendLine();
            clsWriter.Append("{").AppendLine();

            var bodyWriter = clsWriter.Append('\t', 1).Block($"BODY_{name.Replace(".", "_")}");

            bodyWriter.Append($"public {graphType}()").AppendLine().Append("{").AppendLine();
            var methodWriter = bodyWriter.Append('\t', 1).Block($"Constructor_{name.Replace(".", "_")}");

            foreach (var field in msg.Fields.OrderBy(x => x.Number))
            {
                var fieldType = field.FieldType;
                var fieldName = field.GetFieldName();

                if (fieldType == PrimitiveType.Stream || PrimitiveType.Bytes.Equals(fieldType) || field.Type.IsMap)
                    continue;

                if (field.Type.IsArray)
                {
                    methodWriter.Append($"Field<ListGraphType<{GetGraphTypeName(fieldType)}>>(\"{fieldName}\"");
                    AddNamedParameters(methodWriter, field);
                }                
                else if (PrimitiveType.Datetime == fieldType || PrimitiveType.TimeStamp == fieldType || PrimitiveType.Duration == fieldType)
                {
                    methodWriter.Append($"Field<DateTimeGraphType>(\"{fieldName}\"");
                    AddNamedParameters(methodWriter, field);
                }
               
                else if (fieldType is IUserDefinedType)
                {
                    methodWriter.Append($"Field<{GetGraphTypeName(fieldType)}>(\"{fieldName}\"");
                    AddNamedParameters(methodWriter, field);
                }
                else
                {
                    methodWriter.Append($"Field(x => x.{fieldName}");
                    if (field.IsNullable)
                    {
                        methodWriter.Append(", nullable:true");
                    }
                    methodWriter.Append(")");

                    if (field.Option.Description != null)
                    {
                        methodWriter.Append($".Description(\"{field.Option.Description}\")");
                    }
                }

                methodWriter.Append(";").AppendLine();
            }

            bodyWriter.AppendLine()
                .Append("}")
                .AppendLine();

            clsWriter.Append("}").AppendLine(2);
        }

        private void GenerateQueryGraph(ServiceDeclaration service, CodeWriter methodWriter)
        {
            if (!service.Rpcs.Any(x => x.Option.Method == "GET")) return;

            methodWriter.Append($"#region {service.Name}").AppendLine(2);

            foreach (var rpc in service.Rpcs)
            {
                if (rpc.Option.Method != "GET" || (_option.Explicit && rpc.Option.GraphQl?.Query == null))
                    continue;

                var request = rpc.RequestType;
                var response = rpc.ResponseType;

                if (PrimitiveType.Void.Equals(response))
                    continue;

                var queryName = GetQueryName(rpc, service);
                methodWriter.Append($"FieldAsync<{GetGraphTypeName(response)}>(\"{queryName}\",\r\n ");

                if (rpc.Option.Description != null)
                {
                    methodWriter.Append($"\tdescription: \"{rpc.Option.Description}\",\r\n");
                }

                #region Arguments

                if (!PrimitiveType.Void.Equals(request))
                {
                    methodWriter.Append("\targuments: new QueryArguments()\r\n\t{\r\n");

                    AddArguments(request as MessageDeclaration, methodWriter, 2);
                    
                    methodWriter.Append("\t},\r\n");
                }

                #endregion

                #region Resolver 

                methodWriter.Append('\t', 1).Append("resolve: async context =>\r\n\t{\r\n");

                var resolveWriter = methodWriter.Append('\t', 2).Block($"RESOLVER_{queryName}");                               

                if (!PrimitiveType.Void.Equals(request))
                {
                    var requestMsg = (MessageDeclaration)request;
                    
                    resolveWriter.Append($"var request = new {requestMsg.GetTypeName()}();").AppendLine();

                    foreach (var field in requestMsg.Fields)
                    {
                        resolveWriter.Append($"request.{field.GetFieldName()} = context.GetArgument<{field.GetFieldTypeName()}>(\"{field.Name.Camel()}\", default({field.GetFieldTypeName()}));").AppendLine();
                    }
                    resolveWriter.AppendLine();
                }

                AddSecurity(service, rpc, resolveWriter);

                resolveWriter.Append($"var service = context.RequestServices.GetRequiredService<{GetServiceName(service)}>();").AppendLine();

                var arg = !PrimitiveType.Void.Equals(request) ? "request" : "";
                resolveWriter.Append($"var result = await service.{rpc.Name}({arg}).ConfigureAwait(false);").AppendLine();

                AddResultSecurity(service, rpc, resolveWriter);

                resolveWriter.Append("return result;").AppendLine();

                #endregion

                methodWriter.Append("\t}\r\n);").AppendLine(2);

            }

            methodWriter.Append($"#endregion {service.Name}").AppendLine(2);
        }

        private static void AddSecurity(ServiceDeclaration service, RpcDeclaration rpc, CodeWriter resolveWriter)
        {
            if (!service.Option.RequiredAuthorization && !rpc.Option.RequiredAuthorization  )
                return ;

            resolveWriter.Append("var httpContext = context.RequestServices.GetRequiredService<IHttpContextAccessor>().HttpContext;").AppendLine();

            if (rpc.Option.Authorized || service.Option.Authorized)
            {                
                resolveWriter.Append("if (!httpContext.User.Identity.IsAuthenticated)\r\n{\r\n\t throw new UnauthorizedAccessException(\"Authentication Required\");\r\n}").AppendLine();
            }
            
            if (!string.IsNullOrEmpty(service.Option.Roles) || !string.IsNullOrEmpty(rpc.Option.Roles))
            {
                var roles = (rpc.Option.Roles ?? service.Option.Roles).Split(",");
               
                var roleChek = roles.Select(r => $"!httpContext.User.IsInRole(\"{r}\")").Aggregate((x, y) => $"{x} || {y}");
                resolveWriter.Append($"if ({roleChek})\r\n{{\r\n\t throw new UnauthorizedAccessException(\"Roles Authorization Required\");\r\n}}").AppendLine();
            }

            bool authorizationServiceResolved = !string.IsNullOrEmpty(rpc.Option.Policy) || !string.IsNullOrEmpty(service.Option.Policy);
            if (authorizationServiceResolved)
            {                
                resolveWriter.Append("var authorizationService = context.RequestServices.GetRequiredService<IAuthorizationService>();").AppendLine(); 
                resolveWriter.Append($"var policyResult = await authorizationService.AuthorizeAsync(httpContext.User, httpContext, \"{rpc.Option.Policy ?? service.Option.Policy}\").ConfigureAwait(false);").AppendLine();                
                resolveWriter.Append($"if (!policyResult.Succeeded)\r\n{{\r\n\t throw new UnauthorizedAccessException($\"Authorization Failed: {{ string.Join(\", \", policyResult.Failure.FailedRequirements) }}\");\r\n}}").AppendLine();
            }

            resolveWriter.AppendLine();

            if(rpc.Option.AuthOptions?.RequestPolicy != null && !PrimitiveType.Void.Equals(rpc.RequestType))
            {
                if (!authorizationServiceResolved)
                {
                    authorizationServiceResolved = true;
                    resolveWriter.Append("var authorizationService = context.RequestServices.GetRequiredService<IAuthorizationService>();").AppendLine();
                    resolveWriter.Append("var ");
                }
                
                resolveWriter.Append($"policyResult = await authorizationService.AuthorizeAsync(httpContext.User, request, \"{rpc.Option.AuthOptions.RequestPolicy}\").ConfigureAwait(false);").AppendLine();
                resolveWriter.Append($"if (!policyResult.Succeeded)\r\n{{\r\n\t throw new UnauthorizedAccessException($\"Request Authorization Failed: {{ string.Join(\", \", policyResult.Failure.FailedRequirements) }}\");\r\n}}").AppendLine();

                resolveWriter.AppendLine();
            }           
        }

        private void AddResultSecurity(ServiceDeclaration service, RpcDeclaration rpc, CodeWriter resolveWriter)
        {
            if (rpc.Option.AuthOptions?.ResultPolicy == null)
                return;

            bool authorizationServiceResolved = !string.IsNullOrEmpty(rpc.Option.Policy) 
                || !string.IsNullOrEmpty(service.Option.Policy)
                || (rpc.Option.AuthOptions?.RequestPolicy != null && !PrimitiveType.Void.Equals(rpc.RequestType));

            resolveWriter.Append("if (result != null)\r\n{\r\n");
            if (!authorizationServiceResolved)
            {
                resolveWriter.Append('\t',1).Append("var authorizationService = context.RequestServices.GetRequiredService<IAuthorizationService>();").AppendLine();
                resolveWriter.Append('\t',1).Append("var ");
            }
            else
            {
                resolveWriter.Append('\t', 1);
            }

            resolveWriter.Append($"policyResult = await authorizationService.AuthorizeAsync(httpContext.User, result, \"{rpc.Option.AuthOptions.ResultPolicy}\").ConfigureAwait(false);").AppendLine();
            resolveWriter.Append('\t', 1).Append($"if (!policyResult.Succeeded)\r\n\t{{\r\n\t\t throw new UnauthorizedAccessException($\"Result Authorization Failed: {{ string.Join(\", \", policyResult.Failure.FailedRequirements) }}\");\r\n\t}}").AppendLine();

            resolveWriter.Append("}").AppendLine();
            resolveWriter.AppendLine();

        }

        private static void AddNamedParameters(CodeWriter methodWriter, FieldDeclaration field)
        {
            if (field.IsNullable)
            {
                //methodWriter.Append(", nullable:true");
            }
            if (field.Option.Description != null)
            {
                methodWriter.Append($", description:\"{field.Option.Description}\"");
            }
            methodWriter.Append(")");
        }

        private string GetGraphTypeName(ITypeDeclaration type)
        {
            if (type is IUserDefinedType)
            {
                return $"{type.GetFullTypeName().Replace('.','_')}GraphType";
            }
            else if (type is PrimitiveType pt)
            {
                return GetGraphQLPrimitiveTypeName(pt);
            }
           
            return type.Name;
        }

        private string GetQueryName(RpcDeclaration rpc, ServiceDeclaration service)
        {
            string name = rpc.Name;

            if (rpc.Option.GraphQl?.Query != null)
            {
                return rpc.Option.GraphQl.Query;
            }
            else if (rpc.Name.StartsWith("Get") && rpc.Name.Length > 3)
            {
                name = rpc.Name.Substring(3);
            }

            name = $"{service.Name}_{name}";
            return name;
        }
    
        private void AddArguments(MessageDeclaration request, CodeWriter writer, int tabs)
        {
            foreach (var item in request.Fields)
            {
                if (item.FieldType == PrimitiveType.Stream || PrimitiveType.Bytes.Equals(item.FieldType) || item.Type.IsMap)
                    continue;

                var graphQlType = GetGraphTypeName(item.FieldType);
                if (item.Type.IsArray)
                {
                    graphQlType = $"ListGraphType<{graphQlType}>";
                }

                if (item.Option.Required)
                {
                    writer.Append('\t', tabs).Append($"new QueryArgument<NonNullGraphType<{graphQlType}>>(){{ Name = \"{item.GetFieldName()}\"");
                }
                else
                {
                    writer.Append('\t', tabs).Append($"new QueryArgument<{graphQlType}>(){{ Name = \"{item.GetFieldName()}\"");
                }

                if (item.Option.Description != null) 
                {
                    writer.Append($", Description = \"{item.Option.Description}\"");
                }
                if(item.Option.Default != null)
                {                    
                    if(item.Option.Default is string)
                        writer.Append($", DefaultValue = \"{item.Option.Default}\"");
                    else
                        writer.Append($", DefaultValue = {item.Option.Default}");
                }
                writer.Append(" },").AppendLine();
            }
        }
        
        private string GetGraphQLPrimitiveTypeName(PrimitiveType type)
        {
            switch (type.Name)
            {
                case "int8": return "ByteGraphType";
                case "int16": return "ShortGraphType";
                case "int32": return "IntGraphType";
                case "int64": return "LongGraphType";
                case "uint16": return "UShortGraphType";
                case "uint32": return "UIntGraphType";
                case "uint64": return "ULongGraphType";
                case "bool": return "BooleanGraphType";
                case "string": return "StringGraphType";              
                case "datetime": return "DateTimeGraphType";
                case "float": return "FloatGraphType";
                case "double": return "FloatGraphType";            
                case "guid": return "GuidGraphType";
                case "decimal": return "FloatGraphType";              
                case "google.protobuf.Timestamp": return "DateTimeGraphType";
                case "google.protobuf.Duration": return "TimeSpanSecondsGraphType";                
                case "google.protobuf.BoolValue": return "BooleanGraphType";
                case "google.protobuf.DoubleValue": return "FloatGraphType";
                case "google.protobuf.FloatValue": return "FloatGraphType";
                case "google.protobuf.Int32Value": return "IntGraphType";
                case "google.protobuf.Int64Value": return "LongGraphType";
                case "google.protobuf.UInt32Value": return "UIntGraphType";
                case "google.protobuf.UInt64Value": return "ULongGraphType";
                case "google.protobuf.StringValue": return "StringGraphType";               
            }

            throw new InvalidOperationException($"Type {type.Name} not supported");
        }

        protected abstract string GetServiceName(ServiceDeclaration service);

        protected abstract void AddFields(CodeWriter clsWriter, Dictionary<string, CodeWriter> slots);

    }

    public class ServiceGraphQLGenerator: GraphQLGenerator
    {
        private readonly ServiceGeneratorOptions _serviceOptions ;

        public ServiceGraphQLGenerator(ProtoFile entryPoint, 
            IEnumerable<ProtoFile> protos,
            ServiceGeneratorOptions serviceOptions,
            ModelGeneratorOptions modelOptions) 
            : base(entryPoint, protos, serviceOptions.GraphQLOptions, modelOptions)
        {
            _serviceOptions  = serviceOptions;
        }

        protected override string GetServiceName(ServiceDeclaration service)
        {
            var serviceNs = _serviceOptions.Namespace ?? $"{Proto.Option.Namespace ?? Proto.Filename.Pascal()}.Services";
            return $"global::{serviceNs}.{_serviceOptions.GetInterfaceName(service)}";
        }

        protected override void AddFields(CodeWriter clsWriter, Dictionary<string, CodeWriter> slots)
        {
            var bodyWriter = clsWriter.Append('\t', 1).Block($"BODY__QUERY__");

            // bodyWriter.Append($"public {_option.QueryName}()").AppendLine().Append("{").AppendLine();
            bodyWriter.Append($"public void Add{Proto.Filename.Pascal()}Definitions()").AppendLine().Append("{").AppendLine();

            var methodWriter = bodyWriter.Append('\t', 1).Block($"Constructor__QUERY__");

            bodyWriter.AppendLine().Append("}").AppendLine();
            clsWriter.Append("}").AppendLine(2);

            foreach (var slot in slots)
            {
                methodWriter.AppendBlock(slot.Key, slot.Value);
            }

            //clsWriter.AppendTemplate(schemaTemplate, new Dictionary<string, object>
            //{
            //    ["NAME"] = $"{_option.QueryName}Schema",
            //    ["QUERY"] = _option.QueryName
            //});

        }
        string schemaTemplate = @"
public class @{NAME} : Schema
{
    public @{NAME}(IServiceProvider provider)
        : base(provider)
    {            
        Query = new @{QUERY}(); 
    }
}
";
    }

    public class GatewayGraphQlGenerator : GraphQLGenerator
    {
        private readonly ClientGenerationOptions _clientOptions ;

        public GatewayGraphQlGenerator(ProtoFile entryPoint, IEnumerable<ProtoFile> protos, 
            ClientGenerationOptions clientOptions,
            ApiGateWayGeneratorOption option, 
            ModelGeneratorOptions modelOptions) 
            : base(entryPoint, protos, option.GraphQLOptions, modelOptions)
        {
            _clientOptions  = clientOptions;
        }

        protected override void AddFields(CodeWriter clsWriter, Dictionary<string, CodeWriter> slots)
        {
            var bodyWriter = clsWriter.Append('\t', 1).Block($"BODY__QUERY__");

            bodyWriter.Append($"public void Add{Proto.Filename.Pascal()}Definitions()").AppendLine().Append("{").AppendLine();

            var methodWriter = bodyWriter.Append('\t', 1).Block($"Constructor__QUERY__");

            bodyWriter.AppendLine().Append("}").AppendLine();
            clsWriter.Append("}").AppendLine(2);

            foreach (var slot in slots)
            {
                methodWriter.AppendBlock(slot.Key, slot.Value);
            }
        }

        protected override string GetServiceName(ServiceDeclaration service)
        {
            return _clientOptions.GetClientName(service, Proto);
        }
    }
}