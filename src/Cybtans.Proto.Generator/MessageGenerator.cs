using Cybtans.Entities;
using Cybtans.Proto.AST;
using Cybtans.Proto.Generators;
using Cybtans.Proto.Generators.CSharp;
using Cybtans.Proto.Options;
using Cybtans.Proto.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using static Cybtans.Proto.Generator.TemplateManager;

namespace Cybtans.Proto.Generator
{
    public class MessageGenerator : IGenerator
    {
        public class GenerationOptions
        {
            public string ProtoOutputFilename { get; set; }

            public string AssemblyFilename { get; set; }            

            public bool GenerateAutoMapperProfile { get; set; }

            public bool GenerateCode => !string.IsNullOrEmpty(ServiceName) && !string.IsNullOrEmpty(ServiceDirectory);

            public string ServiceName { get; set; }

            public string ServiceDirectory { get; set; }

            public string[] Imports { get; set; }

            public GrpcCompatibility? Grpc { get; set; } = new GrpcCompatibility();

            public string? NameTemplate { get; set; }

            public bool GenerateGraphQl { get; set; }

            public string AutoMapperOutput { get; set; }

            public string Namespace { get; set; }
            public bool GenerateMapping { get; set; }

            public bool IsValid()
            {
                return !string.IsNullOrEmpty(AssemblyFilename)
                    && !string.IsNullOrEmpty(ProtoOutputFilename);
            }

            public string GetMappingOutputPath()
            {
                string path;
                if (AutoMapperOutput != null)
                {
                    path = AutoMapperOutput;
                }
                else if(ServiceName != null && ServiceDirectory != null)
                {
                    path = Path.Combine(ServiceDirectory, $"{ServiceName}.Services", "Generated", "Mappings");
                }
                else
                {
                    return null;
                }

                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                return Path.Combine(path, "GeneratedAutoMapperProfile.cs");
            }

            public string GetModelMappingOutputDirectory( )
            {
                string path;
                if (AutoMapperOutput != null)
                {
                    path = AutoMapperOutput;
                }
                else if (ServiceName != null && ServiceDirectory != null)
                {
                    path = Path.Combine(ServiceDirectory, $"{ServiceName}.Services", "Generated", "Mappings");
                }
                else
                {
                    return null;
                }

                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                return path;
            }

            public string GetServiceImplOutputPath()
            {
                var path = Path.Combine(ServiceDirectory, $"{ServiceName}.Services", "Generated");
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                return path;
            }

            public string GetRestAPIOutputPath()
            {
                var path = Path.Combine(ServiceDirectory, $"{ServiceName}.RestApi");
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                return path;
            }
        }

        public bool CanGenerate(string value)
        {
            return value == "messages" || value == "m";
        }

        public bool Generate(string[] args)
        {
            if (args == null || args.Length == 0 || !CanGenerate(args[0]))
                return false;

            var options = new GenerationOptions();

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
                        PrintHelp();

                        return false;
                    }

                    value = args[i];
                }

