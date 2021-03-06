using Antlr4.Runtime;
using Cybtans.Proto.Options;
using System.Collections.Generic;
using System.Linq;

namespace Cybtans.Proto.AST
{
    public class FieldDeclaration: DeclarationNode<FieldOptions>
    {        
        public FieldDeclaration(IToken start) : base(start)
        {
            
        }

        public FieldDeclaration(IToken start, TypeIdentifier typeRef, string name, int number, List<OptionsExpression> options)
            : this(start)
        {
            Type = typeRef;
            Name = name;
            Number = number;

            if (options != null)
            {
                Options = options;
            }
        }

        public FieldDeclaration(int line, int column, TypeIdentifier typeRef, string name, int number, List<OptionsExpression> options)
            :base(line, column)
        {
            Type = typeRef;
            Name = name;
            Number = number;

            if (options != null)
            {
                Options = options;
            }
        }


        public TypeIdentifier Type { get; set; }

        public ITypeDeclaration FieldType => Type?.TypeDeclaration;

        public int Number { get; set; }

        public bool IsExtension { get; set; }

        public MessageDeclaration Message { get; set; }

        public override void CheckSemantic(Scope scope, IErrorReporter logger)
        {
            base.CheckSemantic(scope, logger);

            Type.CheckSemantic(scope, logger);

            if (PrimitiveType.Void.Equals(FieldType))
            {
                logger.AddError($"Type void or google.protobuf.Empty is not supported as a field type in {Line},{Column}");
            }            

        }

        public override string ToString()
        {
            return Type.ToString() +" "+base.ToString();
        }

        public bool IsNullable
        {
            get
            {
                return !Option.Required && 
                (
                    Option.Optional
                    || Type.IsMap
                    || Type.IsArray
                    || FieldType is MessageDeclaration
                    || FieldType == PrimitiveType.String
                    || FieldType == PrimitiveType.Bytes
                    || FieldType == PrimitiveType.Stream
                    || FieldType == PrimitiveType.Object
                    || (FieldType is PrimitiveType p && p.IsNullableValue) 
                    || FieldType == PrimitiveType.BytesValue
                    || FieldType == PrimitiveType.StringValue
                    || FieldType == PrimitiveType.TimeStamp
                    || FieldType == PrimitiveType.Duration
                );
            }
        }
    
        public FieldDeclaration Clone()
        {
            return (FieldDeclaration)MemberwiseClone();            
        }
    }

    public class TypeIdentifier:ProtoAstNode
    {
        public TypeIdentifier() { }

        public TypeIdentifier(IdentifierExpression name)
        {
            this.Name = name;
        }

        public TypeIdentifier(string name)
        {
            this.Name = new IdentifierExpression(name);
        }

        public IdentifierExpression Name { get; set; }

        public bool IsArray { get; set; }

        public bool IsMap { get; set; }

        public TypeIdentifier[] GenericArgs { get; set; }

        public ITypeDeclaration TypeDeclaration { get; set; }

        public override void CheckSemantic(Scope scope, IErrorReporter logger)
        {
            TypeDeclaration = scope.GetDeclaration(Name);
            if(TypeDeclaration == null)
            {
                logger.AddError($"Type {Name} is not defined at {Name.Line},{Name.Column}");
            }            

            if(GenericArgs != null)
            {
                foreach (var genParameter in GenericArgs)
                {
                    genParameter.CheckSemantic(scope, logger);
                    if(PrimitiveType.Void.Equals(genParameter.TypeDeclaration))
                    {
                        logger.AddError($"Type void is not supported as a generic argument in {genParameter.Line},{genParameter.Column}");
                    }
                }
            }

        }

        public string Type
        {
            get
            {
                if (GenericArgs == null || GenericArgs.Length == 0)
                    return Name.ToString();

                var genArgs = GenericArgs
                    .Select(x => x.Type)
                    .Aggregate((a, b) => $"{a},{b}");

                return $"{Name}<{genArgs}>";
            }
        }

        public override string ToString()
        {
            return Type;
        }
    }
}
