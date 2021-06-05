#nullable enable

using Cybtans.Proto.AST;
using Cybtans.Proto.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Markup;

namespace Cybtans.Proto.Generators.CSharp
{

    public class TypeGenerator : FileGenerator<ModelGeneratorOptions>
    {
        Dictionary<ITypeDeclaration, MessageClassInfo> _messages = new Dictionary<ITypeDeclaration, MessageClassInfo>();

        public TypeGenerator(ProtoFile proto, ModelGeneratorOptions option) : base(proto, option)
        {
            Namespace = option.Namespace ?? $"{proto.Option.Namespace ?? proto.Filename.Pascal()}.Models";
        }

        public MessageClassInfo GetMessageInfo(ITypeDeclaration declaration)
        {
            if (!_messages.TryGetValue(declaration, out var info))
            {
                info = new MessageClassInfo((MessageDeclaration)declaration, _option, Proto);
                _messages.Add(declaration, info);
            }
            return info;
        }

        public string Namespace { get; set; }

        protected override void GenerateCode(ProtoFile proto)
        {
            foreach (var item in proto.Declarations)
            {
                if (item is MessageDeclaration msg)
                {
                    if (msg.DeclaringMessage != null)
                        continue;

                    var info = new MessageClassInfo(msg, _option, proto);

                    GenerateMessage(info);

                    _messages.Add(msg, info);
                }
            }
        }

        private void GenerateMessage(MessageClassInfo info)
        {
            var writer = CreateWriter(Namespace);            
            var usingWriter = writer.Usings;

            usingWriter.Append("using System;").AppendLine();

            if (_option.GenerateAccesor)
            {
                usingWriter.Append("using Cybtans.Serialization;").AppendLine();
            }

            var usings = new HashSet<string>();         

            GenerateMessage(info, writer.Class, usings);

            foreach (var item in usings)
            {
                usingWriter.Append(item).AppendLine();
            }

            writer.Save(info.Name);

        }