                switch (arg)
                {
                    case "-o":
                        options.ProtoOutputFilename = value;
                        break;
                    case "-assembly":
                        options.AssemblyFilename = value;
                        break;
                    case "-service":
                        options.ServiceName = value;
                        break;
                    case "-service-o":
                        options.ServiceDirectory = value;
                        break;
                    case "-imports":
                        options.Imports = value.Split(",");
                        break;
                }
            }

            if (!options.IsValid())
            {
                PrintHelp();
                return false;
            }

            GenerateProto(options);

            return true;
        }      

        public bool Generate(CybtansConfig config, GenerationStep step)
        {
            if (!CanGenerate(step.Type))
                return false;

            var protoStep = config.Steps.FirstOrDefault(x => x.Type == "proto");

            var options = new GenerationOptions()
            {
                ProtoOutputFilename = Path.Combine(config.Path, step.ProtoFile),
                AssemblyFilename = Path.Combine(config.Path, step.AssemblyFile),
                Imports = step.Imports,
                ServiceName = config.Service,
                ServiceDirectory = step.Output != null ? Path.Combine(config.Path, step.Output) : null,
                Grpc = step.Grpc,
                NameTemplate = step.NameTemplate,
                GenerateGraphQl = step.GenerateGraphQLQuery,
                Namespace = protoStep?.Models?.Namespace ?? $"{config.Service}.Models",
                GenerateMapping = step.GenerateMappings,
                GenerateAutoMapperProfile = step.GenerateAutoMapperProfile
            };

            if (step.AutoMapperOutput != null)
            {
                options.AutoMapperOutput = Path.Combine(config.Path, step.AutoMapperOutput);
            }

            if (options.Grpc.MappingOutput != null)
            {
                options.Grpc.MappingOutput = Path.Combine(config.Path, options.Grpc.MappingOutput);
            }
            
            GenerateProto(options);

            return true;
        }

        private void GenerateProto(GenerationOptions options)
        {
            Console.WriteLine($"Generating proto from {options.AssemblyFilename}");

            using var loader = new Cybtans.Proto.Utils.AssemblyLoader(options.AssemblyFilename);
            var exportedTypes = loader.LoadTypes();
            var types = GenerateMessages(options, exportedTypes);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Proto generated at {options.ProtoOutputFilename}");
            Console.ResetColor();

            if (types.Any())
            {
                //ProtoGenerator protoGenerator = new ProtoGenerator();
                //protoGenerator.Generate(new[] {
                //    "proto",
                //    "-n", options.ServiceName,
                //    "-o", options.ServiceDirectory,
                //    "-f",  options.ProtoOutputFilename });

                if (!options.GenerateMapping && (options.AutoMapperOutput != null || (options.ServiceName != null && options.ServiceDirectory != null)))
                {
                    GenerateMappings(types, options);
                }
                else if (options.GenerateMapping && options.Namespace != null)
                {
                    GenerateModelMapping(types, options);
                }

                if (options.GenerateCode && options.ServiceName != null && options.ServiceDirectory != null)
                {
                    GenerateServicesImplementation(types, options);
                }
            }
        }

        public void PrintHelp()
        {
            Console.WriteLine("Messages options are:");
            Console.WriteLine("m|messages : Generates a proto file from an assembly");            
            Console.WriteLine("-o : The output filename");
            Console.WriteLine("-assembly : The models assembly");            
            Console.WriteLine("-service : Service Name");
            Console.WriteLine("-service-o : Service root directory");
            Console.WriteLine("-imports : Comma separated import paths for protobuff");
            Console.WriteLine("Example: cybtans-cli m -o Service1/Protos/Models.proto -assembly Services1.Data.dll");
            Console.WriteLine("       : cybtans-cli m -o Service1/Protos/Models.proto -assembly Services1.Data.dll -service Service1 -service-o Service1 -imports ./Common.proto");
        }

        #region Proto Generation

        private string GetTypeName(Type type, GenerationOptions options)
        {
            if (options.Grpc.Enable)
            {
                if (type == typeof(DateTime) || type == typeof(DateTime?))
                    return PrimitiveType.TimeStamp.Name;
                else if (type == typeof(TimeSpan) || type == typeof(TimeSpan?))
                    return PrimitiveType.Duration.Name;
                else if (type == typeof(int?)) return PrimitiveType.Int32Value.Name;
                else if (type == typeof(int?)) return PrimitiveType.Int32Value.Name;
                else if (type == typeof(uint?)) return PrimitiveType.UInt32Value.Name;
                else if (type == typeof(long?)) return PrimitiveType.Int64Value.Name;
                else if (type == typeof(ulong?)) return PrimitiveType.UInt64Value.Name;
                else if (type == typeof(Guid)) return PrimitiveType.String.Name;
                else if (type == typeof(Guid?)) return PrimitiveType.StringValue.Name;
            }

            if (type.IsEnum)
                return type.Name.Pascal();

            var primitive = PrimitiveType.GetPrimitiveType(type);
            if (primitive != null)
                return primitive.Name;

            return GetMessageName(type, options.NameTemplate);
        }

        private string GetMessageName(Type type, string template)
        {
            var attr = type.GetCustomAttribute<GenerateMessageAttribute>(true);
            if (attr?.Name != null)
                return attr.Name;
            else if (template != null)
                return TemplateProcessor.Process(template, new { Name = type.Name.Pascal() });

            return type.Name.Pascal() + "Dto";
        }


        private HashSet<Type> GenerateMessages(GenerationOptions options, IEnumerable<Type> types)
        {
            string outputFilename = options.ProtoOutputFilename;            

            CodeWriter codeWriter = new CodeWriter();
            codeWriter.Append(CodeWriter.Header).AppendLine(2);

            codeWriter.Append("syntax = \"proto3\";").AppendLine(2);

            if (options.GenerateCode)
            {
                codeWriter.Append($"package {options.ServiceName};").AppendLine(2);
            }

            if (options.Grpc.Enable)
            {
                codeWriter.Append($"option csharp_namespace = \"{options.Grpc.GrpcNamespace}\";").AppendLine(2);

                codeWriter.Append("import \"google/protobuf/timestamp.proto\";").AppendLine();
                codeWriter.Append("import \"google/protobuf/duration.proto\";").AppendLine(2);
            }

            if(options.Imports!= null)
            {
                foreach (var import in options.Imports)
                {
                    codeWriter.Append($"import \"{import}\";").AppendLine();
                }

                codeWriter.AppendLine();
            }

            var generated = new HashSet<Type>();
            foreach (var type in types)
            {
                if (generated.Contains(type))                
                    continue;                

                var attr = type.GetCustomAttribute<GenerateMessageAttribute>(true);
                if (attr == null)
                    continue;

                if (type.IsEnum)
                {
                    GenerateEnum(type, codeWriter, generated);
                }
                else if(type.IsClass && !type.IsAbstract)
                {                                        
                    GenerateMessage(type, codeWriter, generated, new HashSet<Type>(), options);
                }
            }

            var path = Path.GetDirectoryName(outputFilename);
            if (!string.IsNullOrEmpty(path) && path != "." && path != "..")
            {
                Directory.CreateDirectory(path);
            }

            if (!options.Grpc.Enable)
            {
                GenerateServices(codeWriter, generated, options);
            }
            else if(options.Grpc.MappingOutput != null)
            {
                GenerateGrpcMapping(generated, options);
            }

            File.WriteAllText(outputFilename, codeWriter.ToString());
            return generated;
        }

        private void GenerateEnum(Type type, CodeWriter codeWriter, HashSet<Type> generated)
        {           
            if (generated.Contains(type))
                return;         

            generated.Add(type);

            codeWriter.Append($"enum { type.Name.Pascal() } {{").AppendLine();

            bool hasMessageOptions = false;
            if (type.GetCustomAttribute<DescriptionAttribute>() != null)
            {
                var description = type.GetCustomAttribute<DescriptionAttribute>();
                codeWriter.Append('\t', 1).Append($"option (description) = \"{description.Description}\";").AppendLine();
                hasMessageOptions = true;
            }

            if (type.GetCustomAttribute<ObsoleteAttribute>() != null)
            {
                codeWriter.Append('\t', 1).Append($"option deprecated = true;").AppendLine();
                hasMessageOptions = true;
            }

            if (hasMessageOptions)
            {
                codeWriter.AppendLine();
            }

            var members = Enum.GetNames(type);
            foreach (var item in members)
            {              
                var value = Convert.ToInt32(Enum.Parse(type, item));
                codeWriter.Append('\t', 1).Append($"{item} = {value}");

                var member = type.GetField(item);
                if(member.GetCustomAttribute<DescriptionAttribute>() != null)
                {
                    var attr = member.GetCustomAttribute<DescriptionAttribute>();
                    codeWriter.Append($" [(description) = \"{attr.Description}\"]");
                }

                codeWriter.Append(";");

                codeWriter.AppendLine();
            }
            
            codeWriter.Append("}");
            codeWriter.AppendLine(2);
        }

        private void GenerateMessage(Type type, CodeWriter codeWriter, HashSet<Type> generated, HashSet<Type> visited, GenerationOptions options)
        {
            var attr = type.GetCustomAttribute<GenerateMessageAttribute>(true);
            if (attr == null || generated.Contains(type))
                return;           

            generated.Add(type);          

            codeWriter.Append($"message { GetTypeName(type, options) } {{");
            codeWriter.AppendLine();

            bool hasMessageOptions = false;
            if (type.GetCustomAttribute<DescriptionAttribute>() != null)
            {
                var description = type.GetCustomAttribute<DescriptionAttribute>();
                codeWriter.Append('\t', 1).Append($"option (message_description) = \"{description.Description}\";").AppendLine();
                hasMessageOptions = true;
            }

            if (type.GetCustomAttribute<ObsoleteAttribute>() != null)
            {
                codeWriter.Append('\t', 1).Append($"option deprecated = true;").AppendLine();
                hasMessageOptions = true;
            }

            if (hasMessageOptions)
            {
                codeWriter.AppendLine();
            }

            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var counter = 1;
            List<Type> types = new();

            foreach (var p in props)
            {
                if (p.GetCustomAttribute<MessageExcludedAttribute>() != null ||
                    p.DeclaringType.FullName.StartsWith("Cybtans.Entities.DomainTenantEntity") ||
                    p.DeclaringType.FullName.StartsWith("Cybtans.Entities.TenantEntity"))
                    continue;

                if (p.DeclaringType.FullName.StartsWith("Cybtans.Entities.DomainAuditableEntity") ||
                    p.DeclaringType.FullName.StartsWith("Cybtans.Entities.AuditableEntity"))
                {
                    if (p.Name == "Creator")
                        continue;
                }

                Type propertyType = p.PropertyType;
                bool repeated = false;

                if (propertyType.IsArray && propertyType != typeof(byte[]))
                {
                    propertyType = propertyType.GetElementType();
                    repeated = true;
                }
                else if (IsCollection(propertyType))
                {
                    propertyType = propertyType.GetGenericArguments()[0];
                    repeated = true;
                }

                bool optional = propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Nullable<>);
                if (optional)
                {
                    propertyType = propertyType.GetGenericArguments()[0];
                }

                var isPrimitive = PrimitiveType.GetPrimitiveType(propertyType) != null;
                var propertyTypeAttr = propertyType.GetCustomAttribute<GenerateMessageAttribute>(true);

                if (visited.Contains(propertyType) || (!isPrimitive && !propertyType.IsEnum && propertyTypeAttr == null))
                    continue;

                codeWriter.Append('\t', 1);

                if (repeated)
                {
                    codeWriter.Append("repeated ");
                }

                codeWriter.Append($"{GetTypeName(propertyType, options)} {p.Name.Camel()} = {counter++}");

                if (!options.Grpc.Enable)
                {
                    AppendOptions(codeWriter, p, optional);
                }

                codeWriter.Append(";");
                codeWriter.AppendLine();

                if (!isPrimitive && !generated.Contains(propertyType) && (propertyTypeAttr != null || propertyType.IsEnum))
                {
                    types.Add(propertyType);
                }
            }

            codeWriter.Append("}");
            codeWriter.AppendLine(2);

            visited.Add(type);

            foreach (var t in types)
            {
                if (t.IsClass)
                {
                    GenerateMessage(t, codeWriter, generated, new HashSet<Type>(visited), options);
                }
                else if (t.IsEnum)
                {
                    GenerateEnum(t, codeWriter, generated);
                }
            }
        }

        private static bool IsCollection(Type type)
        {
            if(type.IsGenericType)
            {
                var genDef = type.GetGenericTypeDefinition();
                return genDef == typeof(ICollection<>) || genDef == typeof(List<>) || genDef == typeof(HashSet<>);
            }
            return false;
        }

        private static void AppendOptions(CodeWriter codeWriter, PropertyInfo p, bool optional)
        {
            var options = new List<string>();
            if (optional ||              
                p.DeclaringType.FullName.StartsWith("Cybtans.Entities.DomainAuditableEntity") ||
                p.DeclaringType.FullName.StartsWith("Cybtans.Entities.AuditableEntity"))
            {
                options.Add("(optional) = true");
            }
            else if (p.GetCustomAttribute<RequiredAttribute>() != null)
            {
                options.Add("(required) = true");
            }
            
            if (p.GetCustomAttribute<DescriptionAttribute>() != null)
            {
                var attr = p.GetCustomAttribute<DescriptionAttribute>();
                options.Add($"(field_description) = \"{attr.Description}\"");
            }

            if (p.GetCustomAttribute<ObsoleteAttribute>() != null)
            {
                options.Add("(deprecated) = true");
            }
            
            if(p.GetCustomAttribute<ProtoFieldAttribute>() != null)
            {
                var attr = p.GetCustomAttribute<ProtoFieldAttribute>();
                if (attr.TsOptional)
                {
                    options.Add("(ts).optional = true");
                }
                if (attr.TsPartial)
                {
                    options.Add("(ts).partial = true");
                }
                if(attr.Default != null)
                {
                    if(attr.Default is string str)
                    {
                        options.Add($"(default) = \"{str.Replace("\"","\\\"")}\"");
                    }
                    else if(attr.Default is bool)
                    {
                        options.Add($"(default) = {attr.Default.ToString().ToLowerInvariant()}");
                    }
                    else
                    {
                        options.Add($"(default) = {attr.Default}");
                    }
                }
            }

            if (options.Any())
            {
                codeWriter.Append(" [");
                codeWriter.Append(string.Join(", ", options));
                codeWriter.Append("]");
            }
        }

        private void GenerateServices(CodeWriter codeWriter, HashSet<Type> types, GenerationOptions options)
        {            
            codeWriter.AppendLine();

            if (types.Any(type =>
            {
                var attr = type.GetCustomAttribute<GenerateMessageAttribute>(true);
                return attr != null && attr.Service != ServiceType.None;
            }))
            {
                codeWriter.Append(GetAllTemplate);
            }

            foreach (var type in types)
            {
                var attr = type.GetCustomAttribute<GenerateMessageAttribute>(true);
                if (attr == null || attr.Service == ServiceType.None || !type.IsClass)
                    continue;

                PropertyInfo IdProp = GetKey(type);

                if (IdProp == null)
                    continue;                                

                codeWriter.AppendLine();

                if (attr.Service == ServiceType.ReadOnly)
                {
                    codeWriter.Append(TemplateProcessor.Process(GetRawTemplate("ReadOnlyProtoServices.tpl"), new
                    {
                        SERVICE_NAME = $"I{type.Name.Pascal()}Service",
                        ENTITY = type.Name.Pascal(),
                        ID_TYPE = GetTypeName(IdProp.PropertyType, options),
                        ID = IdProp.Name.Camel(),
                        ENTITYDTO = GetTypeName(type, options),
                        GetAll_OPTIONS = GetOptions(type, attr, "GetAll", options),
                        Get_OPTIONS = GetOptions(type, attr, "Get", options),                       
                    }));
                }
                else
                {
                    codeWriter.Append(TemplateProcessor.Process(GetRawTemplate("ProtoServices.tpl"), new
                    {
                        SERVICE_NAME = $"I{type.Name.Pascal()}Service",
                        ENTITY = type.Name.Pascal(),
                        ID_TYPE = GetTypeName(IdProp.PropertyType, options),
                        ID = IdProp.Name.Camel(),
                        ENTITYDTO = GetTypeName(type, options),
                        GetAll_OPTIONS = GetOptions(type, attr, "GetAll", options),
                        Get_OPTIONS = GetOptions(type, attr, "Get", options),
                        Create_OPTIONS = GetOptions(type, attr, "Create", options),
                        Update_OPTIONS = GetOptions(type, attr, "Update", options),
                        Delete_OPTIONS = GetOptions(type, attr, "Delete", options),
                    }));
                }

            }
        }

        private string GetOptions(Type type, GenerateMessageAttribute attr, string rpc, GenerationOptions options)
        {
            var sb = new StringBuilder();

            var security = GetSecurity(attr, rpc);
            if (security != null)
            {
                sb.Append(security).AppendLine();
            }

            var typeName = GetTypeName(type, options);
            if (rpc == "GetAll")
            {
                sb.Append($"option (description) = \"Returns a collection of {typeName}\";").AppendLine();
                if (options.GenerateGraphQl)
                {
                    sb.Append($"option (graphql).query = \"{type.Name.Camel().Pluralize()}\";").AppendLine();
                }
            }
            else if (rpc == "Get")
            {
                sb.Append($"option (description) = \"Returns one {typeName} by Id\";").AppendLine();
                if (options.GenerateGraphQl)
                {
                    sb.Append($"option (graphql).query = \"{type.Name.Camel()}\";").AppendLine();
                }
            }
            else if (rpc == "Create")
            {
                sb.Append($"option (description) = \"Creates one {typeName}\";").AppendLine();
            }
            else if (rpc == "Update")
            {
                sb.Append($"option (description) = \"Updates one {typeName} by Id\";").AppendLine();
            }
            else if (rpc == "Delete")
            {
                sb.Append($"option (description) = \"Deletes one {typeName} by Id\";").AppendLine();
            }

            return sb.ToString();
        }

        private string GetSecurity(GenerateMessageAttribute attr, string rpc)
        {
            var security = new List<string>();
            if(attr.AllowedRead != null)
            {
                security.Add(attr.AllowedRead);
            }

            if((rpc == "Create" || rpc == "Update" || rpc == "Delete") && (attr.AllowedWrite !=null && attr.AllowedWrite != attr.AllowedRead))
            {
                security.Add(attr.AllowedWrite);
            }

            var names = string.Join(",", security);
            return attr.Security switch
            {                
                SecurityType.Policy => $"option (policy) = \"{names}\";",
                SecurityType.Role => $"option (roles) = \"{names}\";",
                SecurityType.Authorized => $"option (authorized) = true;",
                SecurityType.AllowAnonymous => $"option (anonymous) = true;", 
                _ => null
            };
        }

        private bool IsGenerated(Type type)
        {           
            var isPrimitive = PrimitiveType.GetPrimitiveType(type) != null;
            var propertyTypeAttr = type.GetCustomAttribute<GenerateMessageAttribute>(true);

            if (!isPrimitive && !type.IsEnum && propertyTypeAttr == null)
                return false;

            return true;
        }

        #region Mapping

        public void GenerateGrpcMapping(HashSet<Type> types, GenerationOptions generationOptions)
        {
            var options = generationOptions.Grpc;
            if (options.GrpcNamespace == null)
            {
                throw new InvalidOperationException("GrpcNamespace not defined in cybtans.json");
            }

            if(options.MappingNamespace == null)
            {
                throw new InvalidOperationException("MappingNamespace not defined in cybtans.json");
            }                        

            var writer = new CsFileWriter(options.MappingNamespace, options.MappingOutput);

            writer.Usings.Append("using System;").AppendLine();
            writer.Usings.Append("using System.Collections.Generic;").AppendLine();
            writer.Usings.Append("using System.Linq;").AppendLine();         

            var clsWriter = writer.Class;

            clsWriter.Append("public static class GrpcMappingExtensions").AppendLine().Append("{").AppendLine();
            clsWriter.Append('\t', 1);

            var bodyWriter = clsWriter.Block("BODY");

            foreach (var type in types)
            {
                if (type.IsClass && !type.IsAbstract)
                {
                    GenerateModelToProtobufMapping(type, bodyWriter, generationOptions);

                    GenerateProtobufToPocoMapping(type, bodyWriter, generationOptions);
                }
            }

            clsWriter.Append("}").AppendLine();

            writer.Save("ProtobufMappingExtensions");
    
        }
      
        private void GenerateModelToProtobufMapping(Type type, CodeWriter writer, GenerationOptions generationOptions)
        {
            GrpcCompatibility options = generationOptions.Grpc;

            var typeName = type.FullName;
            var grpcTypeName = $"{options.GrpcNamespace}.{GetMessageName(type, generationOptions.NameTemplate)}";

            writer.Append($"public static global::{grpcTypeName} ToProtobufModel(this global::{typeName} model)")
                .AppendLine().Append("{").AppendLine().Append('\t', 1);

            var bodyWriter = writer.Block($"ToProtobufModel_{type.Name}_BODY");

            bodyWriter.Append($"if(model == null) return null;").AppendLine(2);

            bodyWriter.Append($"global::{grpcTypeName} result = new global::{grpcTypeName}();").AppendLine();

            foreach (var field in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var fieldName = field.Name;
                var fieldType = field.PropertyType;

                if (field.GetCustomAttribute<MessageExcludedAttribute>() != null ||
                   field.DeclaringType.FullName.StartsWith("Cybtans.Entities.DomainTenantEntity") ||
                   field.DeclaringType.FullName.StartsWith("Cybtans.Entities.TenantEntity"))
                    continue;

                if (field.DeclaringType.FullName.StartsWith("Cybtans.Entities.DomainAuditableEntity") ||
                    field.DeclaringType.FullName.StartsWith("Cybtans.Entities.AuditableEntity"))
                {
                    if (field.Name == "Creator")
                        continue;
                }              
                
                if (IsArray(fieldType, out var elementType))
                {
                    if (!IsGenerated(elementType))
                        continue;

                    bodyWriter.Append($"if(model.{fieldName} != null) ");

                    var selector = ConvertToGrpc("x", elementType, options);
                    if (selector == "x")
                    {
                        bodyWriter.Append($"result.{fieldName}.AddRange(model.{fieldName});").AppendLine();
                    }
                    else
                    {
                        bodyWriter.Append($"result.{fieldName}.AddRange(model.{fieldName}.Select(x => {selector}));").AppendLine();
                    }
                }               
                else
                {
                    if (!IsGenerated(fieldType))
                        continue;

                    var path = ConvertToGrpc($"model.{fieldName}", fieldType, options);
                    bodyWriter.Append($"result.{fieldName} = {path};").AppendLine();
                }
            }

            bodyWriter.Append("return result;").AppendLine();

            writer.Append("}").AppendLine(2);
        }

        private void GenerateProtobufToPocoMapping(Type type, CodeWriter writer, GenerationOptions generationOptions)
        {
            GrpcCompatibility options = generationOptions.Grpc;
            var typeName = type.FullName;
            var grpcTypeName = $"{options.GrpcNamespace}.{GetMessageName(type, generationOptions.NameTemplate)}";

            writer.Append($"public static {typeName} ToPocoModel(this global::{grpcTypeName} model)")
                .AppendLine().Append("{").AppendLine().Append('\t', 1);

            var bodyWriter = writer.Block($"ToPocoModel_{type.Name}_BODY");

            bodyWriter.Append($"if(model == null) return null;").AppendLine(2);

            bodyWriter.Append($"global::{typeName} result = new global::{typeName}();").AppendLine();

            foreach (var field in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var fieldName = field.Name;
                var fieldType = field.PropertyType;

                if (field.GetCustomAttribute<MessageExcludedAttribute>() != null ||
                   !field.CanWrite ||
                   field.DeclaringType.FullName.StartsWith("Cybtans.Entities.DomainTenantEntity") ||
                   field.DeclaringType.FullName.StartsWith("Cybtans.Entities.TenantEntity"))
                        continue;

                if (field.DeclaringType.FullName.StartsWith("Cybtans.Entities.DomainAuditableEntity") ||
                    field.DeclaringType.FullName.StartsWith("Cybtans.Entities.AuditableEntity"))
                {
                    if (field.Name == "Creator")
                        continue;
                }
                

                if (IsArray(fieldType, out var elementType))
                {
                    if (!IsGenerated(elementType))
                        continue;

                    var selector = ConvertToPoco("x", elementType, options);
                    if (selector == "x")
                    {
                        bodyWriter.Append($"result.{fieldName} = model.{fieldName}.ToList();").AppendLine();
                    }
                    else
                    {
                        bodyWriter.Append($"result.{fieldName} = model.{fieldName}.Select(x => {selector}).ToList();").AppendLine();
                    }
                }
                else
                {
                    if (!IsGenerated(fieldType))
                        continue;

                    var path = ConvertToPoco($"model.{fieldName}", fieldType, options);
                    bodyWriter.Append($"result.{fieldName} = {path};").AppendLine();
                }
            }

            bodyWriter.Append("return result;").AppendLine();

            writer.Append("}").AppendLine(2);
        }

        private string ConvertToGrpc(string fieldName ,Type type, GrpcCompatibility options)
        {
            if (type == typeof(DateTime))
            {
                return $"Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.SpecifyKind({fieldName}, DateTimeKind.Utc))";
            }
            else if(type == typeof(DateTime?))
            {
                return $"{fieldName}.HasValue ? Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.SpecifyKind({fieldName}.Value, DateTimeKind.Utc)): null";
            }
            else if (type == typeof(TimeSpan?))
            {
                return $"{fieldName}.HasValue ? Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan({fieldName}.Value)";
            }
            else if(type == typeof(TimeSpan))
            {
                return $"Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan({fieldName})";
            }
            else if (type == typeof(string))
            {
                return $"{fieldName} ?? string.Empty";
            }
            else if(type == typeof(Guid))
            {
                return $"{fieldName}.ToString()";
            }
            else if (type == typeof(Guid?))
            {
                return $"{fieldName}?.ToString()";
            }
            else if (type.IsClass)
            {
                return $"ToProtobufModel({fieldName})";
            }
            else if (type.IsEnum)
            {
                var grpcTypeName = $"global::{options.GrpcNamespace}.{type.Name}";
                return $"({grpcTypeName})(int){fieldName}";
            }            
            else
            {
               type = Nullable.GetUnderlyingType(type);
                if(type != null)
                {
                    if (type.IsEnum)
                    {
                        var grpcTypeName = $"global::{options.GrpcNamespace}.{type.Name}";
                        return $"({grpcTypeName})({fieldName} ?? 0)";
                    }

                    return $"{fieldName} ?? default({type.Name})";
                }

                return fieldName;
            }
        }

        private string ConvertToPoco(string fieldName, Type type, GrpcCompatibility options)
        {
            if (type == typeof(DateTime))
            {
                return $"{fieldName}?.ToDateTime() ?? default(global::System.DateTime)";
            }
            else if (type == typeof(DateTime?))
            {
                return $"{fieldName}?.ToDateTime()";
            }
            else if (type == typeof(TimeSpan))
            {
                return $"{fieldName}?.ToTimeSpan() ?? default(global::System.TimeSpan)";
            }
            else if (type == typeof(TimeSpan))
            {
                return $"{fieldName}?.ToTimeSpan()";
            }
            else if (type.IsClass && type != typeof(string))
            {
                return $"ToPocoModel({fieldName})";
            }
            else if (type == typeof(Guid))
            {
                return $"!string.IsNullOrEmpty({fieldName}) ? Guid.Parse({fieldName}) : Guid.Empty";
            }
            else if (type == typeof(Guid?))
            {
                return $"!string.IsNullOrEmpty({fieldName}) ?  new Guid?(Guid.Parse({fieldName})) : new Guid?(null)";
            }
            else if (type.IsEnum)
            {
                return $"(global::{type.FullName}){fieldName}";
            }
            else
            {
                type = Nullable.GetUnderlyingType(type);
                if (type != null)
                {
                    if (type.IsEnum)
                    {                      
                        return $"(global::{type.FullName})(int)({fieldName})";
                    }
                    else if(type.IsPrimitive)
                    {
                        return $"{fieldName} != default ? {fieldName} : null";
                    }                   
                }

                return fieldName;
            }
        }

        #endregion

        private static bool IsArray(Type propertyType, out Type elementType)
        {
            bool repeated = false;
            elementType = null;

            if (propertyType.IsArray && propertyType != typeof(byte[]))
            {
                elementType = propertyType.GetElementType();
                repeated = true;
            }
            else if (propertyType.IsGenericType)
            {
                if (IsCollection(propertyType))
                {
                    elementType = propertyType.GetGenericArguments()[0];
                    repeated = true;
                }
            
            }

            return repeated;
        }
      
        private static PropertyInfo GetKey(Type type)
        {
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var IdProp = props.FirstOrDefault(x =>
            {
                var name = x.Name.ToLower();
                return name == "id" || name == $"{type.Name.ToLower()}id" || name == $"{type.Name.ToLower()}_id";
            });
            return IdProp;
        }

        string GetAllTemplate = @"
