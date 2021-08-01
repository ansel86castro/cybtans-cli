using Cybtans.Proto.AST;
using Cybtans.Proto.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace Cybtans.Proto.Generators.CSharp
{
    public class GrpcMappingGenerator : FileGenerator<ServiceGeneratorOptions>
    {
        class RpcTypeInfo
        {
            public bool IsResponse;
            public bool IsRequest;
            public MessageDeclaration Type;
            public bool VisitedRequest;
            public bool VisitedResponse;

        }

        private readonly IEnumerable<ProtoFile> _protos;
        private readonly ModelGeneratorOptions _modelOptions;

        public GrpcMappingGenerator(ProtoFile entry, IEnumerable<ProtoFile> protos, ServiceGeneratorOptions option, ModelGeneratorOptions modelOptions)
            : base(entry, option)
        {
            this._protos = protos;
            this._modelOptions = modelOptions;
        }

        public override void GenerateCode()
        {
            Dictionary<string, RpcTypeInfo> typesMap = new Dictionary<string, RpcTypeInfo>();

            foreach (var proto in _protos)
            {
                foreach (var decl in proto.Declarations)
                {
                    if (decl is ServiceDeclaration service)
                    {
                        foreach (var rpc in service.Rpcs)
                        {
                            if (rpc.IsExtension)
                                continue;

                            if (rpc.RequestType is MessageDeclaration requestMsg && (service.Option.GrpcProxy || rpc.Option.GrpcMappingRequest))
                            {
                                AddTypes(requestMsg, 0, typesMap);
                            }

                            if (rpc.ResponseType is MessageDeclaration responseMsg && (service.Option.GrpcProxy || rpc.Option.GrpcMappingResponse))
                            {
                                AddTypes(responseMsg, 1, typesMap);
                            }
                        }
                    }
                    else if(decl is MessageDeclaration msg)
                    {
                        if (msg.Option.GrpcRequest)
                        {
                            AddTypes(msg, 0, typesMap);
                        }
                        if (msg.Option.GrpcResponse)
                        {
                            AddTypes(msg, 1, typesMap);
                        }
                    }
                }       
            }

            if (!typesMap.Any())
                return;

            var ns = _option.ImplementationNamespace ?? $"{Proto.Option.Namespace ?? Proto.Filename.Pascal()}.Services";           
            var modelNs = _modelOptions.Namespace ?? $"{Proto.Option.Namespace ?? Proto.Filename.Pascal()}.Models";

            var writer = CreateWriter(ns);

            writer.Usings.Append("using System;").AppendLine();                       
            writer.Usings.Append("using System.Linq;").AppendLine();

            writer.Usings.AppendLine().Append($"using mds = global::{modelNs};").AppendLine();


            var clsWriter = writer.Class;

            clsWriter.Append("public static class ProtobufMappingExtensions").AppendLine().Append("{").AppendLine();
            clsWriter.Append('\t', 1);

            var bodyWriter = clsWriter.Block("BODY");

            foreach (var (key, item) in typesMap)
            {

                if (item.IsRequest)
                {
                    GenerateModelToProtobufMapping(item.Type, bodyWriter);
                }

                if (item.IsResponse)
                {
                    GenerateProtobufToPocoMapping(item.Type, bodyWriter);
                }
            }

            clsWriter.Append("}").AppendLine();

            writer.SaveTo(_option.ImplementationOutput, $"ProtobufMappingExtensions");

        }

        private void GenerateModelToProtobufMapping(MessageDeclaration type, CodeWriter writer)
        {
            var proto = type.ProtoDeclaration;
            var typeName = type.GetFullTypeName();
            var grpcTypeName = $"{proto.Option.CSharpNamespace}.{type.GetProtobufName()}";

            writer.Append($"public static global::{grpcTypeName} ToProtobufModel(this mds::{typeName} model)")
                .AppendLine().Append("{").AppendLine().Append('\t', 1);

            var bodyWriter = writer.Block($"ToProtobufModel_{typeName.Replace('.', '_')}_BODY");

            bodyWriter.Append($"if(model == null) return null;").AppendLine(2);

            bodyWriter.Append($"global::{grpcTypeName} result = new global::{grpcTypeName}();").AppendLine();

            foreach (var field in type.Fields)
            {
                if (field.IsExtension || field.Option.GrpcOption.NotMapped)
                    continue;

                var fieldName = field.Name.Pascal();
                var fieldType = field.FieldType;

                if (field.Type.IsArray)
                {
                    bodyWriter.Append($"if(model.{fieldName} != null) ");

                    var selector = ConvertToGrpc("x", fieldType);
                    if (selector == "x")
                    {
                        bodyWriter.Append($"result.{fieldName}.AddRange(model.{fieldName});").AppendLine();
                    }
                    else
                    {
                        bodyWriter.Append($"result.{fieldName}.AddRange(model.{fieldName}.Select(x => {selector} ));").AppendLine();
                    }
                }
                else if (!field.Type.IsMap)                
                {
                    var path = ConvertToGrpc($"model.{fieldName}", fieldType);
                    bodyWriter.Append($"result.{fieldName} = {path};").AppendLine();
                }
            }

            bodyWriter.Append("return result;").AppendLine();

            writer.Append("}").AppendLine(2);
        }

        private void GenerateProtobufToPocoMapping(MessageDeclaration type, CodeWriter writer)
        {
            var proto = type.ProtoDeclaration;
            var typeName = type.GetFullTypeName();
            var grpcTypeName = $"{proto.Option.CSharpNamespace}.{type.GetProtobufName()}";

            writer.Append($"public static mds::{typeName} ToPocoModel(this global::{grpcTypeName} model)")
                .AppendLine().Append("{").AppendLine().Append('\t', 1);

            var bodyWriter = writer.Block($"ToPocoModel_{typeName.Replace('.', '_')}_BODY");

            bodyWriter.Append($"if(model == null) return null;").AppendLine(2);

            bodyWriter.Append($"mds::{typeName} result = new mds::{typeName}();").AppendLine();

            foreach (var field in type.Fields)
            {
                if (field.IsExtension || field.Option.GrpcOption.NotMapped)
                    continue;

                var fieldName = field.Name.Pascal();
                var fieldType = field.FieldType;

                if (field.Type.IsArray)
                {                  
                    var selector = ConvertToPoco("x", fieldType);
                    if (selector == "x")
                    {
                        bodyWriter.Append($"result.{fieldName} = model.{fieldName}.ToList();").AppendLine();
                    }
                    else
                    {
                        bodyWriter.Append($"result.{fieldName} = model.{fieldName}.Select(x => {selector}).ToList();").AppendLine();
                    }
                }
                else if (!field.Type.IsMap)                
                {
                    var path = ConvertToPoco($"model.{fieldName}", fieldType);
                    bodyWriter.Append($"result.{fieldName} = {path};").AppendLine();
                }
            }

            bodyWriter.Append("return result;").AppendLine();

            writer.Append("}").AppendLine(2);
        }

        private string ConvertToGrpc(string fieldName, ITypeDeclaration fieldType)
        {
            if (PrimitiveType.TimeStamp == fieldType)
            {
                return $"{fieldName}.HasValue ? Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.SpecifyKind({fieldName}.Value, DateTimeKind.Utc)) : null";
            }
            else if(PrimitiveType.Datetime == fieldType)
            {
                return $"Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.SpecifyKind({fieldName}, DateTimeKind.Utc))";
            }
            else if (PrimitiveType.Duration == fieldType)
            {
                return $"{fieldName}.HasValue ? Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan({fieldName}.Value.TimeOfDay) : null";
            }
            else if (PrimitiveType.StringValue == fieldType)
            {
                return $"{ fieldName}";
            }
            else if (PrimitiveType.String == fieldType)
            {                
                return $"{ fieldName} ?? string.Empty";
            }
            else if(PrimitiveType.Bytes == fieldType)
            {
                return $"{fieldName} != null ? Google.Protobuf.ByteString.CopyFrom({fieldName}) : Google.Protobuf.ByteString.Empty";
            }
            else if(PrimitiveType.Stream == fieldType)
            {
                return $"{fieldName} != null ? Google.Protobuf.ByteString.FromStream({fieldName}) : Google.Protobuf.ByteString.Empty";
            }
            else if (fieldType is MessageDeclaration)
            {
                return $"ToProtobufModel({fieldName})";
            }
            else if(fieldType is EnumDeclaration e)
            {
                var grpcTypeName = $"global::{e.ProtoDeclaration.Option.CSharpNamespace}.{e.GetProtobufName()}";
                return $"({grpcTypeName}){fieldName}";
            }
            else
            {
                return fieldName;
            }
        }

        private string ConvertToPoco(string fieldName, ITypeDeclaration fieldType)
        {
            if (PrimitiveType.TimeStamp == fieldType)
            {
                return $"{fieldName}?.ToDateTime()";
            }            
            else if (PrimitiveType.Duration.Equals(fieldType))
            {
                return $"{fieldName} != null ? DateTime.UnixEpoch.Add({fieldName}.ToTimeSpan()) : new DateTime?()";
            }
            else if (fieldType is MessageDeclaration)
            {
                return $"ToPocoModel({fieldName})";
            }
            else if (fieldType is EnumDeclaration)
            {
                return $"(mds::{fieldType.GetFullTypeName()}){fieldName}";
            }
            else if (PrimitiveType.Bytes == fieldType)
            {
                return $"{fieldName}?.ToByteArray()";
            }
            else if (PrimitiveType.Stream == fieldType)
            {
                return $"{fieldName} != null ? new System.IO.MemoryStream({fieldName}.ToByteArray()) : null";
            }
            else
            {
                return fieldName;
            }
        }

        private void AddTypes(MessageDeclaration type, int position, Dictionary<string, RpcTypeInfo> typesMap)
        {
            RpcTypeInfo info;

            if (!typesMap.TryGetValue(type.Name, out info))
            {
                info = new RpcTypeInfo
                {
                    Type = type,
                    IsRequest = position == 0,
                    IsResponse = position == 1
                };

                typesMap.Add(type.Name, info);
            }            

            if(info.Type != type)
            {
                info.Type = type;
            }

            if (position == 0)
            {
                if (info.VisitedRequest) return;

                if (!info.IsRequest) info.IsRequest = true;
                info.VisitedRequest = true;
            }                

            if(position == 1)
            {
                if (info.VisitedResponse) return;
                info.VisitedResponse = true;
                if (!info.IsResponse) info.IsResponse = true;
            }

            foreach (var fieldType in type.Fields.Where(x=>x.FieldType is MessageDeclaration).Select(x=>x.FieldType).Cast<MessageDeclaration>())
            {
                AddTypes(fieldType, position, typesMap);
            }
        }

    }
}
