using Cybtans.Services;
using System.Threading.Tasks;
using @{SERVICE}.Models;

namespace @{SERVICE}.Services
{
    [RegisterDependency(typeof(I@{SERVICE}Service))]
    public class @{SERVICE}Service : I@{SERVICE}Service
    {
        public Task<HelloReply> Hello(HelloRequest request)
        {
            return Task.FromResult(new HelloReply
            {
                Response = $"{request.Msg} Reply"
            });
        }
    }
}