message GetAllRequest {
	string filter = 1 [optional = true];
	string sort = 2 [optional = true];
	int32 skip = 3 [optional = true];
	int32 take = 4 [optional = true];
}
";

        #endregion

        #region CSharp Generation

        private string GetNamespace(Type type)
        {
            var index = type.FullName.LastIndexOf(".");
            string ns = null;
            if (index > 0)
            {
                ns = type.FullName.Substring(0, index);
            }
            if (ns == null)
            {
                Console.WriteLine("Invalid Namespace Entity");              
            }
            return ns;
        }

        private void GenerateMappings(HashSet<Type> types, GenerationOptions options)
        {
            var writer = new CodeWriter();
            string ns = GetNamespace(types.First());
            if (ns == null)
            {                
                return;
            }

            foreach (var type in types)
            {
                if (!type.IsClass)
                    continue;

                writer.AppendLine();                
                writer.Append($"CreateMap<global::{type.FullName}, {GetTypeName(type, options)}>();").AppendLine();
                writer.Append($"CreateMap<{GetTypeName(type, options)}, global::{type.FullName}>();").AppendLine();
            }

            File.WriteAllText(options.GetMappingOutputPath(), 
            TemplateProcessor.Process(MappingTemplate, new
            {                
                SERVICE = options.ServiceName,
                MAPPINGS = writer.ToString()
            }));
        }

        private void GenerateServicesImplementation(HashSet<Type> types, GenerationOptions options)
        {          
            string ns = GetNamespace(types.First());
            if (ns == null)            
                return;

            foreach (var type in types)
            {
                var attr = type.GetCustomAttribute<GenerateMessageAttribute>(true);

                if (!type.IsClass || attr.Service == ServiceType.None || attr.Service == ServiceType.Interface)
                    continue;

                PropertyInfo IdProp = GetKey(type);

                if (IdProp == null)
                    continue;

                var primitiveType = PrimitiveType.GetPrimitiveType(IdProp.PropertyType);
                if (primitiveType == null)
                    continue;

                File.WriteAllText($"{options.GetServiceImplOutputPath()}/{type.Name}Service.cs",
                TemplateProcessor.Process(
                     attr.Service  == ServiceType.Default ? ServiceImpTemplate :
                     attr.Service == ServiceType.Partial ? ServiceImpPartialTemplate :
                     ReadOnlyServiceImpTemplate,
                    new
                    {
                        SERVICE_NAME = $"I{type.Name}Service",
                        ENTITIES_NAMESPACE = ns,
                        SERVICE = options.ServiceName,
                        ENTITY = type.Name,
                        TKEY = primitiveType.GetPrimitiveTypeName(),
                        TMESSAGE = GetTypeName(type, options)
                    }));

            }
        }

        public void GenerateModelMapping(HashSet<Type> types, GenerationOptions generationOptions)
        {           
            var writer = new CsFileWriter(generationOptions.ServiceName, generationOptions.GetModelMappingOutputDirectory());

            writer.Usings.Append("using System;").AppendLine();
            writer.Usings.Append("using System.Collections.Generic;").AppendLine();
            writer.Usings.Append("using System.Linq;").AppendLine();

            var clsWriter = writer.Class;

            clsWriter.Append("public static class ModelMappingExtensions").AppendLine().Append("{").AppendLine();
            clsWriter.Append('\t', 1);

            var bodyWriter = clsWriter.Block("BODY");

            foreach (var type in types)
            {
                if (type.IsClass && !type.IsAbstract)
                {
                    GenerateModelMapping(type, bodyWriter, generationOptions, true);

                    GenerateModelMapping(type, bodyWriter, generationOptions, false);
                }
            }

            clsWriter.Append("}").AppendLine();

            writer.Save("ModelMappingExtensions");

        }

        private void GenerateModelMapping(Type type, CodeWriter writer, GenerationOptions generationOptions, bool toData)
        {
            var dataType = type.FullName;
            var modelType = $"{generationOptions.Namespace}.{GetMessageName(type, generationOptions.NameTemplate)}";

            var srcTypeName = toData ? modelType : dataType;
            var destTypeName = toData ? dataType : modelType;

            CodeWriter bodyWriter;
            if (toData)
            {
                writer.Append($"public static global::{destTypeName} ToDataModel(this global::{srcTypeName} model)")
                   .AppendLine().Append("{").AppendLine().Append('\t', 1);

                bodyWriter = writer.Block($"ToDataModel_{type.Name}_BODY");
            }
            else
            {
                writer.Append($"public static global::{destTypeName} ToServiceModel(this global::{srcTypeName} model)")
                    .AppendLine().Append("{").AppendLine().Append('\t', 1);

                bodyWriter = writer.Block($"ToServiceModel_{type.Name}_BODY");

            }

            bodyWriter.Append($"if(model == null) return null;").AppendLine(2);
            bodyWriter.Append($"var result = new global::{destTypeName}();").AppendLine();

            foreach (var field in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
             
                if (field.GetCustomAttribute<MessageExcludedAttribute>() != null ||
                   field.DeclaringType.FullName.StartsWith("Cybtans.Entities.DomainTenantEntity") ||
                   field.DeclaringType.FullName.StartsWith("Cybtans.Entities.TenantEntity"))
                    continue;

                if (field.DeclaringType.FullName.StartsWith("Cybtans.Entities.DomainAuditableEntity") ||
                    field.DeclaringType.FullName.StartsWith("Cybtans.Entities.AuditableEntity"))
                {
                    if (field.Name == "Creator")
                        continue;
                }

                if(toData && !field.CanWrite)
                {
                    continue;
                }

                var fieldName = toData ? field.Name: field.Name.Pascal();
                var modelName = toData ? field.Name.Pascal() : field.Name;
                var fieldType = field.PropertyType;


                if (IsArray(fieldType, out var elementType))
                {
                    if (!IsGenerated(elementType))
                        continue;

                    var selector = GetFieldPath("x", elementType, generationOptions, toData);
                    
                    bodyWriter.Append($"if(model.{fieldName} != null) ");                  
                    if (selector == "x")
                    {
                        if (toData)
                        {
                            bodyWriter.Append($"result.{fieldName} = model.{modelName};").AppendLine();
                        }
                        else
                        {
                            bodyWriter.Append($"result.{fieldName} = new List<{elementType}>(model.{modelName});").AppendLine();
                        }
                    }
                    else
                    {                        
                        bodyWriter.Append($"result.{fieldName} = model.{modelName}.Select(x => {selector}).ToList();").AppendLine();
                    }                    
                }
                else
                {
                    if (!IsGenerated(fieldType))
                        continue;

                    var path = GetFieldPath($"model.{modelName}", fieldType, generationOptions, toData);
                    bodyWriter.Append($"result.{fieldName} = {path};").AppendLine();
                }
            }

            bodyWriter.Append("return result;").AppendLine();

            writer.Append("}").AppendLine(2);
        }

        private string GetFieldPath(string fieldName, Type type, GenerationOptions options, bool toData)
        {            
            if (type.IsClass && type != typeof(string))
            {
                return toData? $"ToDataModel({fieldName})" : $"ToServiceModel({fieldName})";
            }
            else if (type.IsEnum)
            {
                if (toData)
                {                    
                    return $"(global::{type.FullName})(int){fieldName}";
                }
                else
                {
                    var typeName = $"global::{options.Namespace}.{type.Name}";
                    return $"({typeName})(int){fieldName}";
                }
                
            }
            else if (type.IsPrimitive)
            {                
                return fieldName;
            }
            else
            {
                type = Nullable.GetUnderlyingType(type);
                if (type != null && type.IsEnum)
                {
                    if (toData)
                    {
                        return $"({fieldName} != null ? new {type.FullName} ? (({type.FullName}){fieldName}.Value) : ({type.FullName}?)null )";                       
                    }
                    else
                    {
                        var modelTypeName = $"global::{options.Namespace}.{type.Name}";
                        return $"({fieldName} != null ? new {modelTypeName} ? (({modelTypeName}){fieldName}.Value) : ({modelTypeName}?)null )";
                    }
                }

                return fieldName;
            }
        }


        private void GenerateRestApiRegisterExtensor(HashSet<Type> types, GenerationOptions options)
        {
            CodeWriter writer = new CodeWriter();
            foreach (var type in types)
            {
                var attr = type.GetCustomAttribute<GenerateMessageAttribute>(true);
                if (!type.IsClass || attr.Service == ServiceType.None || attr.Service == ServiceType.Interface)
                    continue;

                writer.Append($"services.AddScoped<I{type.Name}Service, {type.Name}Service>();").AppendLine();                
            }

            Directory.CreateDirectory($"{options.GetRestAPIOutputPath()}/Extensions");

            File.WriteAllText($"{options.GetRestAPIOutputPath()}/Extensions/{options.ServiceName}RegisterExtensions.cs",
               TemplateProcessor.Process(StartupRegisterTemplate, new
               {
                   SERVICE = options.ServiceName,
                   REGISTERS = writer.ToString()
               }));
        }      

        const string MappingTemplate = @"