        private void GenerateMessage(MessageClassInfo info, CodeWriter clsWriter, HashSet<string> usings)
        {
            MessageDeclaration msg = info.Message;

            if (msg.Option.Description != null)
            {
                clsWriter.Append("/// <summary>").AppendLine();
                clsWriter.Append("/// ").Append(msg.Option.Description).AppendLine();
                clsWriter.Append("/// </summary>").AppendLine();
                clsWriter.Append($"[Description(\"{msg.Option.Description.Scape()}\")]").AppendLine();
            }

            if (msg.Option.Deprecated)
            {
                clsWriter.Append($"[Obsolete]").AppendLine();
            }

            clsWriter.Append("public ");

            if (_option.PartialClass)
            {
                clsWriter.Append("partial ");
            }

            clsWriter.Append($"class {info.Name} ");

            if (msg.Option.Base != null)
            {
                clsWriter.Append($": {msg.Option.Base}");
                if (_option.GenerateAccesor)
                {
                    clsWriter.Append(", IReflectorMetadataProvider");
                }
            }
            else if (_option.GenerateAccesor)
            {
                clsWriter.Append(": IReflectorMetadataProvider");
            }

            clsWriter.AppendLine();
            clsWriter.Append("{").AppendLine();
            clsWriter.Append('\t', 1);

            var bodyWriter = clsWriter.Block("BODY");

            if (msg.Fields.Any(x => x.Type.IsMap || x.Type.IsArray))
            {
                usings.Add("using System.Collections.Generic;");
            }

            if (msg.Option.Description != null || msg.Fields.Any(x => x.Option.Description != null))
            {
                usings.Add("using System.ComponentModel;");
            }

            if (msg.Fields.Any(x => x.Option.Required))
            {
                usings.Add("using System.ComponentModel.DataAnnotations;");
            }

            if (_option.GenerateAccesor)
            {
                bodyWriter.Append($"private static readonly {info.Name}Accesor __accesor = new {info.Name}Accesor();")
                    .AppendLine()
                    .AppendLine();
            }

            foreach (var fieldInfo in info.Fields.Values.OrderBy(x => x.Field.Number))
            {
               
                var field = fieldInfo.Field;

                if (field.Option.Description != null)
                {
                    bodyWriter.Append("/// <summary>").AppendLine();
                    bodyWriter.Append("/// ").Append(field.Option.Description).AppendLine();
                    bodyWriter.Append("/// </summary>").AppendLine();
                }

                if (field.Option.Required)
                {
                    bodyWriter.Append("[Required]").AppendLine();
                }

                if (field.Option.Deprecated)
                {
                    bodyWriter.Append("[Obsolete]").AppendLine();
                }

                if (field.Option.Description != null)
                {
                    bodyWriter.Append($"[Description(\"{field.Option.Description.Scape()}\")]").AppendLine();
                }

                bodyWriter
                    .Append("public ")
                    .Append(fieldInfo.Type);

                bodyWriter.Append($" {fieldInfo.Name} {{get; set;}}");

                if (field.Option.Default != null)
                {
                    bodyWriter.Append(" = ").Append(field.Option.Default.ToString()).Append(";");
                }

                bodyWriter.AppendLine(2);
                
            }

            if (_option.GenerateAccesor)
            {
                bodyWriter.Append("public IReflectorMetadata GetAccesor()\r\n{\r\n\treturn __accesor;\r\n}");
            }

            if (info.Fields.Count == 1)
            {
                //Generate ImplicitConverter
                var field = info.Fields.First().Value;
                bodyWriter.AppendLine();
                bodyWriter.Append($"public static implicit operator {info.Name}({field.Type} {field.Field.Name})\r\n{{");
                bodyWriter.AppendLine();
                bodyWriter.Append('\t', 1).Append($"return new {info.Name} {{ {field.Name} = {field.Field.Name} }};");
                bodyWriter.AppendLine();
                bodyWriter.Append("}").AppendLine();
            }         

            if (_option.GenerateAccesor)
            {
                clsWriter.AppendLine(2).Append($"\t#region {info.Name}  Accesor");
                clsWriter.AppendLine().Append('\t', 1);

                GenerateAccesor(info, clsWriter.Block($"ACESSOR_{info.Name}"));

                clsWriter.Append("\t#endregion").AppendLine();
                clsWriter.AppendLine();
            }

            
            if (msg.InnerMessages.Any() || msg.Enums.Any())
            {
                clsWriter.AppendLine().Append($"\t#region {info.Name} Nested types").AppendLine();
                clsWriter.Append("\tpublic static class Types").AppendLine().
                    Append("\t{").AppendLine()
                    .Append('\t', 2);

                var innerTypeWriter = clsWriter.Block($"TYPES_{info.Name}");

                foreach (var inner in msg.InnerMessages)
                {                  
                    GenerateMessage(new MessageClassInfo(inner, _option, info.Proto), innerTypeWriter, usings);
                }

                foreach (var inner in msg.Enums)
                {                 
                    GenerateEnum(inner, innerTypeWriter);
                }

                clsWriter.AppendLine().Append("\t}").AppendLine();
                clsWriter.Append("\t#endregion").AppendLine();
            }

            clsWriter.AppendLine().Append("}").AppendLine();
        }

