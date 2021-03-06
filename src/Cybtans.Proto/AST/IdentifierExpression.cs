using Antlr4.Runtime;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Cybtans.Proto.AST
{
    public class IdentifierExpression:ExpressionNode ,IEquatable<IdentifierExpression>
    {
      
        public IdentifierExpression(string id, IdentifierExpression left, IToken start)
            :base(start)
        {
            Id = id;
            Left = left;
        }

        public IdentifierExpression(string id)
        {
            Id = id;
        }

        public IdentifierExpression Left { get; }

        public string Id { get; }

        public override string ToString()
        {
            return Left != null ? $"{Left}.{Id}" : Id;
        }

        public override void CheckSemantic(Scope scope, IErrorReporter logger)
        {
           
        }

        public bool Equals([AllowNull] IdentifierExpression other)
        {
            if (other == null) return false;
            if (Id != other.Id) return false;
            if (Left != null && Left.Equals(other.Left)) return false;
            return true;
        }
    }
}