using System;
using AutoMapper;
using @{ SERVICE }.Models;

namespace @{ SERVICE }.Services
{
    public class GeneratedAutoMapperProfile:Profile
    {
        public GeneratedAutoMapperProfile()
        {
           @{ MAPPINGS }        
        }
    }
}";

        const string ServiceImpTemplate = @"
using System;
using AutoMapper;
using Cybtans.Entities;
using Cybtans.Services;
using Microsoft.Extensions.Logging;
using @{ ENTITIES_NAMESPACE };
using @{ SERVICE }.Models;

namespace @{ SERVICE }.Services
{
    [RegisterDependency(typeof(@{SERVICE_NAME}))]
    public class @{ ENTITY }Service : CrudService<@{ENTITY}, @{TKEY}, @{TMESSAGE}, Get@{ENTITY}Request, GetAllRequest, GetAll@{ENTITY}Response, Update@{ENTITY}Request, Create@{ENTITY}Request, Delete@{ENTITY}Request>, @{SERVICE_NAME}
    {
        public @{ ENTITY }Service(IRepository<@{ENTITY}, @{TKEY}> repository, IUnitOfWork uow, IMapper mapper, ILogger<@{ENTITY}Service> logger)
            : base(repository, uow, mapper, logger) { }                
    }
}";

        const string ReadOnlyServiceImpTemplate = @"