        private void GenerateAccesor(MessageClassInfo info, CodeWriter clsWriter)
        {
            clsWriter.Append($"public sealed class {info.Name}Accesor : IReflectorMetadata").AppendLine();
            clsWriter.Append("{")
                .AppendLine().Append('\t', 1);

            var body = clsWriter.Block("ACCESOR_BODY");
            var fields = info.Fields.Values.OrderBy(x=>x.Field.Number);

            var getTypeSwtich = new StringBuilder();
            var getValueSwtich = new StringBuilder();
            var setValueSwtich = new StringBuilder();
            var getPropertyName = new StringBuilder();
            var getPropertyCode = new StringBuilder();

            foreach (var field in fields)
            {
                body.Append($"public const int {field.Name} = {field.Field.Number};").AppendLine();                

                getTypeSwtich.Append($"{field.Name} => typeof({field.Type}),\r\n");
                getValueSwtich.Append($"{field.Name} => obj.{field.Name},\r\n");
                setValueSwtich.Append($"case {field.Name}:  obj.{field.Name} = ({field.Type})value;break;\r\n");
                getPropertyName.Append($"{field.Name} => \"{field.Name}\",\r\n");
                getPropertyCode.Append($"\"{field.Name}\" => {field.Name},\r\n");
            }

            body.Append("private readonly int[] _props = new []").AppendLine();
            body.Append("{").AppendLine();

            body.Append('\t',1).Append(string.Join(",", fields.Select(x => x.Name)));
            body.AppendLine();

            body.Append("};").AppendLine(2);

            body.Append("public int[] GetPropertyCodes() => _props;")
                .AppendLine();

            body.AppendTemplate(Template, new Dictionary<string, object>
            {
                ["TYPE"] = info.Name,
                ["SWITCH"]= getTypeSwtich.ToString(),
                ["GET_VALUE"] = getValueSwtich.ToString(),
                ["SET_VALUE"] = setValueSwtich.ToString(),
                ["GET_PROPERTY_NAME"] = getPropertyName.ToString(),
                ["GET_PROPERTY_CODE"] = getPropertyCode.ToString()
            });

            clsWriter.AppendLine();
            clsWriter.Append("}")
                .AppendLine();
        }

        private void GenerateEnum(EnumDeclaration decl, CodeWriter clsWriter)
        {
            if (decl.Option.Description != null)
            {
                clsWriter.Append("/// <summary>").AppendLine();
                clsWriter.Append("/// ").Append(decl.Option.Description).AppendLine();
                clsWriter.Append("/// </summary>").AppendLine();
                clsWriter.Append($"[Description(\"{decl.Option.Description}\")]").AppendLine();
            }

            if (decl.Option.Deprecated)
            {
                clsWriter.Append($"[Obsolete]").AppendLine();
            }

            clsWriter.Append("public ");
            clsWriter.Append($"enum {decl.GetTypeName()} ").AppendLine();

            clsWriter.Append("{").AppendLine();
            clsWriter.Append('\t', 1);

            var bodyWriter = clsWriter.Block($"BODY_{decl.Name}");

            foreach (var item in decl.Members.OrderBy(x => x.Value))
            {
                if (item.Option.Description != null)
                {
                    bodyWriter.Append("/// <summary>").AppendLine();
                    bodyWriter.Append("/// ").Append(item.Option.Description).AppendLine();
                    bodyWriter.Append("/// </summary>").AppendLine();
                    bodyWriter.Append($"[Description(\"{item.Option.Description}\")]").AppendLine();
                }

                if (item.Option.Deprecated)
                {
                    bodyWriter.Append($"[Obsolete]").AppendLine();
                }

                bodyWriter.Append(item.Name).Append(" = ").Append(item.Value.ToString()).Append(",");
                bodyWriter.AppendLine();
                bodyWriter.AppendLine();
            }

            clsWriter.Append("}").AppendLine();
        }


        string Template = @"
public string GetPropertyName(int propertyCode)
{
    return propertyCode switch
    {
       @{GET_PROPERTY_NAME}
        _ => throw new InvalidOperationException(""property code not supported""),
    };
}

public int GetPropertyCode(string propertyName)
{
    return propertyName switch
    {
        @{GET_PROPERTY_CODE}
        _ => -1,
    };
}

public Type GetPropertyType(int propertyCode)
{
    return propertyCode switch
    {
        @{SWITCH}
        _ => throw new InvalidOperationException(""property code not supported""),
    };
}
       
public object GetValue(object target, int propertyCode)
{
    @{TYPE} obj = (@{TYPE})target;
    return propertyCode switch
    {
        @{GET_VALUE}
        _ => throw new InvalidOperationException(""property code not supported""),
    };
}

public void SetValue(object target, int propertyCode, object value)
{
    @{TYPE} obj = (@{TYPE})target;
    switch (propertyCode)
    {
        @{SET_VALUE}
        default: throw new InvalidOperationException(""property code not supported"");
    }
}
";

    }




}
