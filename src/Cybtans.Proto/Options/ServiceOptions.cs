#nullable enable

using Cybtans.Proto.AST;

namespace Cybtans.Proto.Options
{

    public class ServiceOptions: SecurityOptions
    {
        public ServiceOptions() : base(OptionsType.Service) 
        {
            ServiceSecurity = new Wrapper(this);
        }

        [Field("prefix")]
        public string? Prefix { get; set; }            

        [Field("description")]
        public string? Description { get; set; }

        [Field("grpc_proxy")]
        public bool GrpcProxy { get; set; }


        [Field("grpc_proxy_name")]
        public string? GrpcProxyName { get; set; }

        [Field("service_security")]
        public Wrapper ServiceSecurity { get; }

        [Field("service_description")]
        public string? ServiceDescription { get => Description; set => Description = value; }
    }
}
