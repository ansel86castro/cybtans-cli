#nullable enable


using System.Collections.Generic;
using System.Linq;

namespace Cybtans.Proto.AST
{
    public interface IErrorReporter
    {
        bool HaveErrors { get; }

        void AddError(string message);

        void AddWarning(string message);

        void EnsureNoErrors(string filename);
    }

    public class ErrorReporter : IErrorReporter
    {
        public enum ErrorType
        {
            Error,
            Warning
        }
        public class ErrorInfo
        {
            public ErrorInfo(ErrorType type, string message)
            {
                Type = type;
                Message = message;
            }

            public ErrorType Type { get; set; }

            public string Message { get; set; }            
        }

        public List<ErrorInfo> Errors { get; } = new List<ErrorInfo>();

        public bool HaveErrors => throw new System.NotImplementedException();

        public void AddError(string message)
        {
            Errors.Add(new ErrorInfo(ErrorType.Error, message));
        }

        public void AddWarning(string message)
        {
            Errors.Add(new ErrorInfo(ErrorType.Warning, message));
        }

        public void EnsureNoErrors(string filename)
        {
            if (Errors.Any(x=>x.Type == ErrorType.Error))
            {
                var errors = new List<string>();
                errors.Add($"Errors in {filename}");
                errors.AddRange(Errors.Select(x => $"{x.Type} {x.Message}"));
                throw new Proto3RecognitionException(errors);
            }
        }
    }
}
