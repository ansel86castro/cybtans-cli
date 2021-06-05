#nullable enable

using Cybtans.Proto.AST;

namespace Cybtans.Proto.Options
{
    public class RpcOptions : SecurityOptions
    {
        public RpcOptions() : base(OptionsType.Rpc)
        {
            RpcSecurity = new Wrapper(this);
        }

        [Field("template")]
        public string? Template { get; set; }

        [Field("method")]
        public string? Method { get; set; }

        [Field("file")]
        public StreamOptions? StreamOptions { get; set; }

        [Field("description")]
        public string? Description { get; set; }

        [Field("rpc_description")]
        public string? RpcDescription { get => Description; set => Description = value; }

        [Field("google")]
        public GoogleHttpOptions? Google { get; set; }

        [Field("rpc_security")]
        public Wrapper RpcSecurity { get; }

        /// <summary>
        /// valid values are "request", "response", "all"
        /// </summary>
        [Field("grpc_mapping")]
        public string GrpcMapping { get; set; }

        public bool GrpcMappingRequest => GrpcMapping == "request" || GrpcMapping == "all";

        public bool GrpcMappingResponse => GrpcMapping == "response" || GrpcMapping == "all";

        [Field("graphql")]
        public GraphQlOptions GraphQl { get; set; }

        [Field("attributes")]
        public string? Attributes { get; set; }

        [Field("rpc_attributes")]
        public string? RpcAttributes { get => Attributes; set => Attributes = value; }

    }

    public class StreamOptions: ProtobufOption
    {
        public StreamOptions() : base(OptionsType.Rpc)
        {
        }

        [Field("contentType")]
        public string? ContentType { get; set; }

        [Field("name")]
        public string? Name { get; set; }
    }

    public class GoogleHttpOptions: ProtobufOption
    {
        public GoogleHttpOptions() : base(OptionsType.Rpc) { }

        [Field("api")]
        public GoogleApi? Api { get; set; }
    }

    public class GoogleApi : ProtobufOption
    {
        public GoogleApi() : base(OptionsType.Rpc) { }

        [Field("http")]
        public GoogleApi? Http { get; set; }
    }

    public class GraphQlOptions : ProtobufOption
    {
        public GraphQlOptions(): base(OptionsType.Rpc)
        {

        }

        [Field("query")]
        public string? Query { get; set; }
    }
}
