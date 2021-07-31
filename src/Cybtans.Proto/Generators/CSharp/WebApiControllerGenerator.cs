using Cybtans.Proto.AST;
using Cybtans.Proto.Options;
using Cybtans.Proto.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Cybtans.Proto.Generators.CSharp
{
    public class WebApiControllerGenerator : FileGenerator<WebApiControllerGeneratorOption>
    {
        protected ServiceGenerator _serviceGenerator;
        protected TypeGenerator _typeGenerator;
        public WebApiControllerGenerator(ProtoFile proto, WebApiControllerGeneratorOption option,
         ServiceGenerator serviceGenerator, TypeGenerator typeGenerator) : base(proto, option)
        {
            _serviceGenerator = serviceGenerator;
            _typeGenerator = typeGenerator;
        }

        public override void GenerateCode()
        {
            if (!_serviceGenerator.Services.Any())
                return;

            Directory.CreateDirectory(_option.OutputPath);
            foreach (var item in _serviceGenerator.Services)
            {
                var srvInfo = item.Value;
                GenerateController(srvInfo);
            }
        }

        protected virtual void GenerateController(ServiceGenInfo srvInfo)
        {
            var writer = CreateWriter(_option.Namespace ?? $"{Proto.Option.Namespace ?? Proto.Filename.Pascal()}.Controllers");

            writer.Usings.Append($"using {_serviceGenerator.Namespace};").AppendLine();
          
            GenerateControllerInternal(srvInfo, writer);
        }

        protected virtual string GetServiceType(ServiceDeclaration service)
        {
            return _serviceGenerator.GetInterfaceName(service);
        }

        protected void GenerateControllerInternal(ServiceGenInfo srvInfo, CsFileWriter writer)
        {
            var srv = srvInfo.Service;
            var serviceType = GetServiceType(srv);
            var clsWriter = writer.Class;

            writer.Usings.Append("using System;").AppendLine();
            writer.Usings.Append("using System.Threading.Tasks;").AppendLine();
            writer.Usings.Append("using Microsoft.AspNetCore.Mvc;").AppendLine();
            writer.Usings.Append("using Microsoft.Extensions.Logging;").AppendLine();
            if (srvInfo.Service.Rpcs.Any(x => x.RequestType.HasStreams() || x.ResponseType.HasStreams()))
            {
                writer.Usings.Append("using Cybtans.AspNetCore;").AppendLine();
            }

            writer.Usings.AppendLine().Append($"using mds = global::{_typeGenerator.Namespace};").AppendLine();

            if (srv.Option.RequiredAuthorization || srv.Option.AllowAnonymous ||
               srv.Rpcs.Any(x => x.Option.RequiredAuthorization || x.Option.AllowAnonymous))
            {
                writer.Usings.Append("using Microsoft.AspNetCore.Authorization;").AppendLine();
            }

            #region  Class Name 

            AddDescription(srvInfo, clsWriter);
            AddAttributes(srv.Option.Attributes, clsWriter);
            AddAutorizationAttribute(srv.Option, clsWriter);

            clsWriter.Append($"[Route(\"{srv.Option.Prefix}\")]").AppendLine();
            clsWriter.Append("[ApiController]").AppendLine();
            clsWriter.Append($"public partial class {srvInfo.Name}Controller : ControllerBase").AppendLine();

            clsWriter.Append("{").AppendLine();
            clsWriter.Append('\t', 1);

            #endregion

            var bodyWriter = clsWriter.Block("BODY");

            #region Field Members

            bodyWriter.Append($"private readonly {serviceType} _service;").AppendLine();
            bodyWriter.Append($"private readonly ILogger<{srvInfo.Name}Controller> _logger;").AppendLine();

            if (_option.UseActionInterceptor)
            {
                bodyWriter.Append($"private readonly {_option.GetInterceptorType()} _interceptor;").AppendLine();
            }

            bool requireAuthorizationService = srv.Rpcs.Any(x => x.Option.AuthOptions?.RequestPolicy != null || x.Option.AuthOptions?.ResultPolicy != null);
            if (requireAuthorizationService)
            {
                bodyWriter.Append($"private readonly global::Microsoft.AspNetCore.Authorization.IAuthorizationService _authorizationService;").AppendLine();
            }


            bodyWriter.AppendLine();

            #endregion

            #region Constructor

            bodyWriter.Append($"public {srvInfo.Name}Controller({serviceType} service,  ILogger<{srvInfo.Name}Controller> logger");

            if (requireAuthorizationService)
            {
                bodyWriter.Append($", global::Microsoft.AspNetCore.Authorization.IAuthorizationService authorizationService");
            }

            if (_option.UseActionInterceptor)
            {
                bodyWriter.Append($", {_option.GetInterceptorType()} interceptor = null");
            }          

            bodyWriter.Append(")").AppendLine();

            bodyWriter.Append("{").AppendLine();
            bodyWriter.Append('\t', 1).Append("_service = service;").AppendLine();
            bodyWriter.Append('\t', 1).Append("_logger = logger;").AppendLine();

            if (_option.UseActionInterceptor)
            {
                bodyWriter.Append('\t', 1).Append("_interceptor = interceptor;").AppendLine();
            }
            if (requireAuthorizationService)
            {
                bodyWriter.Append('\t', 1).Append("_authorizationService = authorizationService;").AppendLine();
            }

            bodyWriter.Append("}").AppendLine();

            #endregion

            foreach (var rpc in srv.Rpcs)
            {
                var options = rpc.Option;
                var request = rpc.RequestType;
                var response = rpc.ResponseType;
                var rpcName = _serviceGenerator.GetRpcName(rpc);
                string template = options.Template != null ? $"(\"{options.Template}\")" : "";

                bodyWriter.AppendLine();

                if (rpc.Option.Description != null)
                {
                    bodyWriter.Append("/// <summary>").AppendLine();
                    bodyWriter.Append("/// ").Append(rpc.Option.Description).AppendLine();
                    bodyWriter.Append("/// </summary>").AppendLine();
                    bodyWriter.Append($"[System.ComponentModel.Description(\"{rpc.Option.Description.Scape()}\")]").AppendLine();
                }

                AddAttributes(rpc.Option.Attributes, bodyWriter);

                AddAutorizationAttribute(options, bodyWriter);

                AddRequestMethod(bodyWriter, options, template);

                bodyWriter.AppendLine();

                if (request.HasStreams())
                {
                    bodyWriter.Append("[DisableFormValueModelBinding]").AppendLine();
                }

                bodyWriter.Append($"public async {response.GetControllerReturnTypeName()} {rpcName}").Append("(");
                var parametersWriter = bodyWriter.Block($"PARAMS_{rpc.Name}");
                bodyWriter.Append($"{GetRequestBinding(options.Method, request)}{request.GetFullRequestTypeName("request")})").AppendLine()
                    .Append("{").AppendLine()
                    .Append('\t', 1);

                var methodWriter = bodyWriter.Block($"METHODBODY_{rpc.Name}");

                bodyWriter.AppendLine().Append("}").AppendLine();

                if (options.Template != null)
                {
                    var path = request is MessageDeclaration ? _typeGenerator.GetMessageInfo(request).GetPathBinding(options.Template) : null;
                    if (path != null)
                    {
                        foreach (var field in path)
                        {
                            parametersWriter.Append($"{field.Type} {field.Field.Name}, ");
                            methodWriter.Append($"request.{field.Name} = {field.Field.Name};").AppendLine();
                        }

                        methodWriter.AppendLine();
                    }
                }

                if (PrimitiveType.Void.Equals(request))
                {
                    methodWriter.Append($"_logger.LogInformation(\"Executing {{Action}}\", nameof({rpcName}));").AppendLine(2);
                }
                else
                {
                    if (!request.HasStreams())
                    {
                        methodWriter.Append($"_logger.LogInformation(\"Executing {{Action}} {{Message}}\", nameof({rpcName}), request);").AppendLine(2);
                    }
                    else
                    {
                        methodWriter.Append($"_logger.LogInformation(\"Executing {{Action}}\", nameof({rpcName}));").AppendLine(2);
                    }

                    AddRequestAuthorization(rpc.Option.AuthOptions, methodWriter);

                    if (_option.UseActionInterceptor)
                    {
                        methodWriter.AppendTemplate(inteceptorTemplate, new Dictionary<string, object>
                        {
                            ["ACTION"] = rpcName
                        }).AppendLine(2);                       
                    }                    
                }               

                if (PrimitiveType.Void.Equals(response))
                {
                    methodWriter.Append($"await _service.{rpcName}({(!PrimitiveType.Void.Equals(request) ? "request" : "")}).ConfigureAwait(false);");
                }
                else
                {
                    methodWriter.Append($"var result = await _service.{rpcName}({(!PrimitiveType.Void.Equals(request) ? "request" : "")}).ConfigureAwait(false);").AppendLine();

                    AddResultAuthorization(rpc.Option.AuthOptions, methodWriter);

                    if (response.HasStreams())
                    {
                        WriteDownloadCode(options, response, methodWriter);
                    }
                    else
                    {
                        WriteHandleResult(rpc, rpcName, methodWriter);

                        methodWriter.Append("return result;");
                    }
                }               
            }

            clsWriter.Append("}").AppendLine();
            writer.Save($"{srvInfo.Name}Controller");
        }

        protected virtual void WriteHandleResult(RpcDeclaration rpc, string rpcName, CodeWriter methodWriter)
        {
            if (rpc.Option.HandleResult)
            {
                methodWriter.AppendTemplate(inteceptorResultTemplate, new Dictionary<string, object>
                {
                    ["ACTION"] = rpcName
                }).AppendLine(2);
            }
        }

        private void WriteDownloadCode(RpcOptions options, ITypeDeclaration response, CodeWriter methodWriter)
        {
            var result = "result";
            var contentType = $"\"{options.StreamOptions?.ContentType ?? "application/octet-stream"}\"";

            var fileName = options.StreamOptions?.Name;
            fileName = fileName != null ? $"\"{fileName}\"" : "Guid.NewGuid().ToString()";

            if (response is MessageDeclaration responseMsg)
            {
                var name = responseMsg.Fields.FirstOrDefault(x => x.FieldType == PrimitiveType.String && x.Name.ToLowerInvariant().EndsWith("name"));
                var type = responseMsg.Fields.FirstOrDefault(x => x.FieldType == PrimitiveType.String && x.Name.ToLowerInvariant() == "contenttype" || x.Name.ToLowerInvariant() == "content_type");
                if (name != null)
                    fileName = $"result.{name.GetFieldName()}";
                if (type != null)
                    contentType = $"result.{type.GetFieldName()}";

                methodWriter.AppendTemplate(streamReturnTemplate, new Dictionary<string, object>())
                    .AppendLine();

                var stream = responseMsg.Fields.FirstOrDefault(x => x.FieldType == PrimitiveType.Stream);
                if (stream != null)
                {
                    result = $"result.{stream.GetFieldName()}";
                }
            }

            methodWriter.Append($"return new FileStreamResult({result}, {contentType}) {{ FileDownloadName = {fileName} }};");
        }

        private static void AddDescription(ServiceGenInfo srvInfo, CodeWriter clsWriter)
        {
            if (srvInfo.Service.Option.Description != null)
            {
                clsWriter.Append("/// <summary>").AppendLine();
                clsWriter.Append("/// ").Append(srvInfo.Service.Option.Description).AppendLine();
                clsWriter.Append("/// </summary>").AppendLine();
                clsWriter.Append($"[System.ComponentModel.Description(\"{srvInfo.Service.Option.Description.Scape()}\")]").AppendLine();
            }
        }

        private static void AddAttributes(string attributes, CodeWriter writer)
        {
            if (attributes != null)
            {                
                foreach (var item in attributes.GetAttributeList())
                {
                    writer.Append($"[{item}]").AppendLine();
                }                
            }
        }

        private static void AddRequestMethod(CodeWriter bodyWriter, RpcOptions options, string template)
        {
            switch (options.Method)
            {
                case "GET":
                    bodyWriter.Append($"[HttpGet{template}]");
                    break;
                case "POST":
                    bodyWriter.Append($"[HttpPost{template}]");
                    break;
                case "PUT":
                    bodyWriter.Append($"[HttpPut{template}]");
                    break;
                case "DELETE":
                    bodyWriter.Append($"[HttpDelete{template}]");
                    break;
            }
        }

        private static void AddAutorizationAttribute(SecurityOptions option, CodeWriter clsWriter)
        {
            if (option.Authorized)
            {
                clsWriter.Append("[Authorize]").AppendLine();
                
            }
            else if (option.Roles != null)
            {
                clsWriter.Append($"[Authorize(Roles = \"{option.Roles}\")]").AppendLine();                
            }
            else if (option.Policy != null)
            {
                clsWriter.Append($"[Authorize(Policy = \"{option.Policy}\")]").AppendLine();                
            }
            else if(option.AllowAnonymous)
            {
                clsWriter.Append("[AllowAnonymous]").AppendLine();
            }
        }

        private object GetRequestBinding(string method, ITypeDeclaration request)
        {
            if (PrimitiveType.Void.Equals(request))
                return "";

            switch (method)
            {
                case "DELETE":
                case "GET":
                    return "[FromQuery]";
                case "PATCH":
                case "PUT":
                case "POST":
                    return request.HasStreams() ? "[ModelBinder(typeof(CybtansModelBinder))]" : "[FromBody]";
                default:
                    throw new NotImplementedException("Http verb is not valid or not supported");
            }
        }

        private void AddRequestAuthorization(AuthOptions authOptions, CodeWriter writer)
        {
            if (authOptions?.RequestPolicy == null) return;

            writer.Append($@"var authRequestResult = await _authorizationService.AuthorizeAsync(User, request, ""{authOptions.RequestPolicy}"").ConfigureAwait(false);
if (!authRequestResult.Succeeded)
{{
    throw new UnauthorizedAccessException($""Request Authorization Failed: {{ string.Join("", "", authRequestResult.Failure.FailedRequirements) }}"");
}}");
            writer.AppendLine(2);
        }

        private void AddResultAuthorization(AuthOptions authOptions, CodeWriter writer)
        {
            if (authOptions?.ResultPolicy == null) return;

            writer.Append($@"if (result != null)
{{
    var authResult = await _authorizationService.AuthorizeAsync(User, result, ""{authOptions.ResultPolicy}"").ConfigureAwait(false);
    if (!authResult.Succeeded)
    {{
        throw new UnauthorizedAccessException($""Result Authorization Failed: {{ string.Join("", "", authResult.Failure.FailedRequirements) }}"");
    }}
}}");
            writer.AppendLine(2);
        }


        string streamReturnTemplate = @"
if(Request.Headers.ContainsKey(""Accept"") && System.Net.Http.Headers.MediaTypeHeaderValue.TryParse(Request.Headers[""Accept""], out var mimeType) && mimeType?.MediaType == ""application/x-cybtans"")
{				
	return new ObjectResult(result);
}";

        string inteceptorTemplate = 
@"if(_interceptor != null )
{
    await _interceptor.Handle(request).ConfigureAwait(false);
}";

        string inteceptorResultTemplate =
@"if(_interceptor != null )
{
    await _interceptor.HandleResult(result).ConfigureAwait(false);
}";
    }
}
