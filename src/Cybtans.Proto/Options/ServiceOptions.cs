#nullable enable

using Cybtans.Proto.AST;
using System.Collections.Generic;
using System.Linq;

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

        [Field("attributes")]
        public string? Attributes { get; set; }

        [Field("srv_attributes")]
        public string? SrvAttributes { get => Attributes; set => Attributes = value; }

        [Field("constructor")]
        public ServiceConstructorOptions ConstructorOptions { get; set; }

    }

    public class ServiceConstructorOptions : ProtobufOption
    {
        public ServiceConstructorOptions() : base(OptionsType.Service)
        {
        }

        [Field("access")]
        public string? Visibility { get; set; } = "public";

        [Field("params")]
        public string? Parameters { get; set; }

        public bool HasAdditionalParameters => Parameters != null;

        public List<(string name, string type)> GetParameters()
        {
            if (Parameters == null) return new List<(string name, string type)>();

            var parts = Parameters.Split(',', System.StringSplitOptions.RemoveEmptyEntries);

            return parts
                .Select(x => x.Split(' ', System.StringSplitOptions.RemoveEmptyEntries))
                .Where(x=>x.Length == 2)
                .Select(x=> (x[1], x[0]))
                .ToList();

        }
    }
}
