#nullable enable

using Cybtans.Proto.AST;
using System;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Cybtans.Proto.Options
{

    public class MessageOptions : ProtobufOption
    {
        public MessageOptions() : base(OptionsType.Message)
        {
        }

        [Field("base")]
        public string? Base { get; set; }

        [Field("deprecated")]
        public bool Deprecated { get; set; }

        [Field("description")]
        public string? Description { get; set; }

        [Field("grpc_request")]
        public bool GrpcRequest { get; set; }

        [Field("grpc_response")]
        public bool GrpcResponse { get; set; }

        [Field("message_description")]
        public string? MessageDescription { get => Description; set => Description = value; }
    }
}