using System;
using AutoMapper;
using Cybtans.Entities;
using Cybtans.Services;
using Microsoft.Extensions.Logging;
using @{ ENTITIES_NAMESPACE };
using @{ SERVICE }.Models;

namespace @{ SERVICE }.Services
{
    [RegisterDependency(typeof(@{SERVICE_NAME}))]
    public partial class @{ ENTITY }Service : ReadOnlyService<@{ENTITY}, @{TKEY}, @{TMESSAGE}, Get@{ENTITY}Request, GetAllRequest, GetAll@{ENTITY}Response>, @{SERVICE_NAME}
    {
        public @{ ENTITY }Service(IRepository<@{ENTITY}, @{TKEY}> repository, IUnitOfWork uow, IMapper mapper, ILogger<@{ENTITY}Service> logger)
            : base(repository, uow, mapper, logger) { }                
    }
}";


        const string ServiceImpPartialTemplate = @"
using System;
using AutoMapper;
using Cybtans.Entities;
using Cybtans.Services;
using Microsoft.Extensions.Logging;
using @{ ENTITIES_NAMESPACE };
using @{ SERVICE }.Models;

namespace @{ SERVICE }.Services
{
    [RegisterDependency(typeof(@{SERVICE_NAME}))]
    public partial class @{ ENTITY }Service : CrudService<@{ENTITY}, @{TKEY}, @{TMESSAGE}, Get@{ENTITY}Request, GetAllRequest, GetAll@{ENTITY}Response, Update@{ENTITY}Request, Create@{ENTITY}Request, Delete@{ENTITY}Request>, @{SERVICE_NAME}
    {
        
    }
}";


        const string StartupRegisterTemplate = @"
using Microsoft.Extensions.DependencyInjection;
using @{SERVICE}.Services;

namespace @{SERVICE}.RestApi
{
    public static class StartupAddServicesExtensions
    {
        public static void Add@{SERVICE}Services(this IServiceCollection services)
        {
            @{REGISTERS}
        }
    }
}";

        #endregion
    }
}
