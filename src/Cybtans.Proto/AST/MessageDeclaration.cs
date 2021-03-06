using Antlr4.Runtime;
using Cybtans.Proto.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Cybtans.Proto.AST
{

    public class MessageDeclaration : UserDefinedType<MessageOptions>
    {
        Scope _scope;

        public MessageDeclaration(IToken start, string name) : base(start, name)
        {
            IsValueType = false;
        }                        

        public List<MessageDeclaration> InnerMessages { get; } = new List<MessageDeclaration>();

        public List<FieldDeclaration> Fields { get; } = new List<FieldDeclaration>();

        public List<EnumDeclaration> Enums { get; } = new List<EnumDeclaration>();

        public string Namespace => ProtoDeclaration?.Option?.Namespace;     

        public Scope GetScope(Scope parent)
        {
            if(_scope == null)
            {
                _scope = parent.CreateScope();

                foreach (var item in InnerMessages)
                {
                    _scope.AddDeclaration(item);
                }

                foreach (var item in  Enums)
                {
                    _scope.AddDeclaration(item);
                }
            }

            return _scope;
        }

        public override void CheckSemantic(Scope scope, IErrorReporter logger)
        {           
            base.CheckSemantic(scope, logger);

            var childScope = GetScope(scope);
            foreach (var message in InnerMessages)
            {
                message.DeclaringMessage = this;
                message.ProtoDeclaration = ProtoDeclaration;
                message.CheckSemantic(childScope, logger);

                //message.SourceName = message.Name;
                //message.Name = $"{Name}_{message.Name}";
                //ProtoDeclaration?.Declarations.Add(message);
            }

            foreach (var e in Enums)
            {
                e.DeclaringMessage = this;
                e.ProtoDeclaration = ProtoDeclaration;
                e.CheckSemantic(childScope, logger);
                
                //e.SourceName = e.Name;
                //e.Name = $"{Name}_{e.Name}";
                //ProtoDeclaration?.Declarations.Add(e);
            }            
          

            foreach (var f in Fields)
            {
                f.Message = this;
                f.CheckSemantic(childScope, logger);
            }

            if(Fields.ToLookup(x => x.Number).Any(x=>x.Count() > 1))
            {
                logger.AddError($"Duplicated fields numbers in {Name} ({ string.Join(",", Fields.ToLookup(x => x.Number).Where(x => x.Count() > 1).Select(x => string.Join(",", x) +"="+ x.Key)) }) , {Line},{Column}");
            }

            if (Fields.ToLookup(x => x.Name).Any(x => x.Count() > 1))
            {
                logger.AddError($"Duplicated fields names in {Name} ({ string.Join(",", Fields.ToLookup(x => x.Name).Where(x => x.Count() > 1).Select(x => string.Join(",", x) + "=" + x.Key)) }) , {Line},{Column}");
            }

        }

        public List<FieldDeclaration>? GetPathBinding(string template)
        {
            if (template == null)
                return null;

            var regex = new Regex(@"{(\w+)}");
            MatchCollection matches = regex.Matches(template);
            if (matches.Any(x => x.Success))
            {
                List<FieldDeclaration> fields = new List<FieldDeclaration>();
                foreach (Match? match in matches)
                {
                    if (match != null && match.Success)
                    {
                        var name = match.Groups[1].Value;
                        var field = Fields.First(x => x.Name == name);
                        fields.Add(field);
                    }
                }

                return fields;
            }
            return null;
        }

        public void Merge(MessageDeclaration msg)
        {
            var max = Fields.Max(x => x.Number);            

            var fields = msg.Fields.Select(x =>
            {
                var f= x.Clone();
                f.Number += max;
                f.Message = this;
                return f;
            }).ToList();

            Fields.ForEach(x => x.IsExtension = true);
            Fields.AddRange(fields);

            Enums.AddRange(msg.Enums);
            InnerMessages.AddRange(msg.InnerMessages);
        }
            
    }

   
}
